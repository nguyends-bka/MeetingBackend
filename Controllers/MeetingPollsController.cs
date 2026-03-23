using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MeetingBackend.Data;
using MeetingBackend.DTOs.Meeting;
using MeetingBackend.Entities;
using MeetingBackend.Policies;
using Npgsql;

namespace MeetingBackend.Controllers;

[ApiController]
[Route("api/meeting/{meetingId:guid}/polls")]
[Authorize]
public class MeetingPollsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<MeetingPollsController> _logger;
    private readonly IWebHostEnvironment _env;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public MeetingPollsController(AppDbContext db, ILogger<MeetingPollsController> logger, IWebHostEnvironment env)
    {
        _db = db;
        _logger = logger;
        _env = env;
    }

    private static string UserId(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? user.FindFirstValue(ClaimTypes.Name)
        ?? string.Empty;

    private static long ToUnixMs(DateTime utc) =>
        new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeMilliseconds();

    private static DateTime FromUnixMs(long ms) =>
        DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;

    private async Task<Meeting?> GetMeetingAsync(Guid meetingId) =>
        await _db.Meetings.AsNoTracking().FirstOrDefaultAsync(m => m.Id == meetingId);

    private async Task<bool> IsHostOrAdminAsync(Guid meetingId, string userId, string? role)
    {
        if (role == "Admin") return true;
        var m = await _db.Meetings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == meetingId);
        return m != null && m.HostIdentity == userId;
    }

    private async Task<bool> CanViewPollsAsync(Guid meetingId, string userId, string? role)
    {
        if (await IsHostOrAdminAsync(meetingId, userId, role)) return true;
        return await _db.MeetingParticipants
            .AsNoTracking()
            .AnyAsync(p => p.MeetingId == meetingId && p.UserId == userId);
    }

    /// <summary>
    /// Danh sách biểu quyết + phiếu (host/admin hoặc người đã tham gia meeting).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List(Guid meetingId)
    {
        var userId = UserId(User);
        var role = User.FindFirstValue(ClaimTypes.Role);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        if (!await CanViewPollsAsync(meetingId, userId, role))
            return Unauthorized("Only meeting participants, host, or Admin can list polls");

        var polls = await _db.MeetingPolls
            .AsNoTracking()
            .Where(p => p.MeetingId == meetingId)
            .OrderBy(p => p.CreatedAtUtc)
            .Include(p => p.Votes)
            .ToListAsync();

        var dtos = polls.Select(ToDto).ToList();
        return Ok(dtos);
    }

    /// <summary>
    /// Tạo phiếu (chỉ host meeting). CreatedBy phải trùng user JWT.
    /// PollId tùy chọn — bỏ qua hoặc null thì server tự sinh GUID.
    /// </summary>
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create(Guid meetingId, [FromBody] PollCreateRequestDto dto)
    {
        var userId = UserId(User);
        var username = User.FindFirstValue("username") ?? string.Empty;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var meeting = await _db.Meetings.FirstOrDefaultAsync(m => m.Id == meetingId);
        if (meeting == null)
            return NotFound("Meeting not found");

        var hostIdentity = meeting.HostIdentity?.Trim() ?? string.Empty;
        var isHostById = string.Equals(hostIdentity, userId.Trim(), StringComparison.OrdinalIgnoreCase);
        var isHostByUsername = !string.IsNullOrWhiteSpace(username)
            && string.Equals(hostIdentity, username.Trim(), StringComparison.OrdinalIgnoreCase);
        if (!isHostById && !isHostByUsername)
            return Unauthorized("Only meeting host can create polls");

        if (string.IsNullOrWhiteSpace(dto.Title))
            return BadRequest("Title is required");

        var options = dto.Options?.Where(o => !string.IsNullOrWhiteSpace(o)).Select(o => o.Trim()).ToArray() ?? Array.Empty<string>();
        if (options.Length < 2 || options.Length > 8)
            return BadRequest("Options must have between 2 and 8 items");

        if (dto.CreatedBy != userId)
            return BadRequest("CreatedBy must match authenticated user");

        string pollId;
        if (string.IsNullOrWhiteSpace(dto.PollId))
        {
            do
            {
                pollId = Guid.NewGuid().ToString();
            } while (await _db.MeetingPolls.AnyAsync(p => p.MeetingId == meetingId && p.PollId == pollId));
        }
        else
        {
            pollId = dto.PollId.Trim();
            if (await _db.MeetingPolls.AnyAsync(p => p.MeetingId == meetingId && p.PollId == pollId))
                return Conflict("Poll already exists");
        }

        var mode = dto.SelectionMode == "multiple" ? "multiple" : "single";
        var createdAt = dto.CreatedAt > 0 ? FromUnixMs(dto.CreatedAt) : DateTime.UtcNow;

        var poll = new MeetingPoll
        {
            Id = Guid.NewGuid(),
            MeetingId = meetingId,
            PollId = pollId,
            Title = dto.Title.Trim(),
            OptionsJson = JsonSerializer.Serialize(options, JsonOpts),
            CreatedBy = dto.CreatedBy,
            CreatedByName = string.IsNullOrWhiteSpace(dto.CreatedByName) ? dto.CreatedBy : dto.CreatedByName.Trim(),
            CreatedAtUtc = createdAt,
            SelectionMode = mode,
            EndAtUtc = dto.EndAt.HasValue && dto.EndAt.Value > 0 ? FromUnixMs(dto.EndAt.Value) : null,
            Status = "open",
        };

        _db.MeetingPolls.Add(poll);
        await _db.SaveChangesAsync();

        return Ok(ToDto(poll));
    }

    /// <summary>
    /// Ghi nhận phiếu bầu. VoterIdentity phải trùng user JWT.
    /// </summary>
    [HttpPost("{pollId}/vote")]
    public async Task<IActionResult> Vote(Guid meetingId, string pollId, [FromBody] PollVoteRequestDto? dto)
    {
        if (dto is null)
            return BadRequest("Request body is required");

        var userId = UserId(User);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        if (dto.VoterIdentity != userId)
            return BadRequest("VoterIdentity must match authenticated user");

        var poll = await _db.MeetingPolls
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.MeetingId == meetingId && p.PollId == pollId);
        if (poll == null)
            return NotFound("Poll not found");

        if (poll.Status != "open")
            return BadRequest("Poll is closed");

        var now = dto.At > 0 ? FromUnixMs(dto.At) : DateTime.UtcNow;
        if (poll.EndAtUtc.HasValue && now > poll.EndAtUtc.Value)
            return BadRequest("Poll has ended");

        string[] options;
        try
        {
            options = JsonSerializer.Deserialize<string[]>(poll.OptionsJson, JsonOpts) ?? Array.Empty<string>();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid OptionsJson for poll {PollId}", pollId);
            return BadRequest("Poll options data is invalid");
        }

        var mode = poll.SelectionMode == "multiple" ? "multiple" : "single";
        var indices = NormalizeIndices(dto.OptionIndices, options.Length, mode);
        if (indices == null)
            return BadRequest("Invalid option indices");

        var tracked = await _db.MeetingPolls
            .FirstOrDefaultAsync(p => p.Id == poll.Id);
        if (tracked == null)
            return NotFound();

        var indicesJson = JsonSerializer.Serialize(indices, JsonOpts);
        var existing = await _db.MeetingPollVotes
            .FirstOrDefaultAsync(v => v.MeetingPollId == tracked.Id && v.VoterIdentity == userId);
        if (existing != null)
        {
            existing.OptionIndicesJson = indicesJson;
            existing.VoterName = string.IsNullOrWhiteSpace(dto.VoterName) ? userId : dto.VoterName.Trim();
            existing.VotedAtUtc = now;
        }
        else
        {
            _db.MeetingPollVotes.Add(new MeetingPollVote
            {
                Id = Guid.NewGuid(),
                MeetingPollId = tracked.Id,
                VoterIdentity = userId,
                VoterName = string.IsNullOrWhiteSpace(dto.VoterName) ? userId : dto.VoterName.Trim(),
                OptionIndicesJson = indicesJson,
                VotedAtUtc = now,
            });
        }

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Vote concurrency conflict meeting={MeetingId} poll={PollId} user={UserId}", meetingId, pollId, userId);
            return Conflict(new
            {
                error = "Vote was updated concurrently. Please retry.",
            });
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Vote SaveChanges failed meeting={MeetingId} poll={PollId}", meetingId, pollId);
            if (ex.InnerException is PostgresException pg && pg.SqlState == PostgresErrorCodes.UndefinedTable)
                return StatusCode(500, new
                {
                    error = "Database table missing (e.g. MeetingPollVotes). Run: dotnet ef database update",
                    sqlState = _env.IsDevelopment() ? pg.SqlState : null,
                });
            return StatusCode(500, new
            {
                error = "Could not save vote.",
                detail = _env.IsDevelopment() ? ex.InnerException?.Message ?? ex.Message : null,
            });
        }

        return Ok(new { ok = true });
    }

    /// <summary>
    /// Đóng phiếu — chỉ người tạo phiếu (CreatedBy) hoặc admin.
    /// </summary>
    [HttpPost("{pollId}/close")]
    public async Task<IActionResult> Close(Guid meetingId, string pollId, [FromBody] PollCloseRequestDto dto)
    {
        var userId = UserId(User);
        var role = User.FindFirstValue(ClaimTypes.Role);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        if (dto.ClosedBy != userId)
            return BadRequest("ClosedBy must match authenticated user");

        var poll = await _db.MeetingPolls
            .FirstOrDefaultAsync(p => p.MeetingId == meetingId && p.PollId == pollId);
        if (poll == null)
            return NotFound("Poll not found");

        if (poll.Status == "closed")
            return Ok(ToDto(poll));

        var canClose = poll.CreatedBy == userId || role == "Admin";
        if (!canClose)
            return Unauthorized("Only poll creator or Admin can close");

        poll.Status = "closed";
        poll.ClosedBy = dto.ClosedBy;
        poll.ClosedAtUtc = dto.At > 0 ? FromUnixMs(dto.At) : DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(ToDto(poll));
    }

    private static int[]? NormalizeIndices(int[] raw, int optionCount, string mode)
    {
        if (raw == null || raw.Length == 0 || optionCount <= 0)
            return null;

        var uniq = raw
            .Where(i => i >= 0 && i < optionCount)
            .Distinct()
            .OrderBy(i => i)
            .ToArray();

        if (uniq.Length == 0)
            return null;

        if (mode == "single")
            return uniq.Length == 1 ? uniq : null;

        return uniq;
    }

    private static PollResponseDto ToDto(MeetingPoll p)
    {
        var options = JsonSerializer.Deserialize<string[]>(p.OptionsJson, JsonOpts) ?? Array.Empty<string>();
        var voteRows = p.Votes != null
            ? p.Votes.OrderBy(v => v.VotedAtUtc).ToList()
            : new List<MeetingPollVote>();
        var votes = voteRows
            .Select(v =>
            {
                var idx = JsonSerializer.Deserialize<int[]>(v.OptionIndicesJson, JsonOpts) ?? Array.Empty<int>();
                return new PollVoteEntryDto
                {
                    VoterIdentity = v.VoterIdentity,
                    VoterName = v.VoterName,
                    OptionIndices = idx,
                    At = ToUnixMs(v.VotedAtUtc),
                };
            })
            .ToList();

        return new PollResponseDto
        {
            PollId = p.PollId,
            Title = p.Title,
            Options = options,
            CreatedBy = p.CreatedBy,
            CreatedByName = p.CreatedByName,
            CreatedAt = ToUnixMs(p.CreatedAtUtc),
            SelectionMode = p.SelectionMode,
            EndAt = p.EndAtUtc.HasValue ? ToUnixMs(p.EndAtUtc.Value) : null,
            Status = p.Status,
            ClosedAt = p.ClosedAtUtc.HasValue ? ToUnixMs(p.ClosedAtUtc.Value) : null,
            ClosedBy = p.ClosedBy,
            Votes = votes,
        };
    }
}
