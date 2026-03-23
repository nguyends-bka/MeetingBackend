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

    private async Task<bool> IsHostOrAdminAsync(Guid meetingId, string userId, string? role, string? username)
    {
        if (role == "Admin") return true;
        var m = await _db.Meetings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == meetingId);
        if (m == null) return false;
        return IsHostIdentityMatch(m.HostIdentity?.Trim() ?? string.Empty, userId, username);
    }

    private async Task<bool> IsPollManagerAsync(Guid meetingId, string? username)
    {
        if (string.IsNullOrWhiteSpace(username)) return false;
        var normalized = username.Trim().ToLower();
        return await _db.MeetingPollManagers
            .AsNoTracking()
            .AnyAsync(x => x.MeetingId == meetingId && x.Username.ToLower() == normalized);
    }

    private async Task<bool> CanManagePollsAsync(Guid meetingId, string userId, string? username)
    {
        var meeting = await _db.Meetings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == meetingId);
        if (meeting == null) return false;
        if (IsHostIdentityMatch(meeting.HostIdentity?.Trim() ?? string.Empty, userId, username)) return true;
        return await IsPollManagerAsync(meetingId, username);
    }

    private static bool IsHostIdentityMatch(string hostIdentity, string userId, string? username)
    {
        if (string.Equals(hostIdentity, userId, StringComparison.OrdinalIgnoreCase)) return true;
        if (!string.IsNullOrWhiteSpace(username)
            && string.Equals(hostIdentity, username.Trim(), StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private async Task<bool> CanViewPollsAsync(Guid meetingId, string userId, string? role, string? username)
    {
        if (await IsHostOrAdminAsync(meetingId, userId, role, username)) return true;
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
        var username = User.FindFirstValue("username");
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        if (!await CanViewPollsAsync(meetingId, userId, role, username))
            return Unauthorized("Only meeting participants, host, or Admin can list polls");

        var meeting = await _db.Meetings.AsNoTracking().FirstOrDefaultAsync(m => m.Id == meetingId);
        if (meeting == null)
            return NotFound("Meeting not found");
        var isHostOrAdmin = role == "Admin" || IsHostIdentityMatch(meeting.HostIdentity?.Trim() ?? string.Empty, userId, username);
        var isPollManager = await IsPollManagerAsync(meetingId, username);

        IQueryable<MeetingPoll> pollsQuery = _db.MeetingPolls
            .AsNoTracking()
            .Where(p => p.MeetingId == meetingId)
            .Include(p => p.Votes);

        if (!isHostOrAdmin && !isPollManager)
        {
            pollsQuery = pollsQuery.Where(p => p.Status != "draft");
        }

        var polls = await pollsQuery
            .OrderBy(p => p.CreatedAtUtc)
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

        if (!await CanManagePollsAsync(meetingId, userId.Trim(), username))
            return Unauthorized("Only meeting host or poll manager can create polls");

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
        var status = string.Equals(dto.Status, "open", StringComparison.OrdinalIgnoreCase) ? "open" : "draft";
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
            Status = status,
        };

        _db.MeetingPolls.Add(poll);
        await _db.SaveChangesAsync();

        return Ok(ToDto(poll));
    }

    /// <summary>
    /// Chỉnh sửa phiếu nháp trước khi công bố (chỉ host meeting).
    /// </summary>
    [HttpPut("{pollId}")]
    public async Task<IActionResult> UpdateDraft(Guid meetingId, string pollId, [FromBody] PollCreateRequestDto dto)
    {
        var userId = UserId(User);
        var username = User.FindFirstValue("username") ?? string.Empty;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var meeting = await _db.Meetings.FirstOrDefaultAsync(m => m.Id == meetingId);
        if (meeting == null)
            return NotFound("Meeting not found");
        if (!await CanManagePollsAsync(meetingId, userId.Trim(), username))
            return Unauthorized("Only meeting host or poll manager can edit draft polls");

        var poll = await _db.MeetingPolls
            .Include(p => p.Votes)
            .FirstOrDefaultAsync(p => p.MeetingId == meetingId && p.PollId == pollId);
        if (poll == null)
            return NotFound("Poll not found");
        if (poll.Status != "draft")
            return BadRequest("Only draft polls can be edited");

        if (string.IsNullOrWhiteSpace(dto.Title))
            return BadRequest("Title is required");

        var options = dto.Options?.Where(o => !string.IsNullOrWhiteSpace(o)).Select(o => o.Trim()).ToArray() ?? Array.Empty<string>();
        if (options.Length < 2 || options.Length > 8)
            return BadRequest("Options must have between 2 and 8 items");

        poll.Title = dto.Title.Trim();
        poll.OptionsJson = JsonSerializer.Serialize(options, JsonOpts);
        poll.SelectionMode = dto.SelectionMode == "multiple" ? "multiple" : "single";
        poll.EndAtUtc = dto.EndAt.HasValue && dto.EndAt.Value > 0 ? FromUnixMs(dto.EndAt.Value) : null;

        await _db.SaveChangesAsync();
        return Ok(ToDto(poll));
    }

    [HttpDelete("{pollId}")]
    public async Task<IActionResult> DeleteDraft(Guid meetingId, string pollId)
    {
        var userId = UserId(User);
        var username = User.FindFirstValue("username") ?? string.Empty;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        if (!await CanManagePollsAsync(meetingId, userId.Trim(), username))
            return Unauthorized("Only meeting host or poll manager can delete draft polls");

        var poll = await _db.MeetingPolls
            .FirstOrDefaultAsync(p => p.MeetingId == meetingId && p.PollId == pollId);
        if (poll == null)
            return NotFound("Poll not found");
        if (poll.Status != "draft")
            return BadRequest("Only draft polls can be deleted");

        _db.MeetingPolls.Remove(poll);
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    /// <summary>
    /// Công bố phiếu nháp cho cả phòng (draft -> open), chỉ host hoặc admin.
    /// </summary>
    [HttpPost("{pollId}/publish")]
    public async Task<IActionResult> Publish(Guid meetingId, string pollId, [FromBody] PollPublishRequestDto dto)
    {
        var userId = UserId(User);
        var username = User.FindFirstValue("username");
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();
        if (dto.PublishedBy != userId)
            return BadRequest("PublishedBy must match authenticated user");

        if (!await CanManagePollsAsync(meetingId, userId, username))
            return Unauthorized("Only meeting host or poll manager can publish polls");

        var poll = await _db.MeetingPolls
            .Include(p => p.Votes)
            .FirstOrDefaultAsync(p => p.MeetingId == meetingId && p.PollId == pollId);
        if (poll == null)
            return NotFound("Poll not found");

        if (poll.Status == "closed")
            return BadRequest("Poll is closed");

        if (poll.Status != "open")
        {
            poll.Status = "open";
            await _db.SaveChangesAsync();
        }
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
        var username = User.FindFirstValue("username");
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

        var canClose = await CanManagePollsAsync(meetingId, userId, username);
        if (!canClose)
            return Unauthorized("Only meeting host or poll manager can close");

        poll.Status = "closed";
        poll.ClosedBy = dto.ClosedBy;
        poll.ClosedAtUtc = dto.At > 0 ? FromUnixMs(dto.At) : DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(ToDto(poll));
    }

    [HttpGet("managers")]
    public async Task<IActionResult> ListManagers(Guid meetingId)
    {
        var userId = UserId(User);
        var role = User.FindFirstValue(ClaimTypes.Role);
        var username = User.FindFirstValue("username");
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        if (!await CanViewPollsAsync(meetingId, userId, role, username))
            return Unauthorized();

        var managers = await _db.MeetingPollManagers
            .AsNoTracking()
            .Where(x => x.MeetingId == meetingId)
            .OrderBy(x => x.Username)
            .ToListAsync();

        var usernames = managers
            .Select(x => x.Username)
            .Concat(managers.Select(x => x.AddedBy))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToLower())
            .Distinct()
            .ToList();

        var userMap = await _db.Users
            .AsNoTracking()
            .Where(u => usernames.Contains(u.Username.ToLower()))
            .ToDictionaryAsync(
                u => u.Username.ToLower(),
                u => string.IsNullOrWhiteSpace(u.FullName) ? u.Username : u.FullName!);

        var dtos = managers.Select(x =>
        {
            var un = x.Username.Trim().ToLower();
            var by = x.AddedBy.Trim().ToLower();
            return new PollManagerItemDto
            {
                Username = x.Username,
                FullName = userMap.TryGetValue(un, out var fn) ? fn : x.Username,
                AddedBy = x.AddedBy,
                AddedByFullName = userMap.TryGetValue(by, out var addedByFn) ? addedByFn : x.AddedBy,
                AddedAt = ToUnixMs(x.AddedAtUtc),
            };
        }).ToList();
        return Ok(dtos);
    }

    [HttpPost("managers")]
    public async Task<IActionResult> AddManager(Guid meetingId, [FromBody] AddPollManagerRequestDto dto)
        => await AddManagerCore(meetingId, dto);

    [HttpPut("managers")]
    public async Task<IActionResult> AddManagerPut(Guid meetingId, [FromBody] AddPollManagerRequestDto dto)
        => await AddManagerCore(meetingId, dto);

    private async Task<IActionResult> AddManagerCore(Guid meetingId, AddPollManagerRequestDto dto)
    {
        var userId = UserId(User);
        var username = User.FindFirstValue("username") ?? string.Empty;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var meeting = await _db.Meetings.AsNoTracking().FirstOrDefaultAsync(m => m.Id == meetingId);
        if (meeting == null)
            return NotFound("Meeting not found");
        if (!IsHostIdentityMatch(meeting.HostIdentity?.Trim() ?? string.Empty, userId, username))
            return Unauthorized("Only meeting host can add poll managers");

        var targetUsername = dto.Username?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(targetUsername))
            return BadRequest("Username is required");

        var userExists = await _db.Users.AsNoTracking().AnyAsync(u => u.Username.ToLower() == targetUsername.ToLower());
        if (!userExists)
            return NotFound("User not found");

        var exists = await _db.MeetingPollManagers
            .AnyAsync(x => x.MeetingId == meetingId && x.Username.ToLower() == targetUsername.ToLower());
        if (exists)
            return Ok(new { ok = true, message = "Manager already exists" });

        _db.MeetingPollManagers.Add(new MeetingPollManager
        {
            Id = Guid.NewGuid(),
            MeetingId = meetingId,
            Username = targetUsername,
            AddedBy = username,
            AddedAtUtc = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    [HttpDelete("managers/{username}")]
    public async Task<IActionResult> RemoveManager(Guid meetingId, string username)
    {
        var userId = UserId(User);
        var actorUsername = User.FindFirstValue("username") ?? string.Empty;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var meeting = await _db.Meetings.AsNoTracking().FirstOrDefaultAsync(m => m.Id == meetingId);
        if (meeting == null)
            return NotFound("Meeting not found");
        if (!IsHostIdentityMatch(meeting.HostIdentity?.Trim() ?? string.Empty, userId, actorUsername))
            return Unauthorized("Only meeting host can remove poll managers");

        var target = (username ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(target))
            return BadRequest("Username is required");

        var row = await _db.MeetingPollManagers
            .FirstOrDefaultAsync(x => x.MeetingId == meetingId && x.Username.ToLower() == target.ToLower());
        if (row == null)
            return NotFound("Poll manager not found");

        _db.MeetingPollManagers.Remove(row);
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
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
