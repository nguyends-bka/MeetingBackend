namespace MeetingBackend.DTOs.User;

public class UserLookupByUsernameDto
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? FullName { get; set; }
}
