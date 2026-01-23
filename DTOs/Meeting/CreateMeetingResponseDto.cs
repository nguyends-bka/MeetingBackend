namespace MeetingBackend.DTOs.Meeting;

public class CreateMeetingResponseDto
{
    public Guid MeetingId { get; set; }
    public string MeetingCode { get; set; } = string.Empty;
    public string Passcode { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
}
