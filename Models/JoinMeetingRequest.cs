namespace MeetingBackend.Models
{
    public class JoinMeetingRequest
    {
        public Guid MeetingId { get; set; }
        public string MeetingCode { get; set; } = string.Empty; // Có thể dùng MeetingCode thay vì MeetingId
        public string Passcode { get; set; } = string.Empty; // Mật khẩu để tham gia
    }
}
