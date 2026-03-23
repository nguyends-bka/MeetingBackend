namespace MeetingBackend.DTOs.Meeting;

public class PollVoteEntryDto
{
    public string VoterIdentity { get; set; } = string.Empty;
    public string VoterName { get; set; } = string.Empty;
    public int[] OptionIndices { get; set; } = Array.Empty<int>();
    public long At { get; set; }
}
