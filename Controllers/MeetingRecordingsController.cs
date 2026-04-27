using System.Security.Claims;
using MeetingBackend.Data;
using MeetingBackend.DTOs.Meeting;
using MeetingBackend.Entities;
using MeetingBackend.Options;
using MeetingBackend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MeetingBackend.Controllers;

[ApiController]
[Route("api/meeting")]
[Authorize]
public class MeetingRecordingsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly LiveKitEgressService _egress;
    private readonly RecordingStorageOptions _recordingStorageOptions;

    public MeetingRecordingsController(
        AppDbContext db,
        LiveKitEgressService egress,
        IOptions<RecordingStorageOptions> recordingStorageOptions)
    {
        _db = db;
        _egress = egress;
        _recordingStorageOptions = recordingStorageOptions.Value;
    }

    private string GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue(ClaimTypes.Name)
        ?? string.Empty;

    private string GetUsername() => User.FindFirstValue("username") ?? string.Empty;

    private string GetUserRole() => User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

    private string ResolveRecordingRootDirectory()
    {
        var raw = (_recordingStorageOptions.RootDirectory ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Path.GetFullPath("/recordings");
        }

        return Path.GetFullPath(raw);
    }

    private string BuildStoredOutputPath(Guid meetingId, string fileName)
        => $"recordings/{meetingId}/{fileName}";

    private string BuildEgressOutputPath(Guid meetingId, string fileName)
    {
        var full = Path.Combine(ResolveRecordingRootDirectory(), meetingId.ToString(), fileName);
        // Egress expects POSIX-style separators in filepath.
        return full.Replace('\\', '/');
    }

    private static bool IsPathUnderRoot(string candidatePath, string rootPath)
    {
        var root = Path.GetFullPath(rootPath);
        var candidate = Path.GetFullPath(candidatePath);

        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;

        return candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase);
    }

    private List<string> ResolveRecordingRoots()
    {
        var roots = new List<string>
        {
            ResolveRecordingRootDirectory(),
            "/home/ubuntu/meeting-recordings",
            "/home/ubuntu/meeting-deploy/recordings",
        };

        return roots
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private bool TryResolvePhysicalFilePath(string outputFilePath, out string physicalPath)
    {
        physicalPath = string.Empty;
        if (string.IsNullOrWhiteSpace(outputFilePath)) return false;

        var roots = ResolveRecordingRoots();
        var root = roots[0];
        var normalized = outputFilePath.Replace('\\', '/').Trim();
        var relativePart = normalized;
        if (relativePart.StartsWith("recordings/", StringComparison.OrdinalIgnoreCase))
        {
            relativePart = relativePart["recordings/".Length..];
        }

        var candidates = new List<string>();
        if (Path.IsPathRooted(outputFilePath))
        {
            candidates.Add(Path.GetFullPath(outputFilePath));
        }
        else
        {
            var relativeOsPath = relativePart.Replace('/', Path.DirectorySeparatorChar);
            foreach (var recordRoot in roots)
            {
                candidates.Add(Path.GetFullPath(Path.Combine(recordRoot, relativeOsPath)));
            }
        }

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var isInAllowedRoot = roots.Any(r => IsPathUnderRoot(candidate, r));
            if (!isInAllowedRoot)
            {
                continue;
            }

            if (System.IO.File.Exists(candidate))
            {
                physicalPath = candidate;
                return true;
            }
        }

        var fileName = Path.GetFileName(relativePart);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        foreach (var recordRoot in roots)
        {
            if (!Directory.Exists(recordRoot))
            {
                continue;
            }

            try
            {
                var found = Directory
                    .EnumerateFiles(recordRoot, fileName, SearchOption.AllDirectories)
                    .FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(found))
                {
                    physicalPath = Path.GetFullPath(found);
                    return true;
                }
            }
            catch
            {
                // Ignore inaccessible folders and continue searching other roots.
            }
        }

        return false;
    }

    private async Task<bool> CanViewAsync(Guid meetingId, string userId, string username, string role)
    {
        var meeting = await _db.Meetings.AsNoTracking().FirstOrDefaultAsync(m => m.Id == meetingId);
        if (meeting == null) return false;

        var isAdmin = string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);
        return isAdmin
            || await MeetingHostAuth.IsAnyHostAsync(_db, meeting, userId, username)
            || await HasParticipantAsync(meetingId, userId)
            || await IsInviteeAsync(meetingId, username);
    }

    private MeetingRecordingDto ToDto(MeetingRecording r)
    {
        var hasFile = TryResolvePhysicalFilePath(r.OutputFilePath, out var physicalPath)
            && System.IO.File.Exists(physicalPath);
        var status = r.Status;
        if (string.Equals(r.Status, "Completed", StringComparison.OrdinalIgnoreCase) && !hasFile)
        {
            status = "Failed";
        }

        return new MeetingRecordingDto
        {
            Id = r.Id,
            MeetingId = r.MeetingId,
            EgressId = r.EgressId,
            Status = status,
            OutputFilePath = r.OutputFilePath,
            StartedAtUtc = r.StartedAtUtc,
            EndedAtUtc = r.EndedAtUtc,
            StartedByUserId = r.StartedByUserId,
            StartedByName = r.StartedByName,
            ErrorMessage = hasFile
                ? r.ErrorMessage
                : (r.ErrorMessage ?? "Recording output file is unavailable"),
            PlaybackEndpoint = $"/api/meeting/{r.MeetingId}/recordings/{r.Id}/file",
            IsFileAvailable = hasFile,
        };
    }

    private async Task<bool> HasParticipantAsync(Guid meetingId, string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return false;
        return await _db.MeetingParticipants.AnyAsync(p =>
            p.MeetingId == meetingId && p.UserId == userId);
    }

    private async Task<bool> IsInviteeAsync(Guid meetingId, string username)
    {
        if (string.IsNullOrWhiteSpace(username)) return false;
        var normalized = username.Trim().ToLower();
        return await _db.MeetingInvitees.AnyAsync(i =>
            i.MeetingId == meetingId && i.Username.ToLower() == normalized);
    }

    [HttpGet("{meetingId}/recordings")]
    public async Task<IActionResult> List(Guid meetingId)
    {
        var userId = GetUserId();
        var username = GetUsername();
        var role = GetUserRole();

        var meeting = await _db.Meetings.AsNoTracking().FirstOrDefaultAsync(m => m.Id == meetingId);
        if (meeting == null) return NotFound("Meeting not found");

        var canView = await CanViewAsync(meetingId, userId, username, role);

        if (!canView) return Unauthorized("Only meeting members can view recordings");

        var items = await _db.Set<MeetingRecording>()
            .Where(r => r.MeetingId == meetingId)
            .OrderByDescending(r => r.StartedAtUtc)
            .ToListAsync();

        return Ok(items.Select(ToDto));
    }

    [HttpGet("{meetingId}/recordings/{recordingId}/file")]
    public async Task<IActionResult> GetRecordingFile(Guid meetingId, Guid recordingId)
    {
        var userId = GetUserId();
        var username = GetUsername();
        var role = GetUserRole();

        var canView = await CanViewAsync(meetingId, userId, username, role);
        if (!canView) return Unauthorized("Only meeting members can view recordings");

        var recording = await _db.Set<MeetingRecording>().AsNoTracking().FirstOrDefaultAsync(r =>
            r.MeetingId == meetingId && r.Id == recordingId);
        if (recording == null) return NotFound("Recording not found");

        if (!TryResolvePhysicalFilePath(recording.OutputFilePath, out var filePath))
            return NotFound("Recording file path is invalid");

        if (!System.IO.File.Exists(filePath))
            return NotFound("Recording file not found");

        var fileName = Path.GetFileName(filePath);
        return PhysicalFile(filePath, "video/mp4", fileName, enableRangeProcessing: true);
    }

    [HttpPost("{meetingId}/recordings/start")]
    public async Task<IActionResult> Start(Guid meetingId)
    {
        var userId = GetUserId();
        var username = GetUsername();

        var meeting = await _db.Meetings.FirstOrDefaultAsync(m => m.Id == meetingId);
        if (meeting == null) return NotFound("Meeting not found");

        var isHost = await MeetingHostAuth.IsAnyHostAsync(_db, meeting, userId, username);
        if (!isHost) return Unauthorized("Only meeting host/co-host can start recording");

        if (meeting.EndedAt.HasValue)
            return BadRequest("Meeting has ended");

        var hasActiveRecording = await _db.Set<MeetingRecording>().AnyAsync(r =>
            r.MeetingId == meetingId
            && (r.Status == "Starting" || r.Status == "Active" || r.Status == "Stopping"));
        if (hasActiveRecording)
            return Conflict("Meeting already has an active recording");

        var (canCheckTracks, hasMediaTrack, _) = await _egress.HasActiveMediaTrackAsync(
            meeting.RoomName,
            HttpContext.RequestAborted);

        // Track pre-check is best-effort. If backend cannot query RoomService due to permission/network,
        // do not hard-block start recording.
        // Do not hard-block recording when there is temporarily no active media.
        // In real meetings, participants may turn camera/mic on after recording starts.
        _ = canCheckTracks;
        _ = hasMediaTrack;

        var fileName = $"{DateTime.UtcNow:yyyyMMdd_HHmmss}.mp4";
        var outputFilePath = BuildStoredOutputPath(meetingId, fileName);
        var egressOutputPath = BuildEgressOutputPath(meetingId, fileName);
        var (ok, egressId, error) = await _egress.StartRoomCompositeRecordingAsync(
            meeting.RoomName,
            egressOutputPath,
            HttpContext.RequestAborted);

        if (!ok || string.IsNullOrWhiteSpace(egressId))
        {
            return BadRequest(error ?? "Failed to start recording");
        }

        var startedByName = !string.IsNullOrWhiteSpace(username) ? username : userId;
        var recording = new MeetingRecording
        {
            Id = Guid.NewGuid(),
            MeetingId = meetingId,
            EgressId = egressId,
            Status = "Active",
            OutputFilePath = outputFilePath,
            StartedAtUtc = DateTime.UtcNow,
            StartedByUserId = userId,
            StartedByName = startedByName,
        };

        _db.Set<MeetingRecording>().Add(recording);
        await _db.SaveChangesAsync();

        return Ok(ToDto(recording));
    }

    [HttpPost("{meetingId}/recordings/{recordingId}/stop")]
    public async Task<IActionResult> Stop(Guid meetingId, Guid recordingId)
    {
        var userId = GetUserId();
        var username = GetUsername();

        var meeting = await _db.Meetings.FirstOrDefaultAsync(m => m.Id == meetingId);
        if (meeting == null) return NotFound("Meeting not found");

        var isHost = await MeetingHostAuth.IsAnyHostAsync(_db, meeting, userId, username);
        if (!isHost) return Unauthorized("Only meeting host/co-host can stop recording");

        var recording = await _db.Set<MeetingRecording>().FirstOrDefaultAsync(r =>
            r.MeetingId == meetingId && r.Id == recordingId);
        if (recording == null) return NotFound("Recording not found");

        if (string.IsNullOrWhiteSpace(recording.EgressId))
            return BadRequest("Recording does not have egress id");

        var (ok, error) = await _egress.StopEgressAsync(recording.EgressId, HttpContext.RequestAborted);
        var isAlreadyFinished = !string.IsNullOrWhiteSpace(error)
            && error.Contains("EGRESS_COMPLETE", StringComparison.OrdinalIgnoreCase);

        if (!ok && !isAlreadyFinished)
        {
            // Do not fail the Stop API for transient egress errors.
            // Mark as Stopping and let background watcher re-check file presence.
            recording.Status = "Stopping";
            recording.ErrorMessage = error;
            recording.EndedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return Ok(ToDto(recording));
        }

        recording.EndedAtUtc = DateTime.UtcNow;

        // Wait for file flush to mounted storage before final status (allow longer timeout).
        var hasFile = false;
        for (var i = 0; i < 90; i++)
        {
            if (TryResolvePhysicalFilePath(recording.OutputFilePath, out var resolvedPath)
                && System.IO.File.Exists(resolvedPath))
            {
                hasFile = true;
                break;
            }

            await Task.Delay(TimeSpan.FromSeconds(1), HttpContext.RequestAborted);
        }

        if (hasFile)
        {
            recording.Status = "Completed";
            recording.ErrorMessage = null;
        }
        else
        {
            recording.Status = "Failed";
            recording.ErrorMessage =
                "Recording output was not generated yet. Ensure at least one participant publishes audio during the meeting and try recording again.";
        }

        await _db.SaveChangesAsync();

        return Ok(ToDto(recording));
    }

    [HttpDelete("{meetingId}/recordings/{recordingId}")]
    public async Task<IActionResult> Delete(Guid meetingId, Guid recordingId)
    {
        var userId = GetUserId();
        var username = GetUsername();

        var meeting = await _db.Meetings.FirstOrDefaultAsync(m => m.Id == meetingId);
        if (meeting == null) return NotFound("Meeting not found");

        var isHost = await MeetingHostAuth.IsAnyHostAsync(_db, meeting, userId, username);
        if (!isHost) return Unauthorized("Only meeting host/co-host can delete recording");

        var recording = await _db.Set<MeetingRecording>().FirstOrDefaultAsync(r =>
            r.MeetingId == meetingId && r.Id == recordingId);
        if (recording == null) return NotFound("Recording not found");

        var status = (recording.Status ?? string.Empty).Trim();
        if (string.Equals(status, "Starting", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Stopping", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Cannot delete an active recording. Stop it first.");
        }

        if (TryResolvePhysicalFilePath(recording.OutputFilePath, out var filePath)
            && System.IO.File.Exists(filePath))
        {
            try
            {
                System.IO.File.Delete(filePath);
            }
            catch
            {
                return StatusCode(500, "Failed to delete recording file");
            }
        }

        _db.Set<MeetingRecording>().Remove(recording);
        await _db.SaveChangesAsync();

        return NoContent();
    }
}
