namespace MeetingBackend.DTOs.Catalog;

/// <summary>
/// Một phần tử trong danh sách languages khi tạo/cập nhật user.
/// Frontend gửi code + isPrimary; backend validate.
/// </summary>
public class UserLanguageItemDto
{
    public string Code { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
}
