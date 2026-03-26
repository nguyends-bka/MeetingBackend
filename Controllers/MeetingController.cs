using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using MeetingBackend.Data;
using MeetingBackend.DTOs.Meeting;
using MeetingBackend.Entities;
using MeetingBackend.Mappers;
using MeetingBackend.Policies;
using MeetingBackend.Services;

namespace MeetingBackend.Controllers;

[ApiController]
[Route("api/meeting")]
[Authorize] // 🔐 TẤT CẢ API PHẢI LOGIN
public class MeetingController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly LiveKitTokenService _liveKit;
    private readonly IConfiguration _config;
    private readonly MeetingCodeService _codeService;

    public MeetingController(
        AppDbContext db,
        LiveKitTokenService liveKit,
        IConfiguration config,
        MeetingCodeService codeService)
    {
        _db = db;
        _liveKit = liveKit;
        _config = config;
        _codeService = codeService;
    }

    // Helper method để ghi lại lịch sử vào meeting
    private async Task<MeetingParticipant> RecordJoinAsync(Guid meetingId, string userId, string username)
    {
        var meeting = await _db.Meetings.FirstOrDefaultAsync(m => m.Id == meetingId);
        if (meeting == null)
        {
            throw new InvalidOperationException("Meeting not found");
        }

        // Nếu user đang có session active trong meeting này, không tạo thêm record mới
        var existingActive = await _db.MeetingParticipants
            .FirstOrDefaultAsync(p =>
                p.MeetingId == meetingId &&
                p.UserId == userId &&
                p.LeftAt == null);

        if (existingActive != null)
        {
            // Đồng bộ username (phòng trường hợp username thay đổi)
            if (!string.Equals(existingActive.Username, username, StringComparison.Ordinal))
            {
                existingActive.Username = username;
                await _db.SaveChangesAsync();
            }
            return existingActive;
        }

        if (meeting.EndedAt.HasValue)
        {
            // Meeting đã kết thúc, không cho join tiếp.
            throw new UnauthorizedAccessException("Meeting has ended");
        }

        var participant = new MeetingParticipant
        {
            Id = Guid.NewGuid(),
            MeetingId = meetingId,
            UserId = userId,
            Username = username,
            JoinedAt = DateTime.UtcNow
        };
        _db.MeetingParticipants.Add(participant);

        if (!meeting.StartedAt.HasValue)
        {
            meeting.StartedAt = participant.JoinedAt;
        }

        await _db.SaveChangesAsync();
        return participant;
    }

    // ==========================
    // USER/ADMIN TẠO MEETING
    // ==========================
    [HttpPost("create")]
    [Authorize(Policy = AuthorizationPolicies.UserOrAdmin)]
    public async Task<IActionResult> Create(CreateMeetingRequestDto request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue(ClaimTypes.Name);

        // Tạo meeting code duy nhất
        var meetingCode = await _codeService.GenerateUniqueCodeAsync();
        
        // Tạo passcode (tự động nếu không có)
        var passcode = !string.IsNullOrEmpty(request.Passcode) 
            ? request.Passcode 
            : _codeService.GeneratePasscode(6);

        var meeting = new Meeting
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            HostName = request.HostName,
            HostIdentity = userId!,
            RoomName = Guid.NewGuid().ToString(),
            MeetingCode = meetingCode,
            Passcode = passcode,
            CreatedAt = DateTime.UtcNow
        };

        _db.Meetings.Add(meeting);
        await _db.SaveChangesAsync();

        var response = new CreateMeetingResponseDto
        {
            MeetingId = meeting.Id,
            MeetingCode = meeting.MeetingCode,
            Passcode = meeting.Passcode,
            RoomName = meeting.RoomName
        };

        return Ok(response);
    }

    // ==========================
    // JOIN MEETING BY LINK (KHÔNG CẦN PASSCODE)
    // ==========================
    [HttpPost("join-by-link")]
    public async Task<IActionResult> JoinByLink([FromBody] JoinByLinkRequestDto req)
    {
        if (req.MeetingId == Guid.Empty)
            return BadRequest("Meeting ID is required");

        var meeting = await _db.Meetings
            .FirstOrDefaultAsync(m => m.Id == req.MeetingId);

        if (meeting == null)
            return NotFound("Meeting not found");

        // 🔐 LẤY IDENTITY TỪ JWT (KHÔNG TIN CLIENT)
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue(ClaimTypes.Name);
        var username = User.FindFirstValue("username") ?? "Unknown";

        // Ghi lại lịch sử vào meeting
        MeetingParticipant participant;
        try
        {
            participant = await RecordJoinAsync(meeting.Id, userId!, username);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ex.Message);
        }

        var token = _liveKit.CreateToken(
            meeting.RoomName,
            userId!,
            username
        );

        var response = new JoinMeetingResponseDto
        {
            Token = token,
            LiveKitUrl = _config["LiveKit:Url"]!,
            RoomName = meeting.RoomName,
            MeetingId = meeting.Id,
            MeetingCode = meeting.MeetingCode,
            ParticipantId = participant.Id,
            HostIdentity = meeting.HostIdentity
        };

        return Ok(response);
    }

    // ==========================
    // JOIN MEETING BY ID/CODE + PASSCODE
    // ==========================
    [HttpPost("join")]
    public async Task<IActionResult> Join(JoinMeetingRequestDto req)
    {
        Meeting? meeting = null;

        // Tham gia bằng mã: chỉ cần meetingCode + passcode. Tham gia bằng ID: meetingId + passcode.
        if (req.MeetingId.HasValue && req.MeetingId.Value != Guid.Empty)
        {
            meeting = await _db.Meetings
                .FirstOrDefaultAsync(m => m.Id == req.MeetingId!.Value);
        }
        else if (!string.IsNullOrEmpty(req.MeetingCode))
        {
            meeting = await _db.Meetings
                .FirstOrDefaultAsync(m => m.MeetingCode == req.MeetingCode.ToUpper().Trim());
        }

        if (meeting == null)
            return NotFound("Meeting not found");

        // Kiểm tra passcode
        if (string.IsNullOrEmpty(req.Passcode) || meeting.Passcode != req.Passcode)
        {
            return Unauthorized("Invalid passcode");
        }

        // 🔐 LẤY IDENTITY TỪ JWT (KHÔNG TIN CLIENT)
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue(ClaimTypes.Name);
        var username = User.FindFirstValue("username") ?? "Unknown";

        // Ghi lại lịch sử vào meeting
        MeetingParticipant participant;
        try
        {
            participant = await RecordJoinAsync(meeting.Id, userId!, username);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ex.Message);
        }

        var token = _liveKit.CreateToken(
            meeting.RoomName,
            userId!,
            username
        );

        var response = new JoinMeetingResponseDto
        {
            Token = token,
            LiveKitUrl = _config["LiveKit:Url"]!,
            RoomName = meeting.RoomName,
            MeetingId = meeting.Id,
            MeetingCode = meeting.MeetingCode,
            ParticipantId = participant.Id,
            HostIdentity = meeting.HostIdentity
        };

        return Ok(response);
    }

    // ==========================
    // USER JOIN MEETING BY CODE
    // ==========================
    [HttpPost("join-by-code")]
    public async Task<IActionResult> JoinByCode([FromBody] JoinMeetingRequestDto req)
    {
        if (string.IsNullOrEmpty(req.MeetingCode))
            return BadRequest("Meeting code is required");

        var meeting = await _db.Meetings
            .FirstOrDefaultAsync(m => m.MeetingCode == req.MeetingCode.ToUpper().Trim());

        if (meeting == null)
            return NotFound("Meeting not found");

        // Kiểm tra passcode
        if (string.IsNullOrEmpty(req.Passcode) || meeting.Passcode != req.Passcode)
        {
            return Unauthorized("Invalid passcode");
        }

        // 🔐 LẤY IDENTITY TỪ JWT (KHÔNG TIN CLIENT)
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue(ClaimTypes.Name);
        var username = User.FindFirstValue("username") ?? "Unknown";

        // Ghi lại lịch sử vào meeting
        MeetingParticipant participant;
        try
        {
            participant = await RecordJoinAsync(meeting.Id, userId!, username);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ex.Message);
        }

        var token = _liveKit.CreateToken(
            meeting.RoomName,
            userId!,
            username
        );

        return Ok(new
        {
            token,
            liveKitUrl = _config["LiveKit:Url"],
            roomName = meeting.RoomName,
            meetingId = meeting.Id,
            meetingCode = meeting.MeetingCode,
            title = meeting.Title,
            participantId = participant.Id,
            hostIdentity = meeting.HostIdentity
        });
    }

    // ==========================
    // LẤY DANH SÁCH MEETING
    // User: chỉ thấy meeting của mình
    // Admin: thấy tất cả meetings
    // ==========================
    [HttpGet]
    public async Task<IActionResult> GetMeetings()
    {
        // Lấy userId và role từ JWT token
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue(ClaimTypes.Name);
        var userRole = User.FindFirstValue(ClaimTypes.Role);
        var username = User.FindFirstValue("username") ?? string.Empty;

        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized("User identity not found");
        }

        IQueryable<Meeting> query = _db.Meetings;

        // Nếu không phải Admin, chỉ lấy meeting của user hiện tại
        if (userRole != "Admin")
        {
            query = query.Where(m => m.HostIdentity == userId);
        }

        var meetings = await query
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();

        var managerMeetingIds = string.IsNullOrWhiteSpace(username)
            ? new HashSet<Guid>()
            : (await _db.MeetingPollManagers
                .AsNoTracking()
                .Where(x => x.Username.ToLower() == username.Trim().ToLower())
                .Select(x => x.MeetingId)
                .ToListAsync())
              .ToHashSet();

        var normalizedUserId = userId.Trim();
        var normalizedUsername = username.Trim();
        var response = meetings.Select(m =>
        {
            var isHost = string.Equals(m.HostIdentity, normalizedUserId, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(normalizedUsername)
                    && string.Equals(m.HostIdentity, normalizedUsername, StringComparison.OrdinalIgnoreCase));
            var canManagePoll = isHost || managerMeetingIds.Contains(m.Id);
            var dto = MeetingMapper.ToMeetingListItemDto(m);
            dto.CanManagePoll = canManagePoll;
            return dto;
        }).ToList();
        return Ok(response);
    }

    // ==========================
    // GHI LẠI KHI USER LEAVE MEETING
    // ==========================
    [HttpPost("leave")]
    [Authorize]
    public async Task<IActionResult> Leave([FromBody] LeaveMeetingRequestDto req)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue(ClaimTypes.Name);

        // Ưu tiên dùng MeetingId để đóng TẤT CẢ session active trong meeting
        var meetingId = req.MeetingId ?? Guid.Empty;

        // Fallback: nếu client không gửi MeetingId, thử suy ra từ ParticipantId
        if (meetingId == Guid.Empty && req.ParticipantId.HasValue)
        {
            meetingId = await _db.MeetingParticipants
                .Where(p => p.Id == req.ParticipantId.Value && p.UserId == userId)
                .Select(p => p.MeetingId)
                .FirstOrDefaultAsync();
        }

        if (meetingId == Guid.Empty)
        {
            return BadRequest("MeetingId is required");
        }

        var now = DateTime.UtcNow;

        // Đóng tất cả session active của user trong meeting này (khắc phục duplicate 'Đang tham gia')
        var actives = await _db.MeetingParticipants
            .Where(p =>
                p.MeetingId == meetingId &&
                p.UserId == userId &&
                p.LeftAt == null)
            .ToListAsync();

        foreach (var p in actives)
        {
            p.LeftAt = now;
        }

        if (actives.Count > 0)
        {
            await _db.SaveChangesAsync();
        }

        var response = new LeaveMeetingResponseDto
        {
            Message = "Left meeting successfully",
            UpdatedCount = actives.Count
        };

        return Ok(response);
    }

    // ==========================
    // HOST KẾT THÚC CUỘC HỌP
    // ==========================
    [HttpPost("{meetingId}/end")]
    [Authorize]
    public async Task<IActionResult> EndMeeting(Guid meetingId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue(ClaimTypes.Name);
        var username = User.FindFirstValue("username") ?? string.Empty;

        var meeting = await _db.Meetings.FirstOrDefaultAsync(m => m.Id == meetingId);
        if (meeting == null) return NotFound("Meeting not found");

        var normalizedUserId = (userId ?? string.Empty).Trim();
        var normalizedUsername = username.Trim();
        var isHost = string.Equals(meeting.HostIdentity, normalizedUserId, StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrWhiteSpace(normalizedUsername)
                && string.Equals(meeting.HostIdentity, normalizedUsername, StringComparison.OrdinalIgnoreCase));

        if (!isHost)
        {
            return Unauthorized("Only meeting host can end meeting");
        }

        var now = DateTime.UtcNow;
        meeting.StartedAt ??= now;
        meeting.EndedAt = now;

        // đóng tất cả session đang active trong meeting
        var actives = await _db.MeetingParticipants
            .Where(p => p.MeetingId == meetingId && p.LeftAt == null)
            .ToListAsync();
        foreach (var p in actives)
        {
            p.LeftAt = now;
        }

        await _db.SaveChangesAsync();
        return Ok(new { message = "Meeting ended", endedAt = meeting.EndedAt });
    }

    // ==========================
    // XEM LỊCH SỬ VÀO/RA CỦA MEETING
    // Host: chỉ xem được meeting của mình
    // Admin: xem được tất cả meetings
    // ==========================
    [HttpGet("{meetingId}/history")]
    [Authorize]
    public async Task<IActionResult> GetMeetingHistory(Guid meetingId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue(ClaimTypes.Name);
        var userRole = User.FindFirstValue(ClaimTypes.Role);

        // Kiểm tra user có phải host không
        var meeting = await _db.Meetings
            .FirstOrDefaultAsync(m => m.Id == meetingId);

        if (meeting == null)
            return NotFound("Meeting not found");

        // Chỉ host hoặc Admin mới xem được lịch sử
        if (userRole != "Admin" && meeting.HostIdentity != userId)
            return Unauthorized("Only meeting host or Admin can view history");

        var participants = await _db.MeetingParticipants
            .Where(p => p.MeetingId == meetingId)
            .OrderByDescending(p => p.JoinedAt)
            .ToListAsync();

        var history = participants.Select(MeetingMapper.ToMeetingHistoryItemDto).ToList();
        return Ok(history);
    }

    // ==========================
    // LẤY LỊCH SỬ THAM GIA CỦA USER HIỆN TẠI
    // ==========================
    [HttpGet("my-history")]
    [Authorize]
    public async Task<IActionResult> GetMyHistory()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue(ClaimTypes.Name);

        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized("User identity not found");
        }

        var history = await _db.MeetingParticipants
            .Where(p => p.UserId == userId)
            .Join(
                _db.Meetings,
                participant => participant.MeetingId,
                meeting => meeting.Id,
                (participant, meeting) => new MyHistoryItemDto
                {
                    Id = participant.Id,
                    MeetingId = participant.MeetingId,
                    MeetingTitle = meeting.Title,
                    Username = participant.Username,
                    JoinedAt = participant.JoinedAt,
                    LeftAt = participant.LeftAt,
                    Duration = participant.LeftAt.HasValue
                        ? (participant.LeftAt.Value - participant.JoinedAt).TotalMinutes
                        : null,
                    MeetingCode = meeting.MeetingCode,
                    HostName = meeting.HostName
                }
            )
            .OrderByDescending(h => h.JoinedAt)
            .ToListAsync();

        return Ok(history);
    }
}
