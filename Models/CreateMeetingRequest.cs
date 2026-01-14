namespace MeetingBackend.Models
{
    public class CreateMeetingRequest
    {
        public string Title { get; set; } = string.Empty;
        public string HostName { get; set; } = string.Empty;

        // Identity dùng cho LiveKit
        public string HostIdentity { get; set; } = string.Empty;
    }
}
