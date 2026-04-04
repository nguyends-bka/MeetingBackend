namespace MeetingBackend.DTOs.Auth;

public class FaceLoginRequestDto
{
    // Embedding vector từ thiết bị (cùng kích thước với embedding đã lưu trong DB).
    public float[] Embedding { get; set; } = Array.Empty<float>();
}

