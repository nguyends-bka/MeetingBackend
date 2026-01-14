namespace MeetingBackend.Models;

public record JoinMeetingRequest(
    Guid MeetingId,
    string Identity
);
