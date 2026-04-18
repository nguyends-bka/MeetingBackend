using System.Text;
using Jose;
using Microsoft.Extensions.Options;
using MeetingBackend.Options;

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
        /// <param name="name">Tên hiển thị trong chat (ví dụ: username)</param>
        /// <returns>JWT token</returns>
        public string CreateToken(string room, string identity, string? name = null)
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

            // Tên hiển thị trong chat (Participant.name)
            if (!string.IsNullOrWhiteSpace(name))
            {
                payload["name"] = name.Trim();
            }

            return JWT.Encode(
                payload,
                Encoding.UTF8.GetBytes(_options.ApiSecret),
                JwsAlgorithm.HS256
            );
        }

        /// <summary>
        /// Tạo token cho backend gọi Egress API (start/stop recording).
        /// </summary>
        public string CreateEgressToken(string identity = "backend-egress")
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var payload = new Dictionary<string, object>
            {
                { "iss", _options.ApiKey },
                { "sub", identity },
                { "nbf", now },
                { "exp", now + 3600 },
                {
                    "video", new
                    {
                        roomRecord = true,
                        roomAdmin = true,
                        roomList = true,
                        roomCreate = true,
                    }
                }
            };

            return JWT.Encode(
                payload,
                Encoding.UTF8.GetBytes(_options.ApiSecret),
                JwsAlgorithm.HS256
            );
        }

        /// <summary>
        /// Tạo token backend để gọi RoomService (ListParticipants/ListRooms...).
        /// Token được scope theo room để quyền roomAdmin có hiệu lực.
        /// </summary>
        public string CreateRoomServiceToken(string room, string identity = "backend-roomservice")
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var payload = new Dictionary<string, object>
            {
                { "iss", _options.ApiKey },
                { "sub", identity },
                { "nbf", now },
                { "exp", now + 3600 },
                {
                    "video", new
                    {
                        roomAdmin = true,
                        roomList = true,
                        room = room,
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
