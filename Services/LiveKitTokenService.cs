using System.Text;
using Jose;
using Microsoft.Extensions.Options;
using MeetingBackend.Models;

namespace MeetingBackend.Services
{
    public class LiveKitTokenService
    {
        private readonly LiveKitOptions _options;

        public LiveKitTokenService(IOptions<LiveKitOptions> options)
        {
            _options = options.Value;
        }

        /// <summary>
        /// Tạo LiveKit access token cho user join room
        /// </summary>
        /// <param name="room">RoomName (LiveKit room)</param>
        /// <param name="identity">Identity của user (unique)</param>
        /// <returns>JWT token</returns>
        public string CreateToken(string room, string identity)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var payload = new Dictionary<string, object>
            {
                // LiveKit yêu cầu
                { "iss", _options.ApiKey },     // API Key
                { "sub", identity },            // User identity
                { "nbf", now },                 // Not before
                { "exp", now + 3600 },           // Hết hạn sau 1 giờ

                // Quyền video/audio (LiveKit spec)
                {
                    "video", new
                    {
                        roomJoin = true,        // Cho phép join room
                        room = room,            // Tên room
                        canPublish = true,      // Bật mic/cam
                        canSubscribe = true,    // Xem người khác
                        canPublishData = true   // Data channel (chat, signal)
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
}
