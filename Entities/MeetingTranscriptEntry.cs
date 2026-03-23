namespace MeetingBackend.Entities;

public class MeetingTranscriptEntry
{
    public Guid Id { get; set; }

    public Guid MeetingId { get; set; }

    public string SpeakerIdentity { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public DateTime AtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
