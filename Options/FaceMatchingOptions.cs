namespace MeetingBackend.Options;

public class FaceMatchingOptions
{
    /// <summary>
    /// Cách tính độ tương đồng khuôn mặt:
    /// "Local"  - 512 byte -> 128 float32 (little-endian) rồi tính cosine similarity ngay trong backend.
    /// "Remote" - gọi service FaceMatching qua HTTP (Url bên dưới).
    /// </summary>
    public string Mode { get; set; } = FaceMatchingModes.Local;

    public string Url { get; set; } = "http://100.68.239.124:8080/match";
    public int TimeoutSeconds { get; set; } = 20;
}

public static class FaceMatchingModes
{
    public const string Local = "Local";
    public const string Remote = "Remote";
}
