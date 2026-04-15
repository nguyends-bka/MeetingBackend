namespace MeetingBackend.DTOs.Auth;

public class FaceLoginRequestDto
{
    // Embedding vector từ thiết bị (cùng kích thước với embedding đã lưu trong DB).
    // public byte[] Embedding { get; set; } = Array.Empty<byte>();
    public int[] Embedding { get; set; } = Array.Empty<int>();
}

