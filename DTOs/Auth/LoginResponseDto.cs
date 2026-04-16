namespace MeetingBackend.DTOs.Auth;

public class LoginResponseDto
{
    public string Token { get; set; } = string.Empty;
    public AuthUserDto User { get; set; } = null!;
}

public class AuthUserDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;
    public string? Position { get; set; }
    public string? AcademicRank { get; set; }
    public string? AcademicDegree { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    public string? Avatar { get; set; }

    /// <summary>Đã có vector embedding khuôn mặt trong DB (dùng cho đăng nhập Face).</summary>
    public bool HasFaceEmbedding { get; set; }
}
