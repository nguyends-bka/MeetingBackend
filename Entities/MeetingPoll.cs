namespace MeetingBackend.Entities;

/// <summary>
/// Một phiếu biểu quyết trong meeting (đồng bộ với pollId phía client / LiveKit).
/// </summary>
public class MeetingPoll
{
    public Guid Id { get; set; }

    public Guid MeetingId { get; set; }

    /// <summary>Id do client tạo (UUID string), trùng với voteReducer Poll.id.</summary>
    public string PollId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    /// <summary>JSON mảng chuỗi phương án, ví dụ ["A","B"].</summary>
    public string OptionsJson { get; set; } = "[]";

    public string CreatedBy { get; set; } = string.Empty;

    public string CreatedByName { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    /// <summary>single | multiple</summary>
    public string SelectionMode { get; set; } = "single";

    public DateTime? EndAtUtc { get; set; }

    /// <summary>open | closed</summary>
    public string Status { get; set; } = "open";

    public DateTime? ClosedAtUtc { get; set; }

    public string? ClosedBy { get; set; }

    public Meeting? Meeting { get; set; }

    public ICollection<MeetingPollVote> Votes { get; set; } = new List<MeetingPollVote>();
}
