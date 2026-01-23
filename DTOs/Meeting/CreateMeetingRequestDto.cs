namespace MeetingBackend.DTOs.Meeting;

public class CreateMeetingRequestDto
{
    public string Title { get; set; } = string.Empty;
    public string HostName { get; set; } = string.Empty;
    public string? Passcode { get; set; }
}
