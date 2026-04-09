using MeetingBackend.Data;
using MeetingBackend.Entities;
using Microsoft.EntityFrameworkCore;

namespace MeetingBackend.Services;

/// <summary>Chủ trì gốc (Meeting.HostIdentity) hoặc đồng chủ trì (MeetingCoHosts).</summary>
public static class MeetingHostAuth
{
    public static bool IsPrimaryHost(Meeting meeting, string userId, string? username)
    {
        var h = meeting.HostIdentity?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(h)) return false;
        var uid = (userId ?? string.Empty).Trim();
        if (string.Equals(h, uid, StringComparison.OrdinalIgnoreCase)) return true;
        if (!string.IsNullOrWhiteSpace(username)
            && string.Equals(h, username.Trim(), StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    public static async Task<bool> IsCoHostAsync(AppDbContext db, Guid meetingId, string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return false;
        var uid = userId.Trim();
        return await db.MeetingCoHosts.AsNoTracking()
            .AnyAsync(c => c.MeetingId == meetingId && c.HostUserId == uid, ct);
    }

    public static async Task<bool> IsAnyHostAsync(AppDbContext db, Meeting meeting, string userId, string? username, CancellationToken ct = default)
    {
        if (IsPrimaryHost(meeting, userId, username)) return true;
        return await IsCoHostAsync(db, meeting.Id, userId, ct);
    }
}
