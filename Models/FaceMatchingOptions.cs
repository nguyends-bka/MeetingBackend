namespace MeetingBackend.Models;

public class FaceMatchingOptions
{
    public string Url { get; set; } = "http://54.169.201.65:8080/match";
    public int TimeoutSeconds { get; set; } = 20;
}
