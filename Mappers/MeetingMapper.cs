using MeetingBackend.DTOs.Meeting;
using MeetingBackend.Entities;

namespace MeetingBackend.Mappers;

public static class MeetingMapper
{
    public static MeetingListItemDto ToMeetingListItemDto(Meeting meeting)
    {
        return new MeetingListItemDto
        {
            Id = meeting.Id,
            Title = meeting.Title,
            HostName = meeting.HostName,
            MeetingCode = meeting.MeetingCode,
            Passcode = meeting.Passcode,
            CreatedAt = meeting.CreatedAt
        };
    }

    public static MeetingHistoryItemDto ToMeetingHistoryItemDto(MeetingParticipant participant)
    {
        return new MeetingHistoryItemDto
        {
            Id = participant.Id,
            Username = participant.Username,
            UserId = participant.UserId,
            JoinedAt = participant.JoinedAt,
            LeftAt = participant.LeftAt,
            Duration = participant.LeftAt.HasValue
                ? (participant.LeftAt.Value - participant.JoinedAt).TotalMinutes
                : null
        };
    }
}
