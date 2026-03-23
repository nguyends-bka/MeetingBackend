namespace MeetingBackend.Entities;

public class MeetingPollManager
{
    public Guid Id { get; set; }
    public Guid MeetingId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string AddedBy { get; set; } = string.Empty;
    public DateTime AddedAtUtc { get; set; } = DateTime.UtcNow;
}
