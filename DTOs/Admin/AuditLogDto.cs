namespace MeetingBackend.DTOs.Admin;

public class AuditLogDto
{
    public Guid Id { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Severity { get; set; } = "info";
    public Guid? ActorUserId { get; set; }
    public string? ActorName { get; set; }
    public string? TargetId { get; set; }
    public string? TargetLabel { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public long At { get; set; } // unix ms
}

public class AuditLogPageDto
{
    public List<AuditLogDto> Items { get; set; } = [];
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
