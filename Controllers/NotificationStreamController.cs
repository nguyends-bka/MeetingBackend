using System.Security.Claims;
using MeetingBackend.Services.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MeetingBackend.Controllers;

[ApiController]
[Route("api/notifications")]
public class NotificationStreamController : ControllerBase
{
    private readonly INotificationBroadcaster _broadcaster;
    private readonly ILogger<NotificationStreamController> _logger;

    public NotificationStreamController(
        INotificationBroadcaster broadcaster,
        ILogger<NotificationStreamController> logger)
    {
        _broadcaster = broadcaster;
        _logger = logger;
    }

    /// <summary>
    /// Kênh SSE real-time: giữ kết nối mở, server đẩy sự kiện khi có thông báo mới.
    /// Client dùng EventSource. Token truyền qua query (?token=) vì EventSource
    /// không đặt được header Authorization.
    /// </summary>
    [HttpGet("stream")]
    [Authorize]
    public async Task Stream(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue(ClaimTypes.Name);
        if (string.IsNullOrEmpty(userId))
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";
        // Tắt buffering của reverse proxy (nginx) để sự kiện tới ngay lập tức.
        Response.Headers["X-Accel-Buffering"] = "no";

        var connection = _broadcaster.Subscribe(userId);

        try
        {
            // Gửi một comment mở màn để client biết kết nối đã sẵn sàng.
            await Response.WriteAsync(": connected\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);

            // Heartbeat định kỳ để giữ kết nối sống qua proxy/idle timeout.
            using var heartbeat = new PeriodicTimer(TimeSpan.FromSeconds(25));
            var heartbeatTask = SendHeartbeatAsync(heartbeat, cancellationToken);

            await foreach (var message in connection.Reader.ReadAllAsync(cancellationToken))
            {
                await Response.WriteAsync(message, cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Client ngắt kết nối — bình thường.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SSE stream lỗi cho user {UserId}", userId);
        }
        finally
        {
            _broadcaster.Unsubscribe(userId, connection);
        }
    }

    private async Task SendHeartbeatAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                await Response.WriteAsync(": ping\n\n", ct);
                await Response.Body.FlushAsync(ct);
            }
        }
        catch (OperationCanceledException) { /* kết thúc bình thường */ }
        catch { /* bỏ qua lỗi heartbeat */ }
    }
}
