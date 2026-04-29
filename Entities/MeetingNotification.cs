using System;

namespace MeetingBackend.Entities;

public class MeetingNotification
{
    public Guid Id { get; set; }

    /// <summary>User ID người nhận thông báo (không phải người gửi)</summary>
    public string RecipientUserId { get; set; } = string.Empty;

    /// <summary>Meeting ID liên quan</summary>
    public Guid MeetingId { get; set; }

    /// <summary>Tiêu đề/tên của meeting</summary>
    public string MeetingTitle { get; set; } = string.Empty;

    /// <summary>Loại thông báo: invite_added, cohost_granted, cohost_removed, removed_from_meeting, meeting_started, meeting_ended</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Nội dung thông báo chi tiết</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Username người thực hiện hành động (tạo thông báo)</summary>
    public string ActorUsername { get; set; } = string.Empty;

    /// <summary>Thời gian tạo thông báo</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Thời gian người dùng xem thông báo (null = chưa xem)</summary>
    public DateTime? OpenedAt { get; set; }
}
