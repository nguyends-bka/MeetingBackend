namespace MeetingBackend.Options;

public class RecordingStorageOptions
{
    // Absolute directory mounted for LiveKit egress output.
    public string RootDirectory { get; set; } = "/recordings";
}
