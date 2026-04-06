namespace MeetingBackend.DTOs.User;

public class RegisterFaceEmbeddingRequestDto
{
    public float[] Embedding { get; set; } = Array.Empty<float>();
}
