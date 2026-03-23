namespace MeetingBackend.DTOs.Meeting;

/// <summary>Khớp payload poll_close.</summary>
public class PollCloseRequestDto
{
    public string ClosedBy { get; set; } = string.Empty;
    public long At { get; set; }
}
