namespace MeetingBackend.Models;

public class JoinByCodeRequest
{
    public string MeetingCode { get; set; } = string.Empty;
    public string Passcode { get; set; } = string.Empty; // Mật khẩu để tham gia
}
