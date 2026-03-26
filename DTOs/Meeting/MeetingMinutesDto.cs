namespace MeetingBackend.DTOs.Meeting;

public class MeetingMinutesDto
{
    public Guid MeetingId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string HostName { get; set; } = string.Empty;
    public string HostIdentity { get; set; } = string.Empty;
    public string LocationLabel { get; set; } = "Trực tuyến";
    public string LocationDetail { get; set; } = string.Empty;
    public long StartedAt { get; set; }
    public long? EndedAtEstimated { get; set; }
    public int ParticipantCount { get; set; }
    public IReadOnlyList<MeetingMinutesParticipantDto> Participants { get; set; } = Array.Empty<MeetingMinutesParticipantDto>();
    public IReadOnlyList<MeetingMinutesTranscriptLineDto> Transcript { get; set; } = Array.Empty<MeetingMinutesTranscriptLineDto>();
    public IReadOnlyList<MeetingMinutesPollDto> Polls { get; set; } = Array.Empty<MeetingMinutesPollDto>();
}

public class MeetingMinutesParticipantDto
{
    public string DisplayName { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
}

public class MeetingMinutesTranscriptLineDto
{
    public string SpeakerName { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public long At { get; set; }
}

public class MeetingMinutesPollDto
{
    public string PollId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string[] Options { get; set; } = Array.Empty<string>();
    public Dictionary<int, int> OptionVoteCounts { get; set; } = new();
}
