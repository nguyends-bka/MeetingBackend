namespace MeetingBackend.Options;

public class FaceMatchingOptions
{
    public string Url { get; set; } = "http://100.68.239.124:8080/match";
    public int TimeoutSeconds { get; set; } = 20;
}
