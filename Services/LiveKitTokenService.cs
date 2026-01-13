using System.Text;
using Jose;
using Microsoft.Extensions.Options;
using MeetingBackend.Models;

namespace MeetingBackend.Services;

public class LiveKitTokenService
{
    private readonly LiveKitOptions _options;

    public LiveKitTokenService(IOptions<LiveKitOptions> options)
    {
        _options = options.Value;
    }

    public string CreateToken(string room, string identity)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var payload = new Dictionary<string, object>
        {
            { "iss", _options.ApiKey },
            { "sub", identity },
            { "nbf", now },
            { "exp", now + 3600 }, // 1 giờ
            {
                "video", new
                {
                    roomJoin = true,
                    room = room,
                    canPublish = true,
                    canSubscribe = true,
                    canPublishData = true
                }
            }
        };

        return JWT.Encode(
            payload,
            Encoding.UTF8.GetBytes(_options.ApiSecret),
            JwsAlgorithm.HS256
        );
    }
}
