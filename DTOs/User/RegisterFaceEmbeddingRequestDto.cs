namespace MeetingBackend.DTOs.User;

public class RegisterFaceEmbeddingRequestDto
{
    public int[]? Straight { get; set; }
    public int[]? Right { get; set; }
    public int[]? Left { get; set; }
    public int[]? Up { get; set; }
}
