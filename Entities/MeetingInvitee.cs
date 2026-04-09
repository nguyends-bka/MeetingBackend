namespace MeetingBackend.Entities;

public class MeetingInvitee
{
    public Guid Id { get; set; }

    public Guid MeetingId { get; set; }

    public string Username { get; set; } = string.Empty;

    public Meeting? Meeting { get; set; }
}
