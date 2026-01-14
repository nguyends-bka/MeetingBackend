using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MeetingBackend.Data;
using MeetingBackend.Entities;
using MeetingBackend.Models;
using MeetingBackend.Services;

namespace MeetingBackend.Controllers;

[ApiController]
[Route("api/meeting")]
public class MeetingController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly LiveKitTokenService _tokenService;
    private readonly IConfiguration _config;

    public MeetingController(
        AppDbContext db,
        LiveKitTokenService tokenService,
        IConfiguration config)
    {
        _db = db;
        _tokenService = tokenService;
        _config = config;
    }

    // ✅ CREATE MEETING
    [HttpPost("create")]
    public async Task<IActionResult> Create([FromBody] CreateMeetingRequest req)
    {
        var meeting = new Meeting
        {
            RoomName = $"room-{Guid.NewGuid()}",
            HostIdentity = req.HostIdentity
        };

        _db.Meetings.Add(meeting);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            meetingId = meeting.Id,
            roomName = meeting.RoomName
        });
    }

    // ✅ JOIN MEETING
    [HttpPost("join")]
    public async Task<IActionResult> Join([FromBody] JoinMeetingRequest req)
    {
        var meeting = await _db.Meetings
            .FirstOrDefaultAsync(x => x.Id == req.MeetingId);

        if (meeting == null)
            return NotFound("Meeting not found");

        var token = _tokenService.CreateToken(
            meeting.RoomName,
            req.Identity
        );

        return Ok(new
        {
            token,
            liveKitUrl = _config["LiveKit:Url"],
            roomName = meeting.RoomName
        });
    }
}
