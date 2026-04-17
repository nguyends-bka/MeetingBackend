namespace MeetingBackend.DTOs.Meeting;

public class MeetingDocumentDto
{
    public Guid Id { get; set; }
    public Guid MeetingId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Size { get; set; }

    public string UploaderUserId { get; set; } = string.Empty;
    public string UploaderName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsShared { get; set; }

    // Endpoint để lấy file (client sẽ fetch blob với Authorization)
    public string FileEndpoint { get; set; } = string.Empty;
}

