namespace MeetingBackend.Entities;

/// <summary>
/// Một lượt bỏ phiếu (theo voterIdentity, ghi đè nếu bỏ lại).
/// </summary>
public class MeetingPollVote
{
    public Guid Id { get; set; }

    public Guid MeetingPollId { get; set; }

    /// <summary>Identity LiveKit / userId JWT.</summary>
    public string VoterIdentity { get; set; } = string.Empty;

    public string VoterName { get; set; } = string.Empty;

    /// <summary>JSON mảng số nguyên chỉ số phương án, ví dụ [0] hoặc [0,2].</summary>
    public string OptionIndicesJson { get; set; } = "[]";

    public DateTime VotedAtUtc { get; set; }

    public MeetingPoll? MeetingPoll { get; set; }
}
