namespace MeetingBackend.Entities;

/// <summary>Cấu hình toàn hệ thống do Admin thay đổi được khi đang chạy.</summary>
public class SystemSetting
{
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public DateTime UpdatedAtUtc { get; set; }
}
