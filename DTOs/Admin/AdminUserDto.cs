using MeetingBackend.DTOs.Catalog;

namespace MeetingBackend.DTOs.Admin;

public class AdminUserDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? Position { get; set; }
    public string? AcademicRank { get; set; }
    public string? AcademicDegree { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    public string? OrganizationUnitName { get; set; }
    public bool HasAvatar { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>Danh sách quốc tịch/quốc gia của user.</summary>
    public List<UserCountryResponseDto> Countries { get; set; } = [];

    /// <summary>Danh sách ngôn ngữ của user.</summary>
    public List<UserLanguageResponseDto> Languages { get; set; } = [];
}
