using System.ComponentModel.DataAnnotations;

namespace MeetingBackend.Entities
{
    public class User
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public string Username { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        public string Role { get; set; } = "User"; // Admin | User

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
