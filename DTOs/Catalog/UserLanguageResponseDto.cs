namespace MeetingBackend.DTOs.Catalog;

/// <summary>Ngôn ngữ của user (trong response), kèm cờ IsPrimary.</summary>
public class UserLanguageResponseDto
{
    public string Code { get; set; } = string.Empty;
    public string LanguageName { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
}
