namespace MeetingBackend.DTOs.Meeting;

public class UpdateMeetingRequestDto
{
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Thời gian bắt đầu dự kiến (unix milliseconds UTC).
    /// </summary>
    public long StartAt { get; set; }

    /// <summary>
    /// Thời gian kết thúc dự kiến (unix milliseconds UTC), có thể null.
    /// Dùng tạm trường StartedAt của Meeting để tránh thay đổi schema.
    /// </summary>
    public long? EstimatedEndAt { get; set; }
}

