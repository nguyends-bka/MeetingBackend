namespace MeetingBackend.Entities;

/// <summary>
/// Nhật ký hệ thống: ghi lại các hành động quan trọng (đăng nhập, tạo/hủy cuộc họp,
/// đổi quyền, xóa tài nguyên...) để Admin theo dõi.
/// </summary>
public class AuditLog
{
    public Guid Id { get; set; }

    /// <summary>Nhóm sự kiện, ví dụ: "Auth", "Meeting", "Admin".</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Mã hành động, ví dụ: "login.success", "meeting.create", "user.role.update".</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Mức độ: "info" | "warning" | "error".</summary>
    public string Severity { get; set; } = "info";

    /// <summary>Người thực hiện (Users.Id) — null nếu chưa xác định (ví dụ đăng nhập thất bại).</summary>
    public Guid? ActorUserId { get; set; }

    /// <summary>Tên hiển thị / username của người thực hiện (lưu cứng để không phụ thuộc user còn tồn tại).</summary>
    public string? ActorName { get; set; }

    /// <summary>Đối tượng bị tác động (id): meetingId, userId... để tiện tra cứu.</summary>
    public string? TargetId { get; set; }

    /// <summary>Mô tả đối tượng bị tác động (tên cuộc họp, username bị xóa...).</summary>
    public string? TargetLabel { get; set; }

    /// <summary>Mô tả ngắn gọn, dễ đọc cho người xem.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Địa chỉ IP của request (nếu lấy được).</summary>
    public string? IpAddress { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
