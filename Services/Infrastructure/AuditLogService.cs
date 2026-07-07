using System.Security.Claims;
using MeetingBackend.Data;
using MeetingBackend.Entities;
using Microsoft.AspNetCore.Http;

namespace MeetingBackend.Services.Infrastructure;

public interface IAuditLogService
{
    /// <summary>
    /// Ghi một dòng nhật ký hệ thống. Không bao giờ ném lỗi ra ngoài để tránh
    /// ảnh hưởng luồng nghiệp vụ chính.
    /// </summary>
    Task LogAsync(
        string category,
        string action,
        string message,
        ClaimsPrincipal? actor = null,
        string? targetId = null,
        string? targetLabel = null,
        string severity = "info",
        CancellationToken cancellationToken = default);
}

public sealed class AuditLogService : IAuditLogService
{
    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuditLogService> _logger;

    public AuditLogService(
        AppDbContext db,
        IHttpContextAccessor httpContextAccessor,
        ILogger<AuditLogService> logger)
    {
        _db = db;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task LogAsync(
        string category,
        string action,
        string message,
        ClaimsPrincipal? actor = null,
        string? targetId = null,
        string? targetLabel = null,
        string severity = "info",
        CancellationToken cancellationToken = default)
    {
        try
        {
            Guid? actorId = null;
            string? actorName = null;
            if (actor != null)
            {
                var idRaw = actor.FindFirstValue(ClaimTypes.NameIdentifier);
                if (Guid.TryParse(idRaw, out var g)) actorId = g;
                actorName = actor.FindFirstValue("fullName")
                    ?? actor.FindFirstValue("username")
                    ?? actor.FindFirstValue(ClaimTypes.Name);
            }

            var ip = _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString();

            _db.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                Category = category,
                Action = action,
                Severity = string.IsNullOrWhiteSpace(severity) ? "info" : severity,
                ActorUserId = actorId,
                ActorName = actorName,
                TargetId = targetId,
                TargetLabel = targetLabel,
                Message = message,
                IpAddress = ip,
                CreatedAtUtc = DateTime.UtcNow,
            });

            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Nhật ký lỗi không được làm hỏng nghiệp vụ chính.
            _logger.LogWarning(ex, "Ghi audit log thất bại: {Category}/{Action}", category, action);
        }
    }
}
