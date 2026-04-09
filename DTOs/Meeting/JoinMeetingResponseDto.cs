namespace MeetingBackend.DTOs.Meeting;

public class JoinMeetingResponseDto
{
    public string Token { get; set; } = string.Empty;
    public string LiveKitUrl { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public Guid MeetingId { get; set; }
    public string MeetingCode { get; set; } = string.Empty;
    public Guid ParticipantId { get; set; }
    public string? Title { get; set; }
    public string HostIdentity { get; set; } = string.Empty;

    /// <summary>User hiện tại là chủ trì gốc hoặc đồng chủ trì (quyền điều khiển phòng).</summary>
    public bool IsMeetingHost { get; set; }
}
