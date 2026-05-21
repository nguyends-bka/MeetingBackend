using MeetingBackend.DTOs.Catalog;

namespace MeetingBackend.DTOs.User;

public class UpdateProfileRequestDto
{
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? Position { get; set; }
    public string? AcademicRank { get; set; } // GS | PGS
    public string? AcademicDegree { get; set; } // TS | ThS | CN | KS
    public Guid? OrganizationUnitId { get; set; }
    public string? Avatar { get; set; }

    /// <summary>
    /// Danh sách mã quốc gia/quốc tịch của user.
    /// Null = không thay đổi; [] = xóa toàn bộ; ["VN","US"] = thay thế hoàn toàn.
    /// </summary>
    public List<string>? CountryCodes { get; set; }

    /// <summary>
    /// Danh sách ngôn ngữ của user.
    /// Null = không thay đổi; [] = xóa toàn bộ; danh sách đầy đủ = thay thế hoàn toàn.
    /// Khi danh sách không rỗng, bắt buộc phải có đúng 1 phần tử IsPrimary = true.
    /// </summary>
    public List<UserLanguageItemDto>? Languages { get; set; }
}
