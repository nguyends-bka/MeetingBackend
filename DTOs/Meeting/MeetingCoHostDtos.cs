namespace MeetingBackend.DTOs.Meeting;

public class MeetingCoHostDto
{
    public string HostUserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
}

public class PromoteInviteeToCoHostRequestDto
{
    public string Username { get; set; } = string.Empty;
}

public class DemoteCoHostToInviteeRequestDto
{
    public string Username { get; set; } = string.Empty;
}
