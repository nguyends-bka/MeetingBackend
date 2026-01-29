using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace MeetingBackend.Middleware;

/// <summary>
/// WebSocket endpoint for virtual mic: receives PCM16 audio from frontend.
/// Protocol: first text message = JSON config { sampleRate, channels }, then binary = raw PCM16 (16-bit LE).
/// </summary>
public class VirtualMicWebSocketMiddleware
{
    private static readonly ConcurrentDictionary<string, List<WebSocket>> RoomSockets = new();
    private const int DefaultBufferSize = 4096;

    private readonly RequestDelegate _next;

    public VirtualMicWebSocketMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path.Value ?? "";
        if (!path.StartsWith("/ws/virtual-mic", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var meetingId = context.Request.Query["meetingId"].FirstOrDefault() ?? "default";
        var token = context.Request.Query["token"].FirstOrDefault();

        // Optional: validate JWT here if required
        // if (string.IsNullOrEmpty(token)) { context.Response.StatusCode = 401; return; }

        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        var socketId = Guid.NewGuid().ToString("N")[..8];

        AddToRoom(meetingId, webSocket);

        try
        {
            int? sampleRate = null;
            int? channels = null;

            var buffer = new byte[DefaultBufferSize * 4];
            var receiveBuffer = new List<byte>();

            while (webSocket.State == WebSocketState.Open)
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), context.RequestAborted);

                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var json = Encoding.UTF8.GetString(buffer.AsSpan(0, result.Count));
                    try
                    {
                        var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("sampleRate", out var sr))
                            sampleRate = sr.GetInt32();
                        if (doc.RootElement.TryGetProperty("channels", out var ch))
                            channels = ch.GetInt32();
                    }
                    catch
                    {
                        // ignore invalid config
                    }
                    continue;
                }

                if (result.MessageType == WebSocketMessageType.Binary)
                {
                    // Raw PCM16 chunk received
                    var chunkSize = result.Count;
                    if (chunkSize > 0)
                    {
                        // Optional: forward to other clients in same room, or process/store
                        await BroadcastPcmToRoomAsync(meetingId, webSocket, buffer.AsMemory(0, chunkSize));
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Connection closed
        }
        finally
        {
            RemoveFromRoom(meetingId, webSocket);
            if (webSocket.State == WebSocketState.Open || webSocket.State == WebSocketState.CloseSent)
            {
                try
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Bye", context.RequestAborted);
                }
                catch
                {
                    // ignore
                }
            }
            webSocket.Dispose();
        }
    }

    private static void AddToRoom(string meetingId, WebSocket ws)
    {
        RoomSockets.AddOrUpdate(
            meetingId,
            _ => new List<WebSocket> { ws },
            (_, list) =>
            {
                lock (list) list.Add(ws);
                return list;
            });
    }

    private static void RemoveFromRoom(string meetingId, WebSocket ws)
    {
        if (!RoomSockets.TryGetValue(meetingId, out var list))
            return;
        lock (list)
        {
            list.RemoveAll(w => w == ws);
            if (list.Count == 0)
                RoomSockets.TryRemove(meetingId, out _);
        }
    }

    private static async Task BroadcastPcmToRoomAsync(string meetingId, WebSocket sender, ReadOnlyMemory<byte> pcmChunk)
    {
        if (!RoomSockets.TryGetValue(meetingId, out var list))
            return;

        List<WebSocket> copy;
        lock (list)
        {
            copy = list.Where(ws => ws != sender && ws.State == WebSocketState.Open).ToList();
        }

        foreach (var ws in copy)
        {
            try
            {
                await ws.SendAsync(
                    new ArraySegment<byte>(pcmChunk.ToArray()),
                    WebSocketMessageType.Binary,
                    true,
                    CancellationToken.None);
            }
            catch
            {
                // skip failed client
            }
        }
    }
}
