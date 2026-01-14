namespace MeetingBackend.Entities;

public class Meeting
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string HostName { get; set; } = string.Empty;

    public string RoomName { get; set; } = Guid.NewGuid().ToString();

    public DateTime CreatedAt { get; set; }
}
