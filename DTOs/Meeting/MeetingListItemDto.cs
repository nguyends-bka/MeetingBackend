namespace MeetingBackend.DTOs.Meeting;

public class MeetingListItemDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string HostName { get; set; } = string.Empty;
    public string MeetingCode { get; set; } = string.Empty;
    public string Passcode { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
