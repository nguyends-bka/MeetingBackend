namespace MeetingBackend.Options;

public class LiveKitOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? EgressUrl { get; set; }
    public int EgressRequestTimeoutSeconds { get; set; } = 90;
}
