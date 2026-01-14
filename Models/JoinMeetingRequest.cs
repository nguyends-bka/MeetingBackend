namespace MeetingBackend.Models
{
    public class JoinMeetingRequest
    {
        public Guid MeetingId { get; set; }
        public string Identity { get; set; } = string.Empty;
    }
}
