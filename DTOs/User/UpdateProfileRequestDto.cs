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
}
