using System.Security.Claims;
using System.Text.Json;
using MeetingBackend.Data;
using MeetingBackend.DTOs.Meeting;
using MeetingBackend.Entities;
using MeetingBackend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MeetingBackend.Controllers;

[ApiController]
[Route("api/meeting/{meetingId:guid}/minutes")]
[Authorize]
public class MeetingMinutesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IBackgroundTaskQueue _queue;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<MeetingMinutesController> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public MeetingMinutesController(
        AppDbContext db,
        IBackgroundTaskQueue queue,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<MeetingMinutesController> logger)
    {
        _db = db;
        _queue = queue;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    private static string UserId(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? user.FindFirstValue(ClaimTypes.Name)
        ?? string.Empty;

    private static long ToUnixMs(DateTime utc) =>
        new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeMilliseconds();

    private async Task<bool> CanAccessMeetingAsync(Guid meetingId, string userId, string? username, string? role)
    {
        if (role == "Admin") return true;
        var meeting = await _db.Meetings.AsNoTracking().FirstOrDefaultAsync(m => m.Id == meetingId);
        if (meeting == null) return false;
        if (await MeetingHostAuth.IsAnyHostAsync(_db, meeting, userId, username)) return true;
        return await _db.MeetingParticipants.AsNoTracking()
            .AnyAsync(p => p.MeetingId == meetingId && p.UserId == userId);
    }

    [HttpGet]
    public async Task<IActionResult> Get(Guid meetingId)
    {
        var userId = UserId(User);
        var username = User.FindFirstValue("username");
        var role = User.FindFirstValue(ClaimTypes.Role);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        if (!await CanAccessMeetingAsync(meetingId, userId, username, role))
            return StatusCode(StatusCodes.Status403Forbidden, "Only meeting participants, host, or Admin can view minutes");

        var meeting = await _db.Meetings.AsNoTracking().FirstOrDefaultAsync(m => m.Id == meetingId);
        if (meeting == null)
            return NotFound("Meeting not found");

        // Chủ trì: ưu tiên hiển thị fullName từ bảng Users nếu tra được.
        var hostDisplayName = meeting.HostName;
        var hostIdentity = meeting.HostIdentity?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(hostIdentity))
        {
            if (Guid.TryParse(hostIdentity, out var hostGuid))
            {
                var hostUser = await _db.Users.AsNoTracking()
                    .Where(u => u.Id == hostGuid)
                    .Select(u => new { u.FullName, u.Username })
                    .FirstOrDefaultAsync();
                if (hostUser != null)
                {
                    hostDisplayName = string.IsNullOrWhiteSpace(hostUser.FullName)
                        ? hostUser.Username
                        : hostUser.FullName!;
                }
            }
            else
            {
                var hostUser = await _db.Users.AsNoTracking()
                    .Where(u => u.Username.ToLower() == hostIdentity.ToLower())
                    .Select(u => new { u.FullName, u.Username })
                    .FirstOrDefaultAsync();
                if (hostUser != null)
                {
                    hostDisplayName = string.IsNullOrWhiteSpace(hostUser.FullName)
                        ? hostUser.Username
                        : hostUser.FullName!;
                }
            }
        }

        var participantsRaw = await _db.MeetingParticipants.AsNoTracking()
            .Where(p => p.MeetingId == meetingId)
            .OrderBy(p => p.JoinedAt)
            .ToListAsync();

        var userIds = participantsRaw
            .Select(p => p.UserId.Trim())
            .Where(x => !string.IsNullOrEmpty(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var guidIds = userIds
            .Select(x => Guid.TryParse(x, out var g) ? g : Guid.Empty)
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToList();

        var userLookup = await _db.Users.AsNoTracking()
            .Where(u => guidIds.Contains(u.Id))
            .Select(u => new { Id = u.Id.ToString(), Name = string.IsNullOrWhiteSpace(u.FullName) ? u.Username : u.FullName })
            .ToDictionaryAsync(x => x.Id, x => x.Name, StringComparer.OrdinalIgnoreCase);

        string ResolveParticipantName(string uid, string fallbackUsername)
        {
            var key = uid.Trim();
            if (userLookup.TryGetValue(key, out var name)) return name;
            return string.IsNullOrWhiteSpace(fallbackUsername) ? key : fallbackUsername;
        }

        var distinctByUser = new Dictionary<string, MeetingMinutesParticipantDto>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in participantsRaw)
        {
            var uid = p.UserId.Trim();
            if (string.IsNullOrEmpty(uid)) continue;
            if (distinctByUser.ContainsKey(uid)) continue;
            distinctByUser[uid] = new MeetingMinutesParticipantDto
            {
                UserId = uid,
                DisplayName = ResolveParticipantName(uid, p.Username),
            };
        }

        var participantList = distinctByUser.Values.OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();

        var transcriptRaw = await _db.MeetingTranscriptEntries.AsNoTracking()
            .Where(x => x.MeetingId == meetingId)
            .OrderBy(x => x.AtUtc)
            .ToListAsync();

        var transcriptIds = transcriptRaw
            .Select(x => x.SpeakerIdentity)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var transcriptGuidIds = transcriptIds
            .Select(x => Guid.TryParse(x, out var g) ? g : Guid.Empty)
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToList();

        var transcriptUserLookup = await _db.Users.AsNoTracking()
            .Where(u => transcriptGuidIds.Contains(u.Id))
            .Select(u => new { Id = u.Id.ToString(), Name = string.IsNullOrWhiteSpace(u.FullName) ? u.Username : u.FullName })
            .ToDictionaryAsync(x => x.Id, x => x.Name, StringComparer.OrdinalIgnoreCase);

        var transcript = transcriptRaw.Select(x =>
        {
            var sp = x.SpeakerIdentity.Trim();
            var name = transcriptUserLookup.TryGetValue(sp, out var n) ? n : sp;
            return new MeetingMinutesTranscriptLineDto
            {
                SpeakerName = name,
                Text = x.Text,
                At = ToUnixMs(x.AtUtc),
            };
        }).ToList();

        var polls = await _db.MeetingPolls.AsNoTracking()
            .Where(p => p.MeetingId == meetingId && p.Status != "draft")
            .Include(p => p.Votes)
            .OrderBy(p => p.CreatedAtUtc)
            .ToListAsync();

        var pollDtos = new List<MeetingMinutesPollDto>();
        foreach (var poll in polls)
        {
            string[] options;
            try
            {
                options = JsonSerializer.Deserialize<string[]>(poll.OptionsJson, JsonOpts) ?? Array.Empty<string>();
            }
            catch
            {
                options = Array.Empty<string>();
            }

            var counts = new Dictionary<int, int>();
            for (var i = 0; i < options.Length; i++) counts[i] = 0;

            foreach (var v in poll.Votes ?? Array.Empty<MeetingPollVote>())
            {
                int[] idx;
                try
                {
                    idx = JsonSerializer.Deserialize<int[]>(v.OptionIndicesJson, JsonOpts) ?? Array.Empty<int>();
                }
                catch
                {
                    continue;
                }

                foreach (var i in idx)
                {
                    if (i >= 0 && i < options.Length)
                        counts[i] = counts.GetValueOrDefault(i, 0) + 1;
                }
            }

            pollDtos.Add(new MeetingMinutesPollDto
            {
                PollId = poll.PollId,
                Title = poll.Title,
                Status = poll.Status,
                Options = options,
                OptionVoteCounts = counts,
            });
        }

        var leftTimes = participantsRaw
            .Where(p => p.LeftAt.HasValue)
            .Select(p => p.LeftAt!.Value)
            .ToList();
        var endedAtEstimated = leftTimes.Count > 0 ? ToUnixMs(leftTimes.Max()) : (long?)null;

        return Ok(new MeetingMinutesDto
        {
            MeetingId = meeting.Id,
            Title = meeting.Title,
            HostName = hostDisplayName,
            HostIdentity = meeting.HostIdentity ?? string.Empty,
            LocationDetail = $"Mã: {meeting.MeetingCode}",
            StartedAt = ToUnixMs(meeting.CreatedAt),
            EndedAtEstimated = endedAtEstimated,
            ParticipantCount = participantList.Count,
            Participants = participantList,
            Transcript = transcript,
            Polls = pollDtos,
        });
    }

    [HttpGet("summary/status")]
    public async Task<IActionResult> GetSummaryStatus(Guid meetingId)
    {
        var userId = UserId(User);
        var username = User.FindFirstValue("username");
        var role = User.FindFirstValue(ClaimTypes.Role);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        if (!await CanAccessMeetingAsync(meetingId, userId, username, role))
            return StatusCode(StatusCodes.Status403Forbidden, "Only meeting participants, host, or Admin can check minutes summary");

        var summary = await _db.MeetingMinutesSummaries.AsNoTracking()
            .FirstOrDefaultAsync(s => s.MeetingId == meetingId);

        if (summary == null)
        {
            return Ok(new
            {
                meetingId,
                status = "NotGenerated",
                summaryText = string.Empty,
                overview = string.Empty,
                discussions = string.Empty,
                actionItems = string.Empty,
                errorMessage = (string?)null
            });
        }

        return Ok(new
        {
            meetingId = summary.MeetingId,
            status = summary.Status.ToString(), // "Pending", "Processing", "Success", "Failed"
            summaryText = summary.SummaryText,
            overview = summary.Overview,
            discussions = summary.Discussions,
            actionItems = summary.ActionItems,
            errorMessage = summary.ErrorMessage,
            updatedAt = ToUnixMs(summary.UpdatedAtUtc)
        });
    }

    [HttpPost("summary/trigger")]
    public async Task<IActionResult> TriggerSummary(Guid meetingId)
    {
        var userId = UserId(User);
        var username = User.FindFirstValue("username");
        var role = User.FindFirstValue(ClaimTypes.Role);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        if (!await CanAccessMeetingAsync(meetingId, userId, username, role))
            return StatusCode(StatusCodes.Status403Forbidden, "Only meeting participants, host, or Admin can trigger minutes summary");

        var meeting = await _db.Meetings.AsNoTracking().FirstOrDefaultAsync(m => m.Id == meetingId);
        if (meeting == null)
            return NotFound("Meeting not found");

        var summary = await _db.MeetingMinutesSummaries.FirstOrDefaultAsync(s => s.MeetingId == meetingId);
        if (summary == null)
        {
            summary = new MeetingMinutesSummary
            {
                MeetingId = meetingId,
                Status = MinutesSummaryStatus.Processing,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            _db.MeetingMinutesSummaries.Add(summary);
        }
        else
        {
            summary.Status = MinutesSummaryStatus.Processing;
            summary.SummaryText = string.Empty;
            summary.ErrorMessage = null;
            summary.UpdatedAtUtc = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync();

        _queue.QueueBackgroundWorkItem(async token =>
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var llmClient = scope.ServiceProvider.GetRequiredService<MeetingBackend.Services.Integrations.LlmMinutesSummaryClient>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<MeetingMinutesController>>();

            var innerSummary = await dbContext.MeetingMinutesSummaries.FirstOrDefaultAsync(s => s.MeetingId == meetingId, token);
            if (innerSummary == null) return;

            try
            {
                var summaryResult = await llmClient.GenerateSummaryAsync(meetingId, dbContext, token);
                if (summaryResult.IsCompleted)
                {
                    innerSummary.SummaryText = summaryResult.SummaryText ?? string.Empty;
                    innerSummary.Status = MinutesSummaryStatus.Success;
                    logger.LogInformation("[AI Summary] Tạo tóm tắt thủ công thành công (Gemini Mode) cho cuộc họp {MeetingId}", meetingId);
                }
                else
                {
                    innerSummary.LlmJobId = summaryResult.JobId;
                    innerSummary.Status = MinutesSummaryStatus.Processing;
                    logger.LogInformation("[AI Summary] Đã gửi yêu cầu tóm tắt thủ công thành công (Webhook Mode) cho cuộc họp {MeetingId}. JobId: {JobId}", meetingId, summaryResult.JobId);
                }
                innerSummary.UpdatedAtUtc = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(token);
            }
            catch (Exception ex)
            {
                innerSummary.Status = MinutesSummaryStatus.Failed;
                innerSummary.ErrorMessage = ex.Message;
                innerSummary.UpdatedAtUtc = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(token);
                logger.LogError(ex, "[AI Summary] Lỗi trong quá trình sinh tóm tắt thủ công cho cuộc họp {MeetingId}", meetingId);
            }
        });

        return Accepted(new { message = "AI minutes summary has been triggered and queued in background." });
    }

    [HttpPost("/api/meeting/minutes/summary/callback")]
    [AllowAnonymous]
    public async Task<IActionResult> SummaryCallback()
    {
        // 1. Đọc nội dung body
        using var reader = new System.IO.StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();

        _logger.LogInformation("[AI Summary Callback] ← POST /api/meeting/minutes/summary/callback\nPayload:\n{Body}", body);

        if (string.IsNullOrWhiteSpace(body))
        {
            return BadRequest("Body cannot be empty");
        }

        string? jobId = null;
        string? summaryText = null;
        string status = "success";
        string? errorMessage = null;

        string? overview = null;
        string? discussions = null;
        string? actionItems = null;

        try
        {
            // Thử parse dưới dạng JSON
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (root.TryGetProperty("requestId", out var rProp) && rProp.ValueKind == JsonValueKind.String)
            {
                jobId = rProp.GetString();
            }
            else if (root.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.String)
            {
                jobId = idProp.GetString();
            }
            else if (root.TryGetProperty("jobId", out var jProp) && jProp.ValueKind == JsonValueKind.String)
            {
                jobId = jProp.GetString();
            }
            else if (root.TryGetProperty("meeting_id", out var mIdProp) && mIdProp.ValueKind == JsonValueKind.String)
            {
                jobId = mIdProp.GetString();
            }
            else if (root.TryGetProperty("meetingId", out var mIdProp2) && mIdProp2.ValueKind == JsonValueKind.String)
            {
                jobId = mIdProp2.GetString();
            }

            // Cách 1: Các trường riêng lẻ ở root level
            if (root.TryGetProperty("overview", out var ovProp) && ovProp.ValueKind == JsonValueKind.String)
            {
                overview = ovProp.GetString();
            }
            if (root.TryGetProperty("discussions", out var discProp) && discProp.ValueKind == JsonValueKind.String)
            {
                discussions = discProp.GetString();
            }
            if (root.TryGetProperty("actionItems", out var actProp) && actProp.ValueKind == JsonValueKind.String)
            {
                actionItems = actProp.GetString();
            }
            else if (root.TryGetProperty("action_items", out var actSnakeProp) && actSnakeProp.ValueKind == JsonValueKind.String)
            {
                actionItems = actSnakeProp.GetString();
            }
            else if (root.TryGetProperty("tasks", out var taskProp) && taskProp.ValueKind == JsonValueKind.String)
            {
                actionItems = taskProp.GetString();
            }

            // Cách 2: Các trường nằm trong đối tượng "summary" (Ví dụ: {"summary": {"overview": "...", "discussions": "..."}})
            if (root.TryGetProperty("summary", out var summaryProp))
            {
                if (summaryProp.ValueKind == JsonValueKind.String)
                {
                    summaryText = summaryProp.GetString();
                }
                else if (summaryProp.ValueKind == JsonValueKind.Object)
                {
                    if (summaryProp.TryGetProperty("overview", out var ovInner) && ovInner.ValueKind == JsonValueKind.String)
                    {
                        overview = ovInner.GetString();
                    }
                    if (summaryProp.TryGetProperty("discussions", out var discInner) && discInner.ValueKind == JsonValueKind.String)
                    {
                        discussions = discInner.GetString();
                    }
                    if (summaryProp.TryGetProperty("actionItems", out var actInner) && actInner.ValueKind == JsonValueKind.String)
                    {
                        actionItems = actInner.GetString();
                    }
                    else if (summaryProp.TryGetProperty("action_items", out var actSnakeInner) && actSnakeInner.ValueKind == JsonValueKind.String)
                    {
                        actionItems = actSnakeInner.GetString();
                    }
                    else if (summaryProp.TryGetProperty("tasks", out var taskInner) && taskInner.ValueKind == JsonValueKind.String)
                    {
                        actionItems = taskInner.GetString();
                    }
                }
            }
            
            // Các key dự phòng khác cho text thô gộp chung
            if (string.IsNullOrWhiteSpace(summaryText))
            {
                if (root.TryGetProperty("summaryText", out var stProp) && stProp.ValueKind == JsonValueKind.String)
                {
                    summaryText = stProp.GetString();
                }
                else if (root.TryGetProperty("text", out var tProp) && tProp.ValueKind == JsonValueKind.String)
                {
                    summaryText = tProp.GetString();
                }
                else if (root.TryGetProperty("content", out var cProp) && cProp.ValueKind == JsonValueKind.String)
                {
                    summaryText = cProp.GetString();
                }
            }

            if (root.TryGetProperty("status", out var sProp) && sProp.ValueKind == JsonValueKind.String)
            {
                status = sProp.GetString() ?? "success";
            }

            if (root.TryGetProperty("error", out var errProp) && errProp.ValueKind == JsonValueKind.String)
            {
                errorMessage = errProp.GetString();
            }
            else if (root.TryGetProperty("errorMessage", out var eProp) && eProp.ValueKind == JsonValueKind.String)
            {
                errorMessage = eProp.GetString();
            }
        }
        catch (JsonException)
        {
            return BadRequest("Invalid JSON payload");
        }

        if (string.IsNullOrWhiteSpace(jobId))
        {
            return BadRequest("Job ID ('id', 'jobId', 'requestId', 'meeting_id', or 'meetingId') is required in JSON body");
        }

        // 2. Tìm bản ghi có LlmJobId khớp với jobId hoặc MeetingId nếu jobId là Guid
        MeetingMinutesSummary? summary = null;
        if (Guid.TryParse(jobId, out var meetingGuid))
        {
            summary = await _db.MeetingMinutesSummaries.FirstOrDefaultAsync(s => s.MeetingId == meetingGuid || s.LlmJobId == jobId);
        }
        else
        {
            summary = await _db.MeetingMinutesSummaries.FirstOrDefaultAsync(s => s.LlmJobId == jobId);
        }

        if (summary == null)
        {
            _logger.LogWarning("[AI Summary Callback] ✗ Không tìm thấy bản ghi tóm tắt nào cho Job ID / Meeting ID: {JobId}", jobId);
            return NotFound($"Minutes summary job not found for Job ID: {jobId}");
        }

        _logger.LogInformation("[AI Summary Callback] ✓ Đã tìm thấy bản ghi tóm tắt cho Meeting {MeetingId}. Tiến hành cập nhật kết quả...", summary.MeetingId);

        // 3. Cập nhật trạng thái
        if (string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase) || 
            string.Equals(status, "error", StringComparison.OrdinalIgnoreCase))
        {
            summary.Status = MinutesSummaryStatus.Failed;
            summary.ErrorMessage = errorMessage ?? "LLM service reported failure";
        }
        else
        {
            summary.Status = MinutesSummaryStatus.Success;
            summary.ErrorMessage = null;

            if (!string.IsNullOrWhiteSpace(overview) || !string.IsNullOrWhiteSpace(discussions) || !string.IsNullOrWhiteSpace(actionItems))
            {
                summary.Overview = overview ?? string.Empty;
                summary.Discussions = discussions ?? string.Empty;
                summary.ActionItems = actionItems ?? string.Empty;

                // Tự động gộp thành SummaryText định dạng Markdown chuẩn để tương thích ngược với Frontend hiện tại
                var sb = new System.Text.StringBuilder();
                if (!string.IsNullOrWhiteSpace(overview))
                {
                    sb.AppendLine("### 1. TỔNG QUAN CUỘC HỌP");
                    sb.AppendLine(overview);
                    sb.AppendLine();
                }
                if (!string.IsNullOrWhiteSpace(discussions))
                {
                    sb.AppendLine("### 2. CHI TIẾT THẢO LUẬN & QUYẾT ĐỊNH");
                    sb.AppendLine(discussions);
                    sb.AppendLine();
                }
                if (!string.IsNullOrWhiteSpace(actionItems))
                {
                    sb.AppendLine("### 3. PHÂN CÔNG NHIỆM VỤ");
                    sb.AppendLine(actionItems);
                    sb.AppendLine();
                }
                summary.SummaryText = sb.ToString().TrimEnd();
            }
            else
            {
                summary.SummaryText = summaryText ?? string.Empty;
                // Tự động bóc tách ngược từ SummaryText thô vào 3 trường riêng biệt để DB luôn nhất quán dữ liệu tách biệt
                ParseAndPopulateSections(summary);
            }
        }

        summary.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogInformation("[AI Summary Callback] ✓ Đã lưu kết quả tóm tắt cuộc họp {MeetingId} thành công vào Database.", summary.MeetingId);

        return Ok(new { message = "Summary updated successfully" });
    }

    private static void ParseAndPopulateSections(MeetingMinutesSummary summary)
    {
        if (string.IsNullOrWhiteSpace(summary.SummaryText)) return;

        var text = summary.SummaryText;

        // Định vị vị trí các tiêu đề (không phân biệt hoa thường)
        var idx1 = IndexOfAny(text, new[] { "### 1. TỔNG QUAN", "### 1. TỔNG QUAN CUỘC HỌP", "1. TỔNG QUAN CUỘC HỌP" });
        var idx2 = IndexOfAny(text, new[] { "### 2. CHI TIẾT THẢO LUẬN", "### 2. CHI TIẾT THẢO LUẬN & QUYẾT ĐỊNH", "2. CHI TIẾT THẢO LUẬN & QUYẾT ĐỊNH" });
        var idx3 = IndexOfAny(text, new[] { "### 3. KẾT QUẢ BIỂU QUYẾT", "3. KẾT QUẢ BIỂU QUYẾT" });
        var idx4 = IndexOfAny(text, new[] { "### 4. PHÂN CÔNG NHIỆM VỤ", "### 3. PHÂN CÔNG NHIỆM VỤ", "### 4. PHÂN CÔNG", "4. PHÂN CÔNG NHIỆM VỤ", "3. PHÂN CÔNG NHIỆM VỤ" });

        summary.Overview = string.Empty;
        summary.Discussions = string.Empty;
        summary.ActionItems = string.Empty;

        // Trích xuất phần 1 (Overview)
        if (idx1 >= 0)
        {
            var start = text.IndexOf('\n', idx1);
            if (start == -1) start = idx1;
            
            var end = idx2 >= 0 ? idx2 : (idx3 >= 0 ? idx3 : (idx4 >= 0 ? idx4 : text.Length));
            summary.Overview = text[start..end].Trim();
        }

        // Trích xuất phần 2 (Discussions)
        if (idx2 >= 0)
        {
            var start = text.IndexOf('\n', idx2);
            if (start == -1) start = idx2;
            
            var end = idx3 >= 0 ? idx3 : (idx4 >= 0 ? idx4 : text.Length);
            summary.Discussions = text[start..end].Trim();
        }

        // Trích xuất phần 3 (ActionItems)
        if (idx4 >= 0)
        {
            var start = text.IndexOf('\n', idx4);
            if (start == -1) start = idx4;
            summary.ActionItems = text[start..].Trim();
        }
        else if (idx3 >= 0 && idx2 >= 0)
        {
            var start = text.IndexOf('\n', idx3);
            if (start == -1) start = idx3;
            summary.ActionItems = text[start..].Trim();
        }
    }

    private static int IndexOfAny(string text, string[] searchTerms)
    {
        foreach (var term in searchTerms)
        {
            var idx = text.IndexOf(term, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0) return idx;
        }
        return -1;
    }
}

