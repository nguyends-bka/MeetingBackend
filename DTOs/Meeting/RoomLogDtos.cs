namespace MeetingBackend.DTOs.Meeting;

public class RoomChatCreateRequestDto
{
    public string? ClientMessageId { get; set; }
    public string SenderIdentity { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public long At { get; set; }
}

public class RoomTranscriptCreateRequestDto
{
    public string SpeakerIdentity { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public long At { get; set; }
}

public class RoomChatItemDto
{
    public string? ClientMessageId { get; set; }
    public string SenderIdentity { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public long At { get; set; }
}

public class RoomTranscriptItemDto
{
    public string? SpeakerIdentity { get; set; }
    public string SpeakerName { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public long At { get; set; }
}

public class RoomLogResponseDto
{
    public List<RoomChatItemDto> ChatMessages { get; set; } = new();
    public List<RoomTranscriptItemDto> TranscriptEntries { get; set; } = new();
}
