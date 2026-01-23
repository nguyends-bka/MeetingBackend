namespace MeetingBackend.DTOs.User;

public class UserProfileResponseDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public DateTime CreatedAt { get; set; }
}
