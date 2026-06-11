using System.Security.Claims;
using MeetingBackend.Data;
using MeetingBackend.DTOs.Meeting;
using MeetingBackend.Entities;
using MeetingBackend.Services;
using MeetingBackend.Services.Integrations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MeetingBackend.Controllers;

[ApiController]
[Route("api/meeting/{meetingId:guid}/room-log")]
[Authorize]
public class MeetingRoomLogController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IServiceScopeFactory _scopeFactory;

    public MeetingRoomLogController(AppDbContext db, IServiceScopeFactory scopeFactory)
    {
        _db = db;
        _scopeFactory = scopeFactory;
    }

    private static string UserId(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? user.FindFirstValue(ClaimTypes.Name)
        ?? string.Empty;

    private async Task<bool> CanAccessMeetingAsync(Guid meetingId, string userId, string? username, string? role)
    {
        if (role == "Admin") return true;
        var meeting = await _db.Meetings.AsNoTracking().FirstOrDefaultAsync(m => m.Id == meetingId);
        if (meeting == null) return false;
        if (await MeetingHostAuth.IsAnyHostAsync(_db, meeting, userId, username)) return true;
        return await _db.MeetingParticipants.AsNoTracking()
            .AnyAsync(p => p.MeetingId == meetingId && p.UserId == userId);
    }

    private static DateTime FromUnixMs(long ms) =>
        DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;

    private static long ToUnixMs(DateTime utc) =>
        new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeMilliseconds();

    [HttpGet]
    public async Task<IActionResult> Get(Guid meetingId)
    {
        var userId = UserId(User);
        var username = User.FindFirstValue("username");
        var role = User.FindFirstValue(ClaimTypes.Role);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        if (!await CanAccessMeetingAsync(meetingId, userId, username, role))
            return StatusCode(StatusCodes.Status403Forbidden, "Only meeting participants, host, or Admin can view room logs");

        var chatsRaw = await _db.MeetingChatMessages.AsNoTracking()
            .Where(x => x.MeetingId == meetingId)
            .OrderBy(x => x.SentAtUtc)
            .ToListAsync();

        var transcriptRaw = await _db.MeetingTranscriptEntries.AsNoTracking()
            .Where(x => x.MeetingId == meetingId)
            .OrderBy(x => x.AtUtc)
            .ToListAsync();

        var ids = chatsRaw.Select(x => x.SenderIdentity)
            .Concat(transcriptRaw.Select(x => x.SpeakerIdentity))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var guidIds = ids
            .Select(x => Guid.TryParse(x, out var g) ? g : Guid.Empty)
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToList();

        var userLookup = await _db.Users.AsNoTracking()
            .Where(u => guidIds.Contains(u.Id))
            .Select(u => new { Id = u.Id.ToString(), Name = string.IsNullOrWhiteSpace(u.FullName) ? u.Username : u.FullName })
            .ToDictionaryAsync(x => x.Id, x => x.Name, StringComparer.OrdinalIgnoreCase);

        string ResolveName(string identity)
        {
            if (string.IsNullOrWhiteSpace(identity)) return string.Empty;
            var key = identity.Trim();
            return userLookup.TryGetValue(key, out var name) ? name : key;
        }

        var chats = chatsRaw.Select(x => new RoomChatItemDto
        {
            ClientMessageId = x.ClientMessageId,
            SenderIdentity = x.SenderIdentity,
            SenderName = ResolveName(x.SenderIdentity),
            Message = x.Message,
            At = ToUnixMs(x.SentAtUtc),
        }).ToList();

        var transcript = transcriptRaw.Select(x => new RoomTranscriptItemDto
            {
                SpeakerIdentity = x.SpeakerIdentity,
                SpeakerName = ResolveName(x.SpeakerIdentity),
                Text = x.Text,
                At = ToUnixMs(x.AtUtc),
            }).ToList();

        return Ok(new RoomLogResponseDto
        {
            ChatMessages = chats,
            TranscriptEntries = transcript,
        });
    }

    [HttpPost("chat")]
    public async Task<IActionResult> AddChat(Guid meetingId, [FromBody] RoomChatCreateRequestDto dto)
    {
        var userId = UserId(User);
        var username = User.FindFirstValue("username");
        var role = User.FindFirstValue(ClaimTypes.Role);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        if (!await CanAccessMeetingAsync(meetingId, userId, username, role))
            return StatusCode(StatusCodes.Status403Forbidden, "Only meeting participants can add chat messages");

        var senderIdentity = dto.SenderIdentity?.Trim() ?? string.Empty;
        var message = dto.Message?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(senderIdentity) || string.IsNullOrWhiteSpace(message))
            return BadRequest("SenderIdentity and message are required");

        if (!string.Equals(senderIdentity, userId, StringComparison.OrdinalIgnoreCase))
            return BadRequest("SenderIdentity must match authenticated user");

        if (!string.IsNullOrWhiteSpace(dto.ClientMessageId))
        {
            var existed = await _db.MeetingChatMessages.AsNoTracking().AnyAsync(x =>
                x.MeetingId == meetingId && x.ClientMessageId == dto.ClientMessageId);
            if (existed) return Ok(new { ok = true, deduplicated = true });
        }

        var at = dto.At > 0 ? FromUnixMs(dto.At) : DateTime.UtcNow;
        _db.MeetingChatMessages.Add(new MeetingChatMessage
        {
            Id = Guid.NewGuid(),
            MeetingId = meetingId,
            ClientMessageId = string.IsNullOrWhiteSpace(dto.ClientMessageId) ? null : dto.ClientMessageId.Trim(),
            SenderIdentity = senderIdentity,
            Message = message,
            SentAtUtc = at,
            CreatedAtUtc = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    [HttpPost("transcript")]
    public async Task<IActionResult> AddTranscript(Guid meetingId, [FromBody] RoomTranscriptCreateRequestDto dto)
    {
        var userId = UserId(User);
        var username = User.FindFirstValue("username");
        var role = User.FindFirstValue(ClaimTypes.Role);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        if (!await CanAccessMeetingAsync(meetingId, userId, username, role))
            return StatusCode(StatusCodes.Status403Forbidden, "Only meeting participants can add transcript entries");

        var text = dto.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text)) return BadRequest("Text is required");

        var speakerIdentity = dto.SpeakerIdentity?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(speakerIdentity))
            return BadRequest("SpeakerIdentity is required");
        if (!string.Equals(speakerIdentity, userId, StringComparison.OrdinalIgnoreCase))
            return BadRequest("SpeakerIdentity must match authenticated user");

        var at = dto.At > 0 ? FromUnixMs(dto.At) : DateTime.UtcNow;
        _db.MeetingTranscriptEntries.Add(new MeetingTranscriptEntry
        {
            Id = Guid.NewGuid(),
            MeetingId = meetingId,
            SpeakerIdentity = speakerIdentity,
            Text = text,
            AtUtc = at,
            CreatedAtUtc = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        // Resolve tên người nói rồi gửi lên RAG (fire-and-forget)
        // Tạo scope mới để tránh ObjectDisposedException (DbContext/scoped services bị dispose sau khi request kết thúc)
        _ = Task.Run(async () =>
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var ragClient = scope.ServiceProvider.GetRequiredService<RagTranscriptClient>();

                string speakerName = speakerIdentity;
                if (Guid.TryParse(speakerIdentity, out var speakerGuid))
                {
                    var user = await db.Users.AsNoTracking()
                        .Where(u => u.Id == speakerGuid)
                        .Select(u => new { Name = string.IsNullOrWhiteSpace(u.FullName) ? u.Username : u.FullName })
                        .FirstOrDefaultAsync();
                    if (user != null) speakerName = user.Name;
                }

                await ragClient.SendAsync(meetingId, speakerName, at, text);
            }
            catch
            {
                // Lỗi RAG không được ảnh hưởng response chính
            }
        });

        return Ok(new { ok = true });
    }
}
