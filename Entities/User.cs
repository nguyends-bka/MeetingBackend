using System.ComponentModel.DataAnnotations;
using MeetingBackend.Constants;

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
        public string Role { get; set; } = Roles.User; // Admin | User

        public string? FullName { get; set; } // Họ và tên

        public string? Email { get; set; } // Email

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
