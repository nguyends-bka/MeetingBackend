using System.Collections.Concurrent;

namespace MeetingBackend.Services.Infrastructure;

/// <summary>
/// Quản lý các kết nối SSE (Server-Sent Events) theo từng user và đẩy sự kiện
/// thông báo real-time xuống trình duyệt. Singleton, thread-safe.
/// </summary>
public interface INotificationBroadcaster
{
    /// <summary>Đăng ký một kết nối SSE mới cho user. Trả về channel để controller đọc và ghi ra response.</summary>
    NotificationConnection Subscribe(string userId);

    /// <summary>Hủy đăng ký khi client ngắt kết nối.</summary>
    void Unsubscribe(string userId, NotificationConnection connection);

    /// <summary>Đẩy một sự kiện tới tất cả kết nối đang mở của user.</summary>
    Task PushAsync(string userId, string eventName, string data);
}

/// <summary>Một kết nối SSE, dùng Channel để chuyển message từ broadcaster sang controller.</summary>
public sealed class NotificationConnection
{
    private readonly System.Threading.Channels.Channel<string> _channel =
        System.Threading.Channels.Channel.CreateUnbounded<string>(
            new System.Threading.Channels.UnboundedChannelOptions { SingleReader = true });

    public System.Threading.Channels.ChannelReader<string> Reader => _channel.Reader;

    public bool TryWrite(string message) => _channel.Writer.TryWrite(message);

    public void Complete() => _channel.Writer.TryComplete();
}

public sealed class NotificationBroadcaster : INotificationBroadcaster
{
    // userId -> tập các kết nối đang mở (một user có thể mở nhiều tab)
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<NotificationConnection, byte>> _connections = new();
    private readonly ILogger<NotificationBroadcaster> _logger;

    public NotificationBroadcaster(ILogger<NotificationBroadcaster> logger)
    {
        _logger = logger;
    }

    public NotificationConnection Subscribe(string userId)
    {
        var conn = new NotificationConnection();
        var set = _connections.GetOrAdd(userId, _ => new ConcurrentDictionary<NotificationConnection, byte>());
        set.TryAdd(conn, 0);
        return conn;
    }

    public void Unsubscribe(string userId, NotificationConnection connection)
    {
        if (_connections.TryGetValue(userId, out var set))
        {
            set.TryRemove(connection, out _);
            connection.Complete();
            if (set.IsEmpty)
                _connections.TryRemove(userId, out _);
        }
    }

    public Task PushAsync(string userId, string eventName, string data)
    {
        if (string.IsNullOrEmpty(userId)) return Task.CompletedTask;
        if (!_connections.TryGetValue(userId, out var set) || set.IsEmpty)
            return Task.CompletedTask;

        // Định dạng SSE chuẩn: "event: <name>\n" + "data: <json>\n\n"
        var frame = $"event: {eventName}\ndata: {data}\n\n";
        foreach (var conn in set.Keys)
        {
            try { conn.TryWrite(frame); }
            catch (Exception ex) { _logger.LogWarning(ex, "Đẩy SSE tới user {UserId} thất bại", userId); }
        }
        return Task.CompletedTask;
    }
}
