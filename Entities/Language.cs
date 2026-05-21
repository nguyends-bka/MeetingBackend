using System.ComponentModel.DataAnnotations;

namespace MeetingBackend.Entities
{
    public class Language
    {
        /// <summary>Mã ngôn ngữ theo BCP 47 / ISO 639-1, ví dụ: vi, en, ja</summary>
        [Key]
        [MaxLength(10)]
        public string Code { get; set; } = string.Empty;

        /// <summary>Tên ngôn ngữ hiển thị, ví dụ: Tiếng Việt, English, Japanese</summary>
        [Required]
        [MaxLength(100)]
        public string LanguageName { get; set; } = string.Empty;

        /// <summary>Trạng thái hoạt động. False = ẩn khỏi dropdown, không cho user mới chọn.</summary>
        public bool IsActive { get; set; } = true;

        // Navigation
        public ICollection<UserLanguage> UserLanguages { get; set; } = [];
    }
}
