namespace MeetingBackend.Models;

public record JoinMeetingRequest(
    string Room,
    string Identity
);
