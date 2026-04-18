namespace MeetingBackend.DTOs.Meeting;

public class MeetingRecordingDto
{
    public Guid Id { get; set; }
    public Guid MeetingId { get; set; }
    public string EgressId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string OutputFilePath { get; set; } = string.Empty;
    public DateTime StartedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }
    public string StartedByUserId { get; set; } = string.Empty;
    public string StartedByName { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public string PlaybackEndpoint { get; set; } = string.Empty;
    public bool IsFileAvailable { get; set; }
}
