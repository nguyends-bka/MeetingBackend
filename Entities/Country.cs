using System.ComponentModel.DataAnnotations;

namespace MeetingBackend.Entities
{
    public class Country
    {
        /// <summary>Mã quốc gia theo ISO 3166-1 alpha-2, ví dụ: VN, US, JP</summary>
        [Key]
        [MaxLength(10)]
        public string Code { get; set; } = string.Empty;

        /// <summary>Tên quốc gia hiển thị, ví dụ: Việt Nam, United States</summary>
        [Required]
        [MaxLength(100)]
        public string CountryName { get; set; } = string.Empty;

        /// <summary>Trạng thái hoạt động. False = ẩn khỏi dropdown, không cho user mới chọn.</summary>
        public bool IsActive { get; set; } = true;

        // Navigation
        public ICollection<UserCountry> UserCountries { get; set; } = [];
    }
}
