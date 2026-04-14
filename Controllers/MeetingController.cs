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

    private static DateTime FromUnixMs(long unixMs)
    {
        return DateTimeOffset.FromUnixTimeMilliseconds(unixMs).UtcDateTime;
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

        var startAtUtc = request.StartAt.HasValue
            ? FromUnixMs(request.StartAt.Value)
            : DateTime.UtcNow;
        DateTime? estimatedEndUtc = request.EstimatedEndAt.HasValue
            ? FromUnixMs(request.EstimatedEndAt.Value)
            : null;
        if (estimatedEndUtc.HasValue && estimatedEndUtc.Value <= startAtUtc)
        {
            return BadRequest("Thời gian kết thúc dự kiến phải sau thời gian bắt đầu");
        }

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
            // Hệ thống hiện dùng CreatedAt làm thời gian bắt đầu hiển thị theo lịch.
            CreatedAt = startAtUtc,
            // Tái sử dụng StartedAt để lưu thời gian kết thúc dự kiến (đang dùng nhất quán với API update).
            StartedAt = estimatedEndUtc
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
            HostIdentity = meeting.HostIdentity,
            IsMeetingHost = await MeetingHostAuth.IsAnyHostAsync(_db, meeting, userId!, username),
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
            HostIdentity = meeting.HostIdentity,
            IsMeetingHost = await MeetingHostAuth.IsAnyHostAsync(_db, meeting, userId!, username),
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
            hostIdentity = meeting.HostIdentity,
            isMeetingHost = await MeetingHostAuth.IsAnyHostAsync(_db, meeting, userId!, username),
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

        HashSet<Guid> coHostMeetingIdSet = new();
        if (userRole != "Admin")
        {
            var coIds = await _db.MeetingCoHosts
                .AsNoTracking()
                .Where(c => c.HostUserId == userId)
                .Select(c => c.MeetingId)
                .ToListAsync();
            coHostMeetingIdSet = coIds.ToHashSet();
            query = query.Where(m => m.HostIdentity == userId || coHostMeetingIdSet.Contains(m.Id));
        }

        var meetings = await query
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();
        var meetingIds = meetings.Select(m => m.Id).ToList();

        var activeCounts = await _db.MeetingParticipants
            .Where(p => meetingIds.Contains(p.MeetingId) && p.LeftAt == null)
            .GroupBy(p => p.MeetingId)
            .Select(g => new { MeetingId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.MeetingId, x => x.Count);

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
            var isPrimaryHost = string.Equals(m.HostIdentity, normalizedUserId, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(normalizedUsername)
                    && string.Equals(m.HostIdentity, normalizedUsername, StringComparison.OrdinalIgnoreCase));
            var isCoHost = coHostMeetingIdSet.Contains(m.Id);
            var isMeetingHost = isPrimaryHost || isCoHost;
            var canManagePoll = isMeetingHost || managerMeetingIds.Contains(m.Id);
            var dto = MeetingMapper.ToMeetingListItemDto(m);
            dto.IsMeetingHost = isMeetingHost;
            dto.CanManagePoll = canManagePoll;
            dto.ActiveParticipantCount = activeCounts.TryGetValue(m.Id, out var c) ? c : 0;
            return dto;
        }).ToList();
        return Ok(response);
    }

    // ==========================
    // HOST CHỈNH SỬA CUỘC HỌP (khi chưa diễn ra)
    // ==========================
    [HttpPut("{meetingId:guid}")]
    [Authorize]
    public async Task<IActionResult> UpdateMeeting(Guid meetingId, [FromBody] UpdateMeetingRequestDto request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue(ClaimTypes.Name);
        var username = User.FindFirstValue("username") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return BadRequest("Tiêu đề cuộc họp không được để trống");
        }

        var meeting = await _db.Meetings.FirstOrDefaultAsync(m => m.Id == meetingId);
        if (meeting == null) return NotFound("Meeting not found");

        if (!await MeetingHostAuth.IsAnyHostAsync(_db, meeting, userId ?? string.Empty, username))
        {
            return Unauthorized("Only meeting host can update this meeting");
        }

        if (meeting.EndedAt.HasValue)
        {
            return BadRequest("Cuộc họp đã kết thúc, không thể chỉnh sửa");
        }

        var activeCount = await _db.MeetingParticipants
            .Where(p => p.MeetingId == meetingId && p.LeftAt == null)
            .CountAsync();
        if (activeCount > 0)
        {
            return BadRequest("Cuộc họp đang diễn ra, không thể chỉnh sửa");
        }

        var startAtUtc = FromUnixMs(request.StartAt);
        DateTime? estimatedEndUtc = request.EstimatedEndAt.HasValue
            ? FromUnixMs(request.EstimatedEndAt.Value)
            : null;
        if (estimatedEndUtc.HasValue && estimatedEndUtc.Value <= startAtUtc)
        {
            return BadRequest("Thời gian kết thúc dự kiến phải sau thời gian bắt đầu");
        }

        meeting.Title = request.Title.Trim();
        // Hệ thống hiện dùng CreatedAt làm thời gian bắt đầu hiển thị.
        meeting.CreatedAt = startAtUtc;
        // Tái sử dụng StartedAt để lưu thời gian kết thúc dự kiến (không ảnh hưởng trạng thái cuộc họp hiện tại).
        meeting.StartedAt = estimatedEndUtc;

        await _db.SaveChangesAsync();

        var dto = MeetingMapper.ToMeetingListItemDto(meeting);
        dto.IsMeetingHost = true;
        dto.CanManagePoll = true;
        dto.ActiveParticipantCount = 0;
        return Ok(dto);
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

        if (!await MeetingHostAuth.IsAnyHostAsync(_db, meeting, userId ?? string.Empty, username))
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
    // DANH SÁCH MỜI THAM GIA
    // ==========================
    [HttpGet("{meetingId:guid}/invitees")]
    [Authorize]
    public async Task<IActionResult> ListInvitees(Guid meetingId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue(ClaimTypes.Name);
        var userRole = User.FindFirstValue(ClaimTypes.Role);
        var username = User.FindFirstValue("username");

        if (string.IsNullOrEmpty(userId))
            return Unauthorized("User identity not found");

        var meeting = await _db.Meetings.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == meetingId);
        if (meeting == null)
            return NotFound("Meeting not found");

        if (userRole != "Admin" && !await MeetingHostAuth.IsAnyHostAsync(_db, meeting, userId, username))
            return Unauthorized("Only meeting host or Admin can view invitees");

        var inviteeRows = await _db.MeetingInvitees
            .AsNoTracking()
            .Where(x => x.MeetingId == meetingId)
            .OrderBy(x => x.Username)
            .ToListAsync();

        if (inviteeRows.Count == 0)
            return Ok(new List<MeetingInviteeDto>());

        var namesToResolve = inviteeRows
            .Select(x => x.Username)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToLower())
            .Distinct()
            .ToList();

        var userMap = await _db.Users
            .AsNoTracking()
            .Where(u => namesToResolve.Contains(u.Username.ToLower()))
            .ToDictionaryAsync(
                u => u.Username.ToLower(),
                u => string.IsNullOrWhiteSpace(u.FullName) ? u.Username : u.FullName!);

        var list = inviteeRows.Select(row =>
        {
            var un = row.Username.Trim().ToLower();
            return new MeetingInviteeDto
            {
                Username = row.Username,
                FullName = userMap.TryGetValue(un, out var fn) ? fn : row.Username,
            };
        }).ToList();

        return Ok(list);
    }

    [HttpPost("{meetingId:guid}/invitees")]
    [Authorize]
    public async Task<IActionResult> AddInvitee(Guid meetingId, [FromBody] AddMeetingInviteeRequestDto request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue(ClaimTypes.Name);
        var userRole = User.FindFirstValue(ClaimTypes.Role);
        var username = User.FindFirstValue("username") ?? string.Empty;

        if (string.IsNullOrEmpty(userId))
            return Unauthorized("User identity not found");

        if (string.IsNullOrWhiteSpace(request.Username))
            return BadRequest("Username is required");

        var meeting = await _db.Meetings.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == meetingId);
        if (meeting == null)
            return NotFound("Meeting not found");

        if (userRole != "Admin" && !await MeetingHostAuth.IsAnyHostAsync(_db, meeting, userId, username))
            return Unauthorized("Only meeting host or Admin can add invitees");

        var targetUsername = request.Username.Trim();
        var target = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username.ToLower() == targetUsername.ToLower());
        if (target == null)
            return NotFound(new { message = "Không tìm thấy người dùng với username này" });

        if (MeetingHostAuth.IsPrimaryHost(meeting, target.Id.ToString(), target.Username))
            return BadRequest("Chủ trì không cần thêm vào danh sách mời");

        if (await MeetingHostAuth.IsCoHostAsync(_db, meetingId, target.Id.ToString()))
            return BadRequest("Người này đã là đồng chủ trì");

        var exists = await _db.MeetingInvitees
            .AnyAsync(x => x.MeetingId == meetingId && x.Username.ToLower() == target.Username.ToLower());
        if (exists)
            return Conflict(new { message = "Người dùng đã có trong danh sách mời" });

        _db.MeetingInvitees.Add(new MeetingInvitee
        {
            Id = Guid.NewGuid(),
            MeetingId = meetingId,
            Username = target.Username,
        });
        await _db.SaveChangesAsync();

        return Ok(new MeetingInviteeDto
        {
            Username = target.Username,
            FullName = string.IsNullOrWhiteSpace(target.FullName) ? target.Username : target.FullName!.Trim(),
        });
    }

    [HttpDelete("{meetingId:guid}/invitees/{username}")]
    [Authorize]
    public async Task<IActionResult> RemoveInvitee(Guid meetingId, string username)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue(ClaimTypes.Name);
        var userRole = User.FindFirstValue(ClaimTypes.Role);
        var actorUsername = User.FindFirstValue("username") ?? string.Empty;

        if (string.IsNullOrEmpty(userId))
            return Unauthorized("User identity not found");

        var meeting = await _db.Meetings.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == meetingId);
        if (meeting == null)
            return NotFound("Meeting not found");

        if (userRole != "Admin" && !await MeetingHostAuth.IsAnyHostAsync(_db, meeting, userId, actorUsername))
            return Unauthorized("Only meeting host or Admin can remove invitees");

        var target = Uri.UnescapeDataString(username ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(target))
            return BadRequest("Username is required");

        var row = await _db.MeetingInvitees
            .FirstOrDefaultAsync(x => x.MeetingId == meetingId && x.Username.ToLower() == target.ToLower());
        if (row == null)
            return NotFound("Invitee not found");

        _db.MeetingInvitees.Remove(row);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Removed" });
    }

    // ==========================
    // ĐỒNG CHỦ TRÌ (nâng từ danh sách mời)
    // ==========================
    [HttpGet("{meetingId:guid}/co-hosts")]
    [Authorize]
    public async Task<IActionResult> ListCoHosts(Guid meetingId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue(ClaimTypes.Name);
        var userRole = User.FindFirstValue(ClaimTypes.Role);
        var username = User.FindFirstValue("username");

        if (string.IsNullOrEmpty(userId))
            return Unauthorized("User identity not found");

        var meeting = await _db.Meetings.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == meetingId);
        if (meeting == null)
            return NotFound("Meeting not found");

        if (userRole != "Admin" && !await MeetingHostAuth.IsAnyHostAsync(_db, meeting, userId, username))
            return Unauthorized("Only meeting host or Admin can view co-hosts");

        var rows = await _db.MeetingCoHosts
            .AsNoTracking()
            .Where(x => x.MeetingId == meetingId)
            .OrderBy(x => x.Username)
            .ToListAsync();

        if (rows.Count == 0)
            return Ok(new List<MeetingCoHostDto>());

        var namesToResolve = rows
            .Select(x => x.Username)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToLower())
            .Distinct()
            .ToList();

        var userMap = await _db.Users
            .AsNoTracking()
            .Where(u => namesToResolve.Contains(u.Username.ToLower()))
            .ToDictionaryAsync(
                u => u.Username.ToLower(),
                u => string.IsNullOrWhiteSpace(u.FullName) ? u.Username : u.FullName!);

        var list = rows.Select(row =>
        {
            var un = row.Username.Trim().ToLower();
            return new MeetingCoHostDto
            {
                HostUserId = row.HostUserId,
                Username = row.Username,
                FullName = userMap.TryGetValue(un, out var fn) ? fn : row.Username,
            };
        }).ToList();

        return Ok(list);
    }

    [HttpPost("{meetingId:guid}/co-hosts/promote")]
    [Authorize]
    public async Task<IActionResult> PromoteInviteeToCoHost(Guid meetingId, [FromBody] PromoteInviteeToCoHostRequestDto request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue(ClaimTypes.Name);
        var userRole = User.FindFirstValue(ClaimTypes.Role);
        var username = User.FindFirstValue("username") ?? string.Empty;

        if (string.IsNullOrEmpty(userId))
            return Unauthorized("User identity not found");

        if (string.IsNullOrWhiteSpace(request.Username))
            return BadRequest("Username is required");

        var meeting = await _db.Meetings.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == meetingId);
        if (meeting == null)
            return NotFound("Meeting not found");

        if (userRole != "Admin" && !await MeetingHostAuth.IsAnyHostAsync(_db, meeting, userId, username))
            return Unauthorized("Only meeting host or Admin can promote invitees");

        var targetUsername = request.Username.Trim();
        var inviteeRow = await _db.MeetingInvitees
            .FirstOrDefaultAsync(x => x.MeetingId == meetingId && x.Username.ToLower() == targetUsername.ToLower());
        if (inviteeRow == null)
            return BadRequest(new { message = "Người này không nằm trong danh sách mời" });

        var target = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username.ToLower() == inviteeRow.Username.Trim().ToLower());
        if (target == null)
            return NotFound(new { message = "Không tìm thấy người dùng" });

        if (MeetingHostAuth.IsPrimaryHost(meeting, target.Id.ToString(), target.Username))
            return BadRequest("Người này đã là chủ trì");

        if (await MeetingHostAuth.IsCoHostAsync(_db, meetingId, target.Id.ToString()))
            return Conflict(new { message = "Người này đã là đồng chủ trì" });

        _db.MeetingCoHosts.Add(new MeetingCoHost
        {
            Id = Guid.NewGuid(),
            MeetingId = meetingId,
            HostUserId = target.Id.ToString(),
            Username = target.Username,
        });
        _db.MeetingInvitees.Remove(inviteeRow);
        await _db.SaveChangesAsync();

        return Ok(new MeetingCoHostDto
        {
            HostUserId = target.Id.ToString(),
            Username = target.Username,
            FullName = string.IsNullOrWhiteSpace(target.FullName) ? target.Username : target.FullName!.Trim(),
        });
    }

    [HttpPost("{meetingId:guid}/co-hosts/demote")]
    [Authorize]
    public async Task<IActionResult> DemoteCoHostToInvitee(Guid meetingId, [FromBody] DemoteCoHostToInviteeRequestDto request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue(ClaimTypes.Name);
        var userRole = User.FindFirstValue(ClaimTypes.Role);
        var username = User.FindFirstValue("username") ?? string.Empty;

        if (string.IsNullOrEmpty(userId))
            return Unauthorized("User identity not found");
        if (string.IsNullOrWhiteSpace(request.Username))
            return BadRequest("Username is required");

        var meeting = await _db.Meetings.AsNoTracking().FirstOrDefaultAsync(m => m.Id == meetingId);
        if (meeting == null)
            return NotFound("Meeting not found");
        if (userRole != "Admin" && !await MeetingHostAuth.IsAnyHostAsync(_db, meeting, userId, username))
            return Unauthorized("Only meeting host or Admin can change role");

        var targetUsername = request.Username.Trim();
        var cohost = await _db.MeetingCoHosts
            .FirstOrDefaultAsync(x => x.MeetingId == meetingId && x.Username.ToLower() == targetUsername.ToLower());
        if (cohost == null)
            return NotFound(new { message = "Không tìm thấy đồng chủ trì" });

        _db.MeetingCoHosts.Remove(cohost);
        var inviteeExists = await _db.MeetingInvitees
            .AnyAsync(x => x.MeetingId == meetingId && x.Username.ToLower() == cohost.Username.ToLower());
        if (!inviteeExists)
        {
            _db.MeetingInvitees.Add(new MeetingInvitee
            {
                Id = Guid.NewGuid(),
                MeetingId = meetingId,
                Username = cohost.Username,
            });
        }
        await _db.SaveChangesAsync();
        return Ok(new { message = "Role changed to member" });
    }

    [HttpDelete("{meetingId:guid}/co-hosts/{hostUserId}")]
    [Authorize]
    public async Task<IActionResult> RemoveCoHost(Guid meetingId, string hostUserId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue(ClaimTypes.Name);
        var userRole = User.FindFirstValue(ClaimTypes.Role);
        var actorUsername = User.FindFirstValue("username") ?? string.Empty;

        if (string.IsNullOrEmpty(userId))
            return Unauthorized("User identity not found");

        var meeting = await _db.Meetings.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == meetingId);
        if (meeting == null)
            return NotFound("Meeting not found");

        if (userRole != "Admin" && !await MeetingHostAuth.IsAnyHostAsync(_db, meeting, userId, actorUsername))
            return Unauthorized("Only meeting host or Admin can remove co-hosts");

        var decoded = Uri.UnescapeDataString(hostUserId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(decoded))
            return BadRequest("hostUserId is required");

        var row = await _db.MeetingCoHosts
            .FirstOrDefaultAsync(x => x.MeetingId == meetingId && x.HostUserId == decoded);
        if (row == null)
            return NotFound("Co-host not found");

        _db.MeetingCoHosts.Remove(row);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Removed" });
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
        var username = User.FindFirstValue("username");

        if (string.IsNullOrEmpty(userId))
            return Unauthorized("User identity not found");

        var meeting = await _db.Meetings
            .FirstOrDefaultAsync(m => m.Id == meetingId);

        if (meeting == null)
            return NotFound("Meeting not found");

        if (userRole != "Admin" && !await MeetingHostAuth.IsAnyHostAsync(_db, meeting, userId, username))
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
