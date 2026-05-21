namespace MeetingBackend.DTOs.Catalog;

/// <summary>Quốc tịch/quốc gia của user (trong response).</summary>
public class UserCountryResponseDto
{
    public string Code { get; set; } = string.Empty;
    public string CountryName { get; set; } = string.Empty;
}
