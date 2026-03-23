namespace MeetingBackend.DTOs.Meeting;

public class PollResponseDto
{
    public string PollId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string[] Options { get; set; } = Array.Empty<string>();
    public string CreatedBy { get; set; } = string.Empty;
    public string CreatedByName { get; set; } = string.Empty;
    public long CreatedAt { get; set; }
    public string SelectionMode { get; set; } = "single";
    public long? EndAt { get; set; }
    public string Status { get; set; } = "open";
    public long? ClosedAt { get; set; }
    public string? ClosedBy { get; set; }
    public List<PollVoteEntryDto> Votes { get; set; } = new();
}
