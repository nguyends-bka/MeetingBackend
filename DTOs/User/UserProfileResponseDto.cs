namespace MeetingBackend.DTOs.User;

public class UserProfileResponseDto
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
    public string? FaceTemplate { get; set; }
    public DateTime CreatedAt { get; set; }
}
