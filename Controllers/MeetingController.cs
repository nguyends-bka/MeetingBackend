using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using MeetingBackend.Data;
using MeetingBackend.Entities;
using MeetingBackend.Models;
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
        var participant = new MeetingParticipant
        {
            Id = Guid.NewGuid(),
            MeetingId = meetingId,
            UserId = userId,
            Username = username,
            JoinedAt = DateTime.UtcNow
        };
        _db.MeetingParticipants.Add(participant);
        await _db.SaveChangesAsync();
        return participant;
    }

    // ==========================
    // ADMIN TẠO MEETING
    // ==========================
    [HttpPost("create")]
    [Authorize]
    public async Task<IActionResult> Create(CreateMeetingRequest request)
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

        return Ok(new
        {
            meetingId = meeting.Id,
            meetingCode = meeting.MeetingCode,
            passcode = meeting.Passcode,
            roomName = meeting.RoomName
        });
    }

    // ==========================
    // JOIN MEETING BY LINK (KHÔNG CẦN PASSCODE)
    // ==========================
    [HttpPost("join-by-link")]
    public async Task<IActionResult> JoinByLink([FromBody] JoinByLinkRequest req)
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
        var participant = await RecordJoinAsync(meeting.Id, userId!, username);

        var token = _liveKit.CreateToken(
            meeting.RoomName,
            userId!
        );

        return Ok(new
        {
            token,
            liveKitUrl = _config["LiveKit:Url"],
            roomName = meeting.RoomName,
            meetingId = meeting.Id,
            meetingCode = meeting.MeetingCode,
            participantId = participant.Id
        });
    }

    // ==========================
    // JOIN MEETING BY ID/CODE + PASSCODE
    // ==========================
    [HttpPost("join")]
    public async Task<IActionResult> Join(JoinMeetingRequest req)
    {
        Meeting? meeting = null;

        // Nếu có MeetingId (Guid), tìm bằng ID
        if (req.MeetingId != Guid.Empty)
        {
            meeting = await _db.Meetings
                .FirstOrDefaultAsync(m => m.Id == req.MeetingId);
        }
        // Nếu không có MeetingId nhưng có MeetingCode, tìm bằng code
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
        var participant = await RecordJoinAsync(meeting.Id, userId!, username);

        var token = _liveKit.CreateToken(
            meeting.RoomName,
            userId!
        );

        return Ok(new
        {
            token,
            liveKitUrl = _config["LiveKit:Url"],
            roomName = meeting.RoomName,
            meetingId = meeting.Id,
            meetingCode = meeting.MeetingCode,
            participantId = participant.Id
        });
    }

    // ==========================
    // USER JOIN MEETING BY CODE
    // ==========================
    [HttpPost("join-by-code")]
    public async Task<IActionResult> JoinByCode([FromBody] JoinByCodeRequest req)
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
        var participant = await RecordJoinAsync(meeting.Id, userId!, username);

        var token = _liveKit.CreateToken(
            meeting.RoomName,
            userId!
        );

        return Ok(new
        {
            token,
            liveKitUrl = _config["LiveKit:Url"],
            roomName = meeting.RoomName,
            meetingId = meeting.Id,
            meetingCode = meeting.MeetingCode,
            title = meeting.Title,
            participantId = participant.Id
        });
    }

    // ==========================
    // LẤY DANH SÁCH MEETING CỦA USER HIỆN TẠI
    // ==========================
    [HttpGet]
    public async Task<IActionResult> GetMeetings()
    {
        // Lấy userId từ JWT token
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue(ClaimTypes.Name);

        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized("User identity not found");
        }

        // Chỉ lấy các meeting do user hiện tại tạo
        var meetings = await _db.Meetings
            .Where(m => m.HostIdentity == userId)
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => new
            {
                m.Id,
                m.Title,
                m.HostName,
                m.MeetingCode,
                m.Passcode,
                m.CreatedAt
            })
            .ToListAsync();

        return Ok(meetings);
    }

    // ==========================
    // GHI LẠI KHI USER LEAVE MEETING
    // ==========================
    [HttpPost("leave")]
    [Authorize]
    public async Task<IActionResult> Leave([FromBody] LeaveMeetingRequest req)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue(ClaimTypes.Name);

        // Tìm participant record chưa có LeftAt
        var participant = await _db.MeetingParticipants
            .FirstOrDefaultAsync(p => 
                p.Id == req.ParticipantId && 
                p.UserId == userId && 
                p.LeftAt == null);

        if (participant != null)
        {
            participant.LeftAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        return Ok(new { message = "Left meeting successfully" });
    }

    // ==========================
    // XEM LỊCH SỬ VÀO/RA CỦA MEETING
    // ==========================
    [HttpGet("{meetingId}/history")]
    [Authorize]
    public async Task<IActionResult> GetMeetingHistory(Guid meetingId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue(ClaimTypes.Name);

        // Kiểm tra user có phải host không
        var meeting = await _db.Meetings
            .FirstOrDefaultAsync(m => m.Id == meetingId);

        if (meeting == null)
            return NotFound("Meeting not found");

        // Chỉ host mới xem được lịch sử
        if (meeting.HostIdentity != userId)
            return Unauthorized("Only meeting host can view history");

        var history = await _db.MeetingParticipants
            .Where(p => p.MeetingId == meetingId)
            .OrderByDescending(p => p.JoinedAt)
            .Select(p => new
            {
                p.Id,
                p.Username,
                p.UserId,
                p.JoinedAt,
                p.LeftAt,
                Duration = p.LeftAt.HasValue 
                    ? (p.LeftAt.Value - p.JoinedAt).TotalMinutes 
                    : (double?)null
            })
            .ToListAsync();

        return Ok(history);
    }
}
