using MeetingBackend.DTOs.Auth;
using MeetingBackend.DTOs.User;
using MeetingBackend.Entities;

namespace MeetingBackend.Mappers;

// Entity ↔ DTO mapping layer
public static class UserMapper
{
    public static AuthUserDto ToAuthUserDto(User user)
    {
        return new AuthUserDto
        {
            Id = user.Id,
            Username = user.Username,
            FullName = user.FullName ?? string.Empty,
            Role = user.Role,
            Position = user.Position,
            AcademicRank = user.AcademicRank,
            AcademicDegree = user.AcademicDegree,
            OrganizationUnitId = user.OrganizationUnitId,
            FaceTemplate = user.FaceTemplate
        };
    }

    public static UserProfileResponseDto ToUserProfileDto(User user, string? organizationUnitName = null)
    {
        return new UserProfileResponseDto
        {
            Id = user.Id,
            Username = user.Username,
            Role = user.Role,
            FullName = user.FullName,
            Email = user.Email,
            Position = user.Position,
            AcademicRank = user.AcademicRank,
            AcademicDegree = user.AcademicDegree,
            OrganizationUnitId = user.OrganizationUnitId,
            OrganizationUnitName = organizationUnitName,
            FaceTemplate = user.FaceTemplate,
            CreatedAt = user.CreatedAt
        };
    }

    public static UserDto ToUserDto(User user, string? organizationUnitName = null)
    {
        return new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            Role = user.Role,
            FullName = user.FullName,
            Email = user.Email,
            Position = user.Position,
            AcademicRank = user.AcademicRank,
            AcademicDegree = user.AcademicDegree,
            OrganizationUnitId = user.OrganizationUnitId,
            OrganizationUnitName = organizationUnitName,
            FaceTemplate = user.FaceTemplate
        };
    }
}
