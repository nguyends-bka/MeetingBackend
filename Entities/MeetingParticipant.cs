namespace MeetingBackend.Entities;

public class MeetingParticipant
{
    public Guid Id { get; set; }

    public Guid MeetingId { get; set; }

    public string UserId { get; set; } = string.Empty; // User identity từ JWT

    public string Username { get; set; } = string.Empty; // Username để hiển thị

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LeftAt { get; set; } // Null nếu vẫn đang trong meeting

    // Navigation property
    public Meeting? Meeting { get; set; }
}
