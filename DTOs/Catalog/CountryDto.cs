namespace MeetingBackend.DTOs.Catalog;

/// <summary>Thông tin một quốc gia trong dropdown (chỉ dùng cho catalog API, không kèm user).</summary>
public class CountryDto
{
    public string Code { get; set; } = string.Empty;
    public string CountryName { get; set; } = string.Empty;
}
