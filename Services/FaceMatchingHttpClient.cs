using System.Text;
using System.Text.Json;
using MeetingBackend.Models;
using Microsoft.Extensions.Options;

namespace MeetingBackend.Services;

public class FaceMatchingHttpClient : IFaceMatchingClient
{
    private readonly HttpClient _httpClient;
    private readonly FaceMatchingOptions _options;
    private readonly ILogger<FaceMatchingHttpClient> _logger;

    public FaceMatchingHttpClient(
        HttpClient httpClient,
        IOptions<FaceMatchingOptions> options,
        ILogger<FaceMatchingHttpClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<float> ComputeSimilarityAsync(byte[] template1, byte[] template2, CancellationToken cancellationToken = default)
    {
        if (template1.Length == 0 || template2.Length == 0) return 0;

        try
        {
            var payload = new
            {
                template1 = template1.Select(b => (int)(unchecked((sbyte)b))).ToArray(),
                template2 = template2.Select(b => (int)(unchecked((sbyte)b))).ToArray()
            };

            var json = JsonSerializer.Serialize(payload);

            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync(_options.Url, content, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            using var doc = JsonDocument.Parse(responseText);
            var root = doc.RootElement;

            if (root.TryGetProperty("success", out var successProp)
                && successProp.ValueKind == JsonValueKind.True
                && root.TryGetProperty("similarity", out var similarityProp)
                && similarityProp.ValueKind == JsonValueKind.Number
                && similarityProp.TryGetSingle(out var similarity))
            {
                return similarity;
            }

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Face matching request failed");
            return 0;
        }
    }
}
