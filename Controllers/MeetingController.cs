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
    // USER JOIN MEETING BY ID
    // ==========================
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
            meetingCode = meeting.MeetingCode
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
            meetingCode = meeting.MeetingCode
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
            title = meeting.Title
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
}
