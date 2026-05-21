namespace MeetingBackend.Entities
{
    /// <summary>
    /// Bảng liên kết nhiều-nhiều User ↔ Language.
    /// Một user có thể biết nhiều ngôn ngữ; đúng 1 ngôn ngữ được đánh dấu IsPrimary = true.
    /// </summary>
    public class UserLanguage
    {
        public Guid UserId { get; set; }
        public string LanguageCode { get; set; } = string.Empty;

        /// <summary>
        /// Ngôn ngữ ưu tiên/chính để dùng cho giao diện, chatbot, email, thông báo...
        /// Mỗi user chỉ nên có đúng 1 bản ghi IsPrimary = true.
        /// </summary>
        public bool IsPrimary { get; set; } = false;

        // Navigations
        public User User { get; set; } = null!;
        public Language Language { get; set; } = null!;
    }
}
