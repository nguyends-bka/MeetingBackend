namespace MeetingBackend.DTOs.Meeting;

/// <summary>Khớp payload poll_vote.</summary>
public class PollVoteRequestDto
{
    public int[] OptionIndices { get; set; } = Array.Empty<int>();
    public string VoterIdentity { get; set; } = string.Empty;
    public string VoterName { get; set; } = string.Empty;
    public long At { get; set; }
}
