using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MeetingBackend.Options;
using Microsoft.Extensions.Options;

namespace MeetingBackend.Services;

public class LiveKitEgressService
{
    private readonly HttpClient _httpClient;
    private readonly LiveKitTokenService _tokenService;
    private readonly LiveKitOptions _liveKitOptions;

    public LiveKitEgressService(
        HttpClient httpClient,
        LiveKitTokenService tokenService,
        IOptions<LiveKitOptions> liveKitOptions)
    {
        _httpClient = httpClient;
        _tokenService = tokenService;
        _liveKitOptions = liveKitOptions.Value;
    }

    private string ResolveLiveKitApiBaseUrl()
    {
        var raw = (_liveKitOptions.EgressUrl ?? _liveKitOptions.Url ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(raw))
            throw new InvalidOperationException("LiveKit:EgressUrl or LiveKit:Url is not configured");

        if (!raw.Contains("://"))
        {
            raw = $"http://{raw}";
        }

        if (!Uri.TryCreate(raw, UriKind.Absolute, out var parsed))
        {
            throw new InvalidOperationException("LiveKit:EgressUrl (or fallback LiveKit:Url) is invalid");
        }

        var scheme = parsed.Scheme switch
        {
            "wss" => "https",
            "ws" => "http",
            _ => parsed.Scheme
        };

        var builder = new UriBuilder(parsed)
        {
            Scheme = scheme,
            Port = parsed.IsDefaultPort ? -1 : parsed.Port
        };

        return builder.Uri.ToString().TrimEnd('/');
    }

    private async Task<(bool ok, JsonDocument? json, string? error)> PostAsync(
        string method,
        object payload,
        CancellationToken cancellationToken)
    {
        var apiBase = ResolveLiveKitApiBaseUrl();
        var endpoint = $"{apiBase}/twirp/livekit.Egress/{method}";

        var token = _tokenService.CreateEgressToken();
        using var req = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var timeoutSeconds = _liveKitOptions.EgressRequestTimeoutSeconds;
        if (timeoutSeconds < 20) timeoutSeconds = 20;
        if (timeoutSeconds > 300) timeoutSeconds = 300;

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        HttpResponseMessage resp;
        string content;
        try
        {
            resp = await _httpClient.SendAsync(req, timeoutCts.Token);
            content = await resp.Content.ReadAsStringAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return (false, null, $"LiveKit Egress request timed out ({timeoutSeconds}s)");
        }
        catch (HttpRequestException ex)
        {
            return (false, null, $"LiveKit Egress connection failed: {ex.Message}");
        }

        using (resp)
        {
            if (!resp.IsSuccessStatusCode)
            {
                return (false, null, string.IsNullOrWhiteSpace(content)
                    ? $"HTTP {(int)resp.StatusCode}"
                    : content);
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                return (true, null, null);
            }

            try
            {
                return (true, JsonDocument.Parse(content), null);
            }
            catch
            {
                return (true, null, null);
            }
        }
    }

    private async Task<(bool ok, JsonDocument? json, string? error)> PostRoomServiceAsync(
        string method,
        string roomName,
        object payload,
        CancellationToken cancellationToken)
    {
        var apiBase = ResolveLiveKitApiBaseUrl();
        var endpoint = $"{apiBase}/twirp/livekit.RoomService/{method}";

        var token = _tokenService.CreateRoomServiceToken(roomName);
        using var req = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var timeoutSeconds = _liveKitOptions.EgressRequestTimeoutSeconds;
        if (timeoutSeconds < 20) timeoutSeconds = 20;
        if (timeoutSeconds > 300) timeoutSeconds = 300;

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        HttpResponseMessage resp;
        string content;
        try
        {
            resp = await _httpClient.SendAsync(req, timeoutCts.Token);
            content = await resp.Content.ReadAsStringAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return (false, null, $"LiveKit RoomService request timed out ({timeoutSeconds}s)");
        }
        catch (HttpRequestException ex)
        {
            return (false, null, $"LiveKit RoomService connection failed: {ex.Message}");
        }

        using (resp)
        {
            if (!resp.IsSuccessStatusCode)
            {
                return (false, null, string.IsNullOrWhiteSpace(content)
                    ? $"HTTP {(int)resp.StatusCode}"
                    : content);
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                return (true, null, null);
            }

            try
            {
                return (true, JsonDocument.Parse(content), null);
            }
            catch
            {
                return (true, null, null);
            }
        }
    }

    public async Task<(bool ok, bool hasMediaTrack, string? error)> HasActiveMediaTrackAsync(
        string roomName,
        CancellationToken cancellationToken)
    {
        var payload = new { room = roomName };
        var (ok, json, error) = await PostRoomServiceAsync(
            "ListParticipants",
            roomName,
            payload,
            cancellationToken);
        if (!ok)
        {
            return (false, false, error);
        }

        if (json is null)
        {
            return (true, false, null);
        }

        var root = json.RootElement;
        if (!root.TryGetProperty("participants", out var participants)
            || participants.ValueKind != JsonValueKind.Array)
        {
            return (true, false, null);
        }

        foreach (var participant in participants.EnumerateArray())
        {
            if (!participant.TryGetProperty("tracks", out var tracks)
                || tracks.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var track in tracks.EnumerateArray())
            {
                var muted = track.TryGetProperty("muted", out var mutedProp)
                    && mutedProp.ValueKind == JsonValueKind.True;

                if (muted)
                {
                    continue;
                }

                var kind = track.TryGetProperty("type", out var typeProp)
                    ? typeProp.GetString()
                    : null;

                // Ignore data tracks. We need at least one active audio/video track.
                if (!string.Equals(kind, "DATA", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(kind, "KIND_DATA", StringComparison.OrdinalIgnoreCase))
                {
                    return (true, true, null);
                }
            }
        }

        return (true, false, null);
    }

    public async Task<(bool ok, string? egressId, string? error)> StartRoomCompositeRecordingAsync(
        string roomName,
        string outputFilePath,
        CancellationToken cancellationToken)
    {
        var payload = new
        {
            room_name = roomName,
            layout = "grid-dark",
            file_outputs = new[]
            {
                new
                {
                    filepath = outputFilePath
                }
            }
        };

        var (ok, json, error) = await PostAsync("StartRoomCompositeEgress", payload, cancellationToken);
        if (!ok)
        {
            return (false, null, error);
        }

        string? egressId = null;
        if (json is not null)
        {
            var root = json.RootElement;
            if (root.TryGetProperty("egress_id", out var e1) && e1.ValueKind == JsonValueKind.String)
            {
                egressId = e1.GetString();
            }
            else if (root.TryGetProperty("egressId", out var e2) && e2.ValueKind == JsonValueKind.String)
            {
                egressId = e2.GetString();
            }
        }

        if (string.IsNullOrWhiteSpace(egressId))
        {
            return (false, null, "LiveKit did not return egress id");
        }

        return (true, egressId, null);
    }

    public async Task<(bool ok, string? error)> StopEgressAsync(string egressId, CancellationToken cancellationToken)
    {
        var payload = new
        {
            egress_id = egressId
        };

        var (ok, _, error) = await PostAsync("StopEgress", payload, cancellationToken);
        if (!ok)
        {
            return (false, error);
        }

        return (true, null);
    }
}
