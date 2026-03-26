using System.Security.Claims;
using System.Text.Json;
using MeetingBackend.Data;
using MeetingBackend.DTOs.Meeting;
using MeetingBackend.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MeetingBackend.Controllers;

[ApiController]
[Route("api/meeting/{meetingId:guid}/minutes")]
[Authorize]
public class MeetingMinutesController : ControllerBase
{
    private readonly AppDbContext _db;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public MeetingMinutesController(AppDbContext db)
    {
        _db = db;
    }

    private static string UserId(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? user.FindFirstValue(ClaimTypes.Name)
        ?? string.Empty;

    private static long ToUnixMs(DateTime utc) =>
        new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeMilliseconds();

    private static bool IsHostIdentityMatch(string hostIdentity, string userId, string? username)
    {
        if (string.Equals(hostIdentity, userId, StringComparison.OrdinalIgnoreCase)) return true;
        if (!string.IsNullOrWhiteSpace(username)
            && string.Equals(hostIdentity, username.Trim(), StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private async Task<bool> CanAccessMeetingAsync(Guid meetingId, string userId, string? username, string? role)
    {
        if (role == "Admin") return true;
        var meeting = await _db.Meetings.AsNoTracking().FirstOrDefaultAsync(m => m.Id == meetingId);
        if (meeting == null) return false;
        if (IsHostIdentityMatch(meeting.HostIdentity?.Trim() ?? string.Empty, userId, username)) return true;
        return await _db.MeetingParticipants.AsNoTracking()
            .AnyAsync(p => p.MeetingId == meetingId && p.UserId == userId);
    }

    [HttpGet]
    public async Task<IActionResult> Get(Guid meetingId)
    {
        var userId = UserId(User);
        var username = User.FindFirstValue("username");
        var role = User.FindFirstValue(ClaimTypes.Role);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        if (!await CanAccessMeetingAsync(meetingId, userId, username, role))
            return Unauthorized("Only meeting participants, host, or Admin can view minutes");

        var meeting = await _db.Meetings.AsNoTracking().FirstOrDefaultAsync(m => m.Id == meetingId);
        if (meeting == null)
            return NotFound("Meeting not found");

        // Chủ trì: ưu tiên hiển thị fullName từ bảng Users nếu tra được.
        var hostDisplayName = meeting.HostName;
        var hostIdentity = meeting.HostIdentity?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(hostIdentity))
        {
            if (Guid.TryParse(hostIdentity, out var hostGuid))
            {
                var hostUser = await _db.Users.AsNoTracking()
                    .Where(u => u.Id == hostGuid)
                    .Select(u => new { u.FullName, u.Username })
                    .FirstOrDefaultAsync();
                if (hostUser != null)
                {
                    hostDisplayName = string.IsNullOrWhiteSpace(hostUser.FullName)
                        ? hostUser.Username
                        : hostUser.FullName!;
                }
            }
            else
            {
                var hostUser = await _db.Users.AsNoTracking()
                    .Where(u => u.Username.ToLower() == hostIdentity.ToLower())
                    .Select(u => new { u.FullName, u.Username })
                    .FirstOrDefaultAsync();
                if (hostUser != null)
                {
                    hostDisplayName = string.IsNullOrWhiteSpace(hostUser.FullName)
                        ? hostUser.Username
                        : hostUser.FullName!;
                }
            }
        }

        var participantsRaw = await _db.MeetingParticipants.AsNoTracking()
            .Where(p => p.MeetingId == meetingId)
            .OrderBy(p => p.JoinedAt)
            .ToListAsync();

        var userIds = participantsRaw
            .Select(p => p.UserId.Trim())
            .Where(x => !string.IsNullOrEmpty(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var guidIds = userIds
            .Select(x => Guid.TryParse(x, out var g) ? g : Guid.Empty)
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToList();

        var userLookup = await _db.Users.AsNoTracking()
            .Where(u => guidIds.Contains(u.Id))
            .Select(u => new { Id = u.Id.ToString(), Name = string.IsNullOrWhiteSpace(u.FullName) ? u.Username : u.FullName })
            .ToDictionaryAsync(x => x.Id, x => x.Name, StringComparer.OrdinalIgnoreCase);

        string ResolveParticipantName(string uid, string fallbackUsername)
        {
            var key = uid.Trim();
            if (userLookup.TryGetValue(key, out var name)) return name;
            return string.IsNullOrWhiteSpace(fallbackUsername) ? key : fallbackUsername;
        }

        var distinctByUser = new Dictionary<string, MeetingMinutesParticipantDto>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in participantsRaw)
        {
            var uid = p.UserId.Trim();
            if (string.IsNullOrEmpty(uid)) continue;
            if (distinctByUser.ContainsKey(uid)) continue;
            distinctByUser[uid] = new MeetingMinutesParticipantDto
            {
                UserId = uid,
                DisplayName = ResolveParticipantName(uid, p.Username),
            };
        }

        var participantList = distinctByUser.Values.OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();

        var transcriptRaw = await _db.MeetingTranscriptEntries.AsNoTracking()
            .Where(x => x.MeetingId == meetingId)
            .OrderBy(x => x.AtUtc)
            .ToListAsync();

        var transcriptIds = transcriptRaw
            .Select(x => x.SpeakerIdentity)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var transcriptGuidIds = transcriptIds
            .Select(x => Guid.TryParse(x, out var g) ? g : Guid.Empty)
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToList();

        var transcriptUserLookup = await _db.Users.AsNoTracking()
            .Where(u => transcriptGuidIds.Contains(u.Id))
            .Select(u => new { Id = u.Id.ToString(), Name = string.IsNullOrWhiteSpace(u.FullName) ? u.Username : u.FullName })
            .ToDictionaryAsync(x => x.Id, x => x.Name, StringComparer.OrdinalIgnoreCase);

        var transcript = transcriptRaw.Select(x =>
        {
            var sp = x.SpeakerIdentity.Trim();
            var name = transcriptUserLookup.TryGetValue(sp, out var n) ? n : sp;
            return new MeetingMinutesTranscriptLineDto
            {
                SpeakerName = name,
                Text = x.Text,
                At = ToUnixMs(x.AtUtc),
            };
        }).ToList();

        var polls = await _db.MeetingPolls.AsNoTracking()
            .Where(p => p.MeetingId == meetingId && p.Status != "draft")
            .Include(p => p.Votes)
            .OrderBy(p => p.CreatedAtUtc)
            .ToListAsync();

        var pollDtos = new List<MeetingMinutesPollDto>();
        foreach (var poll in polls)
        {
            string[] options;
            try
            {
                options = JsonSerializer.Deserialize<string[]>(poll.OptionsJson, JsonOpts) ?? Array.Empty<string>();
            }
            catch
            {
                options = Array.Empty<string>();
            }

            var counts = new Dictionary<int, int>();
            for (var i = 0; i < options.Length; i++) counts[i] = 0;

            foreach (var v in poll.Votes ?? Array.Empty<MeetingPollVote>())
            {
                int[] idx;
                try
                {
                    idx = JsonSerializer.Deserialize<int[]>(v.OptionIndicesJson, JsonOpts) ?? Array.Empty<int>();
                }
                catch
                {
                    continue;
                }

                foreach (var i in idx)
                {
                    if (i >= 0 && i < options.Length)
                        counts[i] = counts.GetValueOrDefault(i, 0) + 1;
                }
            }

            pollDtos.Add(new MeetingMinutesPollDto
            {
                PollId = poll.PollId,
                Title = poll.Title,
                Status = poll.Status,
                Options = options,
                OptionVoteCounts = counts,
            });
        }

        var leftTimes = participantsRaw
            .Where(p => p.LeftAt.HasValue)
            .Select(p => p.LeftAt!.Value)
            .ToList();
        var endedAtEstimated = leftTimes.Count > 0 ? ToUnixMs(leftTimes.Max()) : (long?)null;

        return Ok(new MeetingMinutesDto
        {
            MeetingId = meeting.Id,
            Title = meeting.Title,
            HostName = hostDisplayName,
            HostIdentity = meeting.HostIdentity ?? string.Empty,
            LocationDetail = $"Mã: {meeting.MeetingCode}",
            StartedAt = ToUnixMs(meeting.CreatedAt),
            EndedAtEstimated = endedAtEstimated,
            ParticipantCount = participantList.Count,
            Participants = participantList,
            Transcript = transcript,
            Polls = pollDtos,
        });
    }
}
