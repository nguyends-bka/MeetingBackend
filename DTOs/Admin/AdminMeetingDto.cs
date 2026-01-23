namespace MeetingBackend.DTOs.Admin;

public class AdminMeetingDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string HostName { get; set; } = string.Empty;
    public string HostIdentity { get; set; } = string.Empty;
    public string MeetingCode { get; set; } = string.Empty;
    public string Passcode { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int ParticipantCount { get; set; }
    public int ActiveParticipantCount { get; set; }
}
