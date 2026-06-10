using System.ComponentModel.DataAnnotations;

namespace MeetingBackend.Entities;

public enum MinutesSummaryStatus
{
    Pending = 0,
    Processing = 1,
    Success = 2,
    Failed = 3
}

public class MeetingMinutesSummary
{
    [Key]
    public Guid MeetingId { get; set; }

    public string SummaryText { get; set; } = string.Empty;

    public MinutesSummaryStatus Status { get; set; } = MinutesSummaryStatus.Pending;

    public string? ErrorMessage { get; set; }

    public string? LlmJobId { get; set; }

    public string Overview { get; set; } = string.Empty;

    public string Discussions { get; set; } = string.Empty;

    public string ActionItems { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation property
    public Meeting? Meeting { get; set; }
}
