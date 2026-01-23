namespace MeetingBackend.DTOs.Admin;

public class UpdateUserRoleResponseDto
{
    public string Message { get; set; } = string.Empty;
    public UserRoleDto User { get; set; } = null!;
}

public class UserRoleDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}
