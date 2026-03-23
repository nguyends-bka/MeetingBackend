namespace MeetingBackend.DTOs.Meeting;

public class AddPollManagerRequestDto
{
    public string Username { get; set; } = string.Empty;
}

public class PollManagerItemDto
{
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string AddedBy { get; set; } = string.Empty;
    public string AddedByFullName { get; set; } = string.Empty;
    public long AddedAt { get; set; }
}
