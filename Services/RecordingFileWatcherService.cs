using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MeetingBackend.Data;
using MeetingBackend.Entities;
using MeetingBackend.Options;
using Microsoft.EntityFrameworkCore;

namespace MeetingBackend.Services;

public class RecordingFileWatcherService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RecordingStorageOptions _opts;
    private readonly ILogger<RecordingFileWatcherService> _logger;

    public RecordingFileWatcherService(
        IServiceScopeFactory scopeFactory,
        IOptions<RecordingStorageOptions> opts,
        ILogger<RecordingFileWatcherService> logger)
    {
        _scopeFactory = scopeFactory;
        _opts = opts.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RecordingFileWatcherService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var cutoff = DateTime.UtcNow.AddDays(-1);
                var candidates = await db.MeetingRecordings
                    .Where(r => r.OutputFilePath != null
                                && r.EndedAtUtc != null
                                && r.EndedAtUtc >= cutoff
                                && r.Status != "Completed"
                                && r.Status != "Failed"
                                && r.Status != "completed"
                                && r.Status != "failed")
                    .ToListAsync(stoppingToken);

                if (candidates.Count > 0)
                {
                    var roots = ResolveRecordingRoots();

                    foreach (var rec in candidates)
                    {
                        if (string.IsNullOrWhiteSpace(rec.OutputFilePath)) continue;

                        if (TryResolvePhysicalFilePath(rec.OutputFilePath, roots, out var resolved)
                            && System.IO.File.Exists(resolved))
                        {
                            rec.Status = "Completed";
                            rec.ErrorMessage = null;
                            _logger.LogInformation("Found recording file for {RecordingId} at {Path}", rec.Id, resolved);
                        }
                        else if (rec.EndedAtUtc.HasValue)
                        {
                            var age = DateTime.UtcNow - rec.EndedAtUtc.Value;
                            if (age > TimeSpan.FromMinutes(2))
                            {
                                rec.Status = "Failed";
                                rec.ErrorMessage = "Recording output was not generated. Ensure at least one participant publishes audio/video during the meeting.";
                                _logger.LogWarning("Recording file not found for {RecordingId} after 2 minutes. Marking as Failed.", rec.Id);
                            }
                        }
                    }

                    await db.SaveChangesAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // shutting down
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RecordingFileWatcherService error");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private string ResolveRecordingRootDirectory()
    {
        var raw = (_opts.RootDirectory ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Path.GetFullPath("/recordings");
        }

        return Path.GetFullPath(raw);
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

    private bool TryResolvePhysicalFilePath(string outputFilePath, List<string> roots, out string physicalPath)
    {
        physicalPath = string.Empty;
        if (string.IsNullOrWhiteSpace(outputFilePath)) return false;

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
            var isInAllowedRoot = roots.Any(r => {
                var root = Path.GetFullPath(r);
                var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar)
                    ? root
                    : root + Path.DirectorySeparatorChar;
                return candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase);
            });
            if (!isInAllowedRoot) continue;

            if (System.IO.File.Exists(candidate))
            {
                physicalPath = candidate;
                return true;
            }
        }

        var fileName = Path.GetFileName(relativePart);
        if (string.IsNullOrWhiteSpace(fileName)) return false;

        foreach (var recordRoot in roots)
        {
            if (!Directory.Exists(recordRoot)) continue;

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
                // ignore inaccessible folders
            }
        }

        return false;
    }
}
