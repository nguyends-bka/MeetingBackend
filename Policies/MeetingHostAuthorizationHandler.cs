using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using MeetingBackend.Data;
using System.Security.Claims;

namespace MeetingBackend.Policies;

// Dynamic meeting host authorization
public class MeetingHostAuthorizationHandler : AuthorizationHandler<MeetingHostRequirement>
{
    private readonly AppDbContext _db;

    public MeetingHostAuthorizationHandler(AppDbContext db)
    {
        _db = db;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        MeetingHostRequirement requirement)
    {
        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)
                         ?? context.User.FindFirst(ClaimTypes.Name);

        if (userIdClaim == null)
        {
            return;
        }

        var userId = userIdClaim.Value;

        // Check if user is Admin (from database)
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id.ToString() == userId);

        if (user != null && user.Role == "Admin")
        {
            context.Succeed(requirement);
            return;
        }

        // Check if user is meeting host (requires meetingId in route/query)
        var httpContext = context.Resource as Microsoft.AspNetCore.Http.HttpContext;
        if (httpContext != null)
        {
            var meetingIdParam = httpContext.Request.RouteValues["meetingId"]?.ToString();
            if (Guid.TryParse(meetingIdParam, out var meetingId))
            {
                var meeting = await _db.Meetings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m => m.Id == meetingId);

                if (meeting != null && meeting.HostIdentity == userId)
                {
                    context.Succeed(requirement);
                }
            }
        }
    }
}
