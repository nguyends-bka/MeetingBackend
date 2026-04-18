namespace MeetingBackend.Entities;

public class MeetingRecording
{
    public Guid Id { get; set; }

    public Guid MeetingId { get; set; }
    public Meeting Meeting { get; set; } = null!;

    public string EgressId { get; set; } = string.Empty;

    // Starting | Active | Stopping | Completed | Failed
    public string Status { get; set; } = "Starting";

    // Relative output path requested to egress, e.g. recordings/{meetingId}/20260417_120000.mp4
    public string OutputFilePath { get; set; } = string.Empty;

    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAtUtc { get; set; }

    public string StartedByUserId { get; set; } = string.Empty;
    public string StartedByName { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }
}
