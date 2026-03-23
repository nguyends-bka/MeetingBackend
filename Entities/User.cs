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

        public string? Position { get; set; } // Chức vụ

        public string? AcademicRank { get; set; } // GS | PGS

        public string? AcademicDegree { get; set; } // TS | ThS | CN | KS

        public Guid? OrganizationUnitId { get; set; }

        public string? FaceTemplate { get; set; } // Chuỗi template khuôn mặt (<= 512 byte)

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
