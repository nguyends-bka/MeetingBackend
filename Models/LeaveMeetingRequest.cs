namespace MeetingBackend.Models;

public class LeaveMeetingRequest
{
    public Guid ParticipantId { get; set; }
    public Guid MeetingId { get; set; } // Thêm MeetingId để xác định meeting và đóng tất cả session active
}
