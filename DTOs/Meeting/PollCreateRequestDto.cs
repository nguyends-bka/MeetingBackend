using Swashbuckle.AspNetCore.Annotations;

namespace MeetingBackend.DTOs.Meeting;

/// <summary>Khớp payload poll_create từ voteReducer / LiveKit.</summary>
public class PollCreateRequestDto
{
    /// <summary>Tùy chọn. Để trống thì backend tự sinh GUID (chuỗi).</summary>
    [SwaggerSchema(Nullable = true, Description = "Tùy chọn — bỏ qua để server tự sinh. Example Value vẫn có thể liệt kê field này; có thể xóa khỏi JSON khi gửi.")]
    public string? PollId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string[] Options { get; set; } = Array.Empty<string>();
    public string CreatedBy { get; set; } = string.Empty;
    public string CreatedByName { get; set; } = string.Empty;
    public long CreatedAt { get; set; }
    public string SelectionMode { get; set; } = "single";
    public long? EndAt { get; set; }
    /// <summary>draft | open. Mặc định draft để host chuẩn bị trước khi công bố.</summary>
    public string? Status { get; set; }
}
