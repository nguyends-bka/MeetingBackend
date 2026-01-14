namespace MeetingBackend.Entities;

public class Meeting
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string RoomName { get; set; } = string.Empty;
    public string HostIdentity { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
