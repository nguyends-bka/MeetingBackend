using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using MeetingBackend.Data;
using System.Security.Claims;

namespace MeetingBackend.Policies;

// Dynamic role authorization - loads role from database
public class RoleAuthorizationHandler : AuthorizationHandler<RoleRequirement>
{
    private readonly AppDbContext _db;

    public RoleAuthorizationHandler(AppDbContext db)
    {
        _db = db;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        RoleRequirement requirement)
    {
        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)
                         ?? context.User.FindFirst(ClaimTypes.Name);

        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return;
        }

        // Load role from database dynamically
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            return;
        }

        // Check if user's role matches requirement
        if (requirement.AllowedRoles.Contains(user.Role))
        {
            context.Succeed(requirement);
        }
    }
}
