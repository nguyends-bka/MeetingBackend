using Microsoft.AspNetCore.Authorization;

namespace MeetingBackend.Attributes;

/// <summary>
/// Custom authorization attribute để kiểm tra role cụ thể
/// </summary>
public class AuthorizeRolesAttribute : AuthorizeAttribute
{
    public AuthorizeRolesAttribute(params string[] roles)
    {
        Roles = string.Join(",", roles);
    }
}
