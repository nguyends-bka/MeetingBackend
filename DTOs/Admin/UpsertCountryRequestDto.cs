using System.ComponentModel.DataAnnotations;

namespace MeetingBackend.DTOs.Admin;

public class UpsertCountryRequestDto
{
    [Required]
    [MaxLength(10)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string CountryName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
