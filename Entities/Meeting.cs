namespace MeetingBackend.Entities;

public class Meeting
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string HostName { get; set; } = string.Empty;

    public string HostIdentity { get; set; } = string.Empty;  

    public string RoomName { get; set; } = Guid.NewGuid().ToString();

    public string MeetingCode { get; set; } = string.Empty; // Code ngắn để share (6-8 ký tự)

    public string Passcode { get; set; } = string.Empty; // Mật khẩu để tham gia (4-6 chữ số)

    public DateTime CreatedAt { get; set; }
}
