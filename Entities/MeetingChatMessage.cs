namespace MeetingBackend.Entities;

public class MeetingChatMessage
{
    public Guid Id { get; set; }

    public Guid MeetingId { get; set; }

    public string? ClientMessageId { get; set; }

    public string SenderIdentity { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public DateTime SentAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
