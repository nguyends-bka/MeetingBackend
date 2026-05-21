namespace MeetingBackend.DTOs.Catalog;

/// <summary>Thông tin một ngôn ngữ trong dropdown (chỉ dùng cho catalog API, không kèm user).</summary>
public class LanguageDto
{
    public string Code { get; set; } = string.Empty;
    public string LanguageName { get; set; } = string.Empty;
}
