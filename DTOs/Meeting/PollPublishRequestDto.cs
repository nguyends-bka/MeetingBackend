namespace MeetingBackend.DTOs.Meeting;

/// <summary>Khớp payload poll_publish.</summary>
public class PollPublishRequestDto
{
    public string PublishedBy { get; set; } = string.Empty;
    public long At { get; set; }
}
