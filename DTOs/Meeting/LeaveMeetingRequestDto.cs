namespace MeetingBackend.DTOs.Meeting;

public class LeaveMeetingRequestDto
{
    public Guid? ParticipantId { get; set; }
    public Guid? MeetingId { get; set; }
}
