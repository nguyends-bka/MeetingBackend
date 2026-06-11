using System.Text;
using System.Text.Json;
using MeetingBackend.Data;
using MeetingBackend.Entities;
using Microsoft.EntityFrameworkCore;

namespace MeetingBackend.Services.Integrations;

public class LlmSummaryResult
{
    public bool IsCompleted { get; set; }
    public string? SummaryText { get; set; }
    public string? JobId { get; set; }
}

public class NonRetryableLlmException : Exception
{
    public NonRetryableLlmException(string message) : base(message) { }
}

public class LlmMinutesSummaryClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<LlmMinutesSummaryClient> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public LlmMinutesSummaryClient(
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger<LlmMinutesSummaryClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Tạo báo cáo tóm tắt cuộc họp thông qua AI (LLM).
    /// </summary>
    public async Task<LlmSummaryResult> GenerateSummaryAsync(Guid meetingId, AppDbContext db, CancellationToken cancellationToken)
    {
        // 1. Biên tập nội dung biên bản hành chính từ Database
        var minutesText = await BuildMinutesTextAsync(meetingId, db);

        // 2. Lấy cấu hình LLM
        var baseUrl = _config["LlmService:BaseUrl"] ?? "https://bkmeeting.soict.io/serverai/meetingminutes";

        // 3. Xây dựng Payload gửi tối giản chỉ gồm requestId và segments theo yêu cầu
        var payload = new
        {
            requestId = meetingId.ToString(),
            segments = minutesText
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        _logger.LogInformation("[AI Summary] → POST {Url}\nPayload:\n{Payload}", baseUrl, json);

        HttpResponseMessage? response = null;
        string responseBody = string.Empty;
        var maxRetries = 4;
        var delayMs = 3000; // Bắt đầu với 3 giây

        for (int retry = 0; retry <= maxRetries; retry++)
        {
            try
            {
                using var requestContent = new StringContent(json, Encoding.UTF8, "application/json");
                var client = _httpClientFactory.CreateClient("LlmSummary");
                response = await client.PostAsync(baseUrl, requestContent, cancellationToken);
                responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    break;
                }

                // Nếu gặp các lỗi tạm thời từ phía LLM (503, 504, 500, 429, 408) và chưa quá số lần thử lại
                var statusCodeInt = (int)response.StatusCode;
                if ((statusCodeInt == 503 || statusCodeInt == 504 || statusCodeInt == 500 || 
                     statusCodeInt == 429 || statusCodeInt == 408) && retry < maxRetries)
                {
                    _logger.LogWarning("[AI Summary] Gặp lỗi tạm thời {StatusCode} (Cố gắng thử lại lần {Retry}/{Max} sau {Delay}ms...)", 
                        response.StatusCode, retry + 1, maxRetries, delayMs);
                    await Task.Delay(delayMs, cancellationToken);
                    delayMs *= 2; // Tăng gấp đôi thời gian chờ (Exponential backoff)
                    continue;
                }

                _logger.LogError("[AI Summary] Gọi LLM thất bại. Mã lỗi: {StatusCode}. Chi tiết: {Body}", response.StatusCode, responseBody);
                throw new NonRetryableLlmException($"LLM API returned status code {response.StatusCode}: {responseBody}");
            }
            catch (Exception ex) when (retry < maxRetries && ex is not OperationCanceledException && ex is not NonRetryableLlmException)
            {
                _logger.LogWarning(ex, "[AI Summary] Lỗi kết nối khi gọi LLM. Đang tự động thử lại lần {Retry}/{Max} sau {Delay}ms...", 
                    retry + 1, maxRetries, delayMs);
                await Task.Delay(delayMs, cancellationToken);
                delayMs *= 2;
            }
        }

        // 4. Chế độ Webhook bất đồng bộ: bóc tách jobId từ responseBody nhận nhanh của Custom LLM
        string? jobId = null;
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            if (root.TryGetProperty("requestId", out var rProp))
            {
                jobId = rProp.GetString();
            }
            else if (root.TryGetProperty("id", out var idProp))
            {
                jobId = idProp.GetString();
            }
            else if (root.TryGetProperty("jobId", out var jProp))
            {
                jobId = jProp.GetString();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[AI Summary] Không thể parse JSON từ phản hồi nhanh của Custom LLM để tìm ID. Sẽ tự sinh ID.");
        }

        if (string.IsNullOrWhiteSpace(jobId))
        {
            jobId = Guid.NewGuid().ToString();
        }

        _logger.LogInformation("[AI Summary] Đã gửi yêu cầu thành công tới Custom LLM. Nhận Job ID: {JobId}. Phản hồi thô: {Body}", jobId, responseBody);
        return new LlmSummaryResult
        {
            IsCompleted = false,
            JobId = jobId
        };
    }

    /// <summary>
    /// Xây dựng biên bản cuộc họp thô định dạng hành chính.
    /// </summary>
    private async Task<string> BuildMinutesTextAsync(Guid meetingId, AppDbContext db)
    {
        var meeting = await db.Meetings.AsNoTracking().FirstOrDefaultAsync(m => m.Id == meetingId);
        if (meeting == null) return string.Empty;

        // Tên chủ trì
        var hostDisplayName = meeting.HostName;
        var hostIdentity = meeting.HostIdentity?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(hostIdentity))
        {
            var hostUser = await db.Users.AsNoTracking()
                .Where(u => u.Id.ToString() == hostIdentity || u.Username.ToLower() == hostIdentity.ToLower())
                .Select(u => new { u.FullName, u.Username })
                .FirstOrDefaultAsync();
            if (hostUser != null)
            {
                hostDisplayName = string.IsNullOrWhiteSpace(hostUser.FullName) ? hostUser.Username : hostUser.FullName;
            }
        }

        // Thành viên tham dự
        var participantsRaw = await db.MeetingParticipants.AsNoTracking()
            .Where(p => p.MeetingId == meetingId)
            .OrderBy(p => p.JoinedAt)
            .ToListAsync();

        var userIds = participantsRaw.Select(p => p.UserId.Trim()).Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList();
        var userLookup = await db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id.ToString()))
            .Select(u => new { Id = u.Id.ToString(), Name = string.IsNullOrWhiteSpace(u.FullName) ? u.Username : u.FullName })
            .ToDictionaryAsync(x => x.Id, x => x.Name, StringComparer.OrdinalIgnoreCase);

        var distinctParticipants = new HashSet<string>();
        foreach (var p in participantsRaw)
        {
            var uid = p.UserId.Trim();
            if (string.IsNullOrEmpty(uid)) continue;
            var displayName = userLookup.TryGetValue(uid, out var name) ? name : (string.IsNullOrWhiteSpace(p.Username) ? uid : p.Username);
            distinctParticipants.Add(displayName);
        }

        // Transcript
        var transcriptRaw = await db.MeetingTranscriptEntries.AsNoTracking()
            .Where(x => x.MeetingId == meetingId)
            .OrderBy(x => x.AtUtc)
            .ToListAsync();

        var transcriptIds = transcriptRaw.Select(x => x.SpeakerIdentity.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
        var transcriptUserLookup = await db.Users.AsNoTracking()
            .Where(u => transcriptIds.Contains(u.Id.ToString()) || transcriptIds.Contains(u.Username))
            .Select(u => new { Id = u.Id.ToString(), u.Username, Name = string.IsNullOrWhiteSpace(u.FullName) ? u.Username : u.FullName })
            .ToListAsync();

        string ResolveSpeakerName(string iden)
        {
            var match = transcriptUserLookup.FirstOrDefault(x => x.Id.Equals(iden, StringComparison.OrdinalIgnoreCase) || x.Username.Equals(iden, StringComparison.OrdinalIgnoreCase));
            return match != null ? match.Name : iden;
        }

        // Polls
        var polls = await db.MeetingPolls.AsNoTracking()
            .Where(p => p.MeetingId == meetingId && p.Status != "draft")
            .Include(p => p.Votes)
            .OrderBy(p => p.CreatedAtUtc)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM");
        sb.AppendLine("Độc lập - Tự do - Hạnh phúc");
        sb.AppendLine("--------------------");
        sb.AppendLine();
        var signAt = meeting.EndedAt ?? meeting.StartedAt ?? DateTime.UtcNow;
        sb.AppendLine($"Hà Nội, ngày {signAt.AddHours(7):dd} tháng {signAt.AddHours(7):MM} năm {signAt.AddHours(7):yyyy}");
        sb.AppendLine();
        sb.AppendLine("BIÊN BẢN CUỘC HỌP GỐC");
        sb.AppendLine($"Biên bản cuộc họp về: {meeting.Title}");
        sb.AppendLine();
        sb.AppendLine("1. Thời gian, địa điểm");
        sb.AppendLine($"- Bắt đầu lúc: {meeting.StartedAt?.AddHours(7):HH:mm:ss dd/MM/yyyy}");
        sb.AppendLine($"- Kết thúc thực tế: {meeting.EndedAt?.AddHours(7):HH:mm:ss dd/MM/yyyy}");
        sb.AppendLine($"- Địa điểm: Phòng họp trực tuyến (Mã: {meeting.MeetingCode})");
        sb.AppendLine();
        sb.AppendLine("2. Thành phần tham dự");
        sb.AppendLine($"- Chủ trì: {hostDisplayName}");
        sb.AppendLine($"- Số lượng thành viên: {distinctParticipants.Count}");
        foreach (var p in distinctParticipants)
        {
            sb.AppendLine($"   • {p}");
        }
        sb.AppendLine();
        sb.AppendLine("3. Tiến trình cuộc họp (Hội thoại đối thoại)");
        if (transcriptRaw.Count == 0)
        {
            sb.AppendLine("(Không có transcript)");
        }
        else
        {
            foreach (var t in transcriptRaw)
            {
                sb.AppendLine($"[{t.AtUtc.AddHours(7):HH:mm:ss}] - {ResolveSpeakerName(t.SpeakerIdentity)}: \"{t.Text}\"");
            }
        }
        sb.AppendLine();

        if (polls.Count > 0)
        {
            sb.AppendLine("4. Kết quả biểu quyết");
            for (var i = 0; i < polls.Count; i++)
            {
                var poll = polls[i];
                sb.AppendLine($"- Biểu quyết {i + 1}. {poll.Title} ({poll.Status})");
                string[] options;
                try { options = JsonSerializer.Deserialize<string[]>(poll.OptionsJson, JsonOpts) ?? Array.Empty<string>(); }
                catch { options = Array.Empty<string>(); }

                var voteCounts = new int[options.Length];
                foreach (var vote in poll.Votes)
                {
                    int[] idxs;
                    try { idxs = JsonSerializer.Deserialize<int[]>(vote.OptionIndicesJson, JsonOpts) ?? Array.Empty<int>(); }
                    catch { continue; }
                    foreach (var idx in idxs)
                    {
                        if (idx >= 0 && idx < options.Length) voteCounts[idx]++;
                    }
                }

                for (var j = 0; j < options.Length; j++)
                {
                    sb.AppendLine($"   • {options[j]}: {voteCounts[j]} phiếu");
                }
            }
            sb.AppendLine();
        }

        sb.AppendLine($"{(polls.Count > 0 ? "5" : "4")}. Kết thúc cuộc họp");
        return sb.ToString();
    }
}
