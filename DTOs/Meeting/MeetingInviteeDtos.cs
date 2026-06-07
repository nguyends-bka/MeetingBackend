namespace MeetingBackend.DTOs.Meeting;

public class MeetingInviteeDto
{
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PrimaryLanguage { get; set; }
}

public class AddMeetingInviteeRequestDto
{
    public string Username { get; set; } = string.Empty;
}
