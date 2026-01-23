namespace MeetingBackend.DTOs.Meeting;

public class JoinMeetingRequestDto
{
    public Guid? MeetingId { get; set; }
    public string? MeetingCode { get; set; }
    public string Passcode { get; set; } = string.Empty;
}
