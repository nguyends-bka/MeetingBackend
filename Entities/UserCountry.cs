namespace MeetingBackend.Entities
{
    /// <summary>
    /// Bảng liên kết nhiều-nhiều User ↔ Country.
    /// Một user có thể thuộc nhiều quốc tịch/quốc gia; không cần IsPrimary.
    /// </summary>
    public class UserCountry
    {
        public Guid UserId { get; set; }
        public string CountryCode { get; set; } = string.Empty;

        // Navigations
        public User User { get; set; } = null!;
        public Country Country { get; set; } = null!;
    }
}
