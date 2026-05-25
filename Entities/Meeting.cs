namespace MeetingBackend.Entities;

public enum MeetingStatus
{
    Upcoming = 0,
    Live = 1,
    Ended = 2,
    NoShow = 3,
    Cancelled = 4
}

public class Meeting
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string HostName { get; set; } = string.Empty;

    public string HostIdentity { get; set; } = string.Empty;  

    public string? Location { get; set; }

    public string RoomName { get; set; } = Guid.NewGuid().ToString();

    public string MeetingCode { get; set; } = string.Empty; // Code ngắn để share (6-8 ký tự)

    public string Passcode { get; set; } = string.Empty; // Mật khẩu để tham gia (4-6 chữ số)

    public DateTime CreatedAt { get; set; }

    /// <summary>Thời điểm bắt đầu thực tế (lần join đầu tiên).</summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>Thời điểm kết thúc thực tế (host kết thúc cuộc họp).</summary>
    public DateTime? EndedAt { get; set; }

    public MeetingStatus Status { get; set; } = MeetingStatus.Upcoming;

    public DateTime? EstimatedEndAt { get; set; }
}

