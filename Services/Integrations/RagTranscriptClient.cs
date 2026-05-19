using System.Text;
using System.Text.Json;

namespace MeetingBackend.Services.Integrations;

/// <summary>
/// Gửi bản ghi transcript lên RAG service sau khi lưu vào database.
/// </summary>
public class RagTranscriptClient
{
    private const string RagTranscriptUrl = "https://rag.soictlab.com/transcript";
    // private const string RagTranscriptUrl = "http://localhost:3001";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<RagTranscriptClient> _logger;

    public RagTranscriptClient(IHttpClientFactory httpClientFactory, ILogger<RagTranscriptClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Gửi một transcript entry lên RAG endpoint (fire-and-forget – không throw exception ra ngoài).
    /// </summary>
    /// <param name="meetingId">ID cuộc họp</param>
    /// <param name="speakerName">Họ và tên người nói</param>
    /// <param name="atUtc">Thời gian nói (UTC)</param>
    /// <param name="text">Nội dung transcript</param>
    public async Task SendAsync(Guid meetingId, string speakerName, DateTime atUtc, string text)
    {
        try
        {
            var payload = new
            {
                //meeting_id = meetingId.ToString(),
                collection = $"meeting-{meetingId}",
                speaker_name = speakerName,
                at = new DateTimeOffset(DateTime.SpecifyKind(atUtc, DateTimeKind.Utc))
                         .ToOffset(TimeSpan.FromHours(7))
                         .ToString("yyyy-MM-ddTHH:mm:sszzz"),  // ISO 8601: 2026-05-18T15:54:54+07:00
                text = text
            };

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            // ── LOG PAYLOAD TRƯỚC KHI GỬI ──────────────────────────────────────
            _logger.LogInformation(
                "[RAG Transcript] → POST {Url}\n{Payload}",
                RagTranscriptUrl, json);

            var client = _httpClientFactory.CreateClient("RagTranscript");
            using var response = await client.PostAsync(RagTranscriptUrl, content);

            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "[RAG Transcript] ✗ {StatusCode} ← {Url}\nResponse: {Body}",
                    (int)response.StatusCode, RagTranscriptUrl, responseBody);
            }
            else
            {
                _logger.LogInformation(
                    "[RAG Transcript] ✓ {StatusCode} ← {Url}\nResponse: {Body}",
                    (int)response.StatusCode, RagTranscriptUrl, responseBody);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[RAG Transcript] ✗ Exception khi gửi tới {Url}. MeetingId={MeetingId}",
                RagTranscriptUrl, meetingId);
        }
    }
}
