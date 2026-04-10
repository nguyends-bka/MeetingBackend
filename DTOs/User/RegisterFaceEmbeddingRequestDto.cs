namespace MeetingBackend.DTOs.User;

public class RegisterFaceEmbeddingRequestDto
{
    public float[]? Straight { get; set; }
    public float[]? Right { get; set; }
    public float[]? Left { get; set; }
    public float[]? Up { get; set; }
}
