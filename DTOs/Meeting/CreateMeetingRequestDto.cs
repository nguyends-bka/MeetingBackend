namespace MeetingBackend.DTOs.Meeting;

public class CreateMeetingRequestDto
{
    public string Title { get; set; } = string.Empty;
    public string HostName { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? Passcode { get; set; }
    /// <summary>
    /// Thời gian bắt đầu dự kiến (unix milliseconds UTC). Nếu không gửi, dùng thời điểm hiện tại.
    /// </summary>
    public long? StartAt { get; set; }
    /// <summary>
    /// Thời gian kết thúc dự kiến (unix milliseconds UTC), có thể null.
    /// </summary>
    public long? EstimatedEndAt { get; set; }
}
