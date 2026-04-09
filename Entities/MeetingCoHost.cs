namespace MeetingBackend.Entities;

/// <summary>Đồng chủ trì (chủ trì tạo phòng vẫn là Meeting.HostIdentity).</summary>
public class MeetingCoHost
{
    public Guid Id { get; set; }

    public Guid MeetingId { get; set; }

    /// <summary>Users.Id dạng chuỗi — khớp JWT NameIdentifier.</summary>
    public string HostUserId { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public Meeting? Meeting { get; set; }
}
