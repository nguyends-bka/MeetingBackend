using Microsoft.AspNetCore.Authorization;

namespace MeetingBackend.Policies;

// Policy-based authorization - business rules, not role names
public static class AuthorizationPolicies
{
    public const string AdminOnly = "AdminOnly";
    public const string UserOrAdmin = "UserOrAdmin";
    public const string MeetingHostOrAdmin = "MeetingHostOrAdmin";

    public static void ConfigurePolicies(AuthorizationOptions options)
    {
        // Admin-only policy - checks if user has Admin role from database
        options.AddPolicy(AdminOnly, policy =>
            policy.Requirements.Add(new RoleRequirement("Admin")));

        // User or Admin policy
        options.AddPolicy(UserOrAdmin, policy =>
            policy.Requirements.Add(new RoleRequirement("User", "Admin")));

        // Meeting host or Admin - checked dynamically in handler
        options.AddPolicy(MeetingHostOrAdmin, policy =>
            policy.Requirements.Add(new MeetingHostRequirement()));
    }
}

// Requirement for role-based authorization
public class RoleRequirement : IAuthorizationRequirement
{
    public string[] AllowedRoles { get; }

    public RoleRequirement(params string[] allowedRoles)
    {
        AllowedRoles = allowedRoles;
    }
}

// Requirement for meeting host authorization
public class MeetingHostRequirement : IAuthorizationRequirement
{
}
