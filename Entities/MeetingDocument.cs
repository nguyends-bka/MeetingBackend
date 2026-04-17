namespace MeetingBackend.Entities;

public class MeetingDocument
{
    public Guid Id { get; set; }

    public Guid MeetingId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = "application/octet-stream";

    public long Size { get; set; }

    // User identity từ JWT
    public string UploaderUserId { get; set; } = string.Empty;

    // Hiển thị trên UI
    public string UploaderName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// True: mọi người tham gia cuộc họp đều thấy.
    /// False: chỉ host/co-host/uploader thấy.
    /// </summary>
    public bool IsShared { get; set; } = true;

    // Đường dẫn file trên server (absolute hoặc relative để map ra file)
    public string StoragePath { get; set; } = string.Empty;

    public Meeting? Meeting { get; set; }
}

