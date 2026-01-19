namespace MeetingBackend.Models
{
    public class CreateMeetingRequest
    {
        public string Title { get; set; } = string.Empty;
        public string HostName { get; set; } = string.Empty;

        public string HostIdentity { get; set; } = string.Empty;

        public string? Passcode { get; set; } // Passcode tùy chọn, nếu không có sẽ tự động tạo
    }
}
