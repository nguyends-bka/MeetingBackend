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
            Role = user.Role
        };
    }

    public static UserProfileResponseDto ToUserProfileDto(User user)
    {
        return new UserProfileResponseDto
        {
            Id = user.Id,
            Username = user.Username,
            Role = user.Role,
            FullName = user.FullName,
            Email = user.Email,
            CreatedAt = user.CreatedAt
        };
    }

    public static UserDto ToUserDto(User user)
    {
        return new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            Role = user.Role,
            FullName = user.FullName,
            Email = user.Email
        };
    }
}
