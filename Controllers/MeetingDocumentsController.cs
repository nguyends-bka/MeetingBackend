using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MeetingBackend.Data;
using MeetingBackend.DTOs.Meeting;
using MeetingBackend.Entities;
using MeetingBackend.Services;
using System.Security.Claims;
using System.Net.Http.Headers;

namespace MeetingBackend.Controllers;

[ApiController]
[Route("api/meeting")]
[Authorize]
public class MeetingDocumentsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<MeetingDocumentsController> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IBackgroundTaskQueue _taskQueue;
    private readonly string _docsRoot;
    // private const string RagEmbedFileUrl = "https://rag.soictlab.com/embed/file";
    private const string RagEmbedFileUrl = "http://bkmeeting.soict.io:18000/embed/file";

    public MeetingDocumentsController(
        AppDbContext db,
        IWebHostEnvironment env,
        ILogger<MeetingDocumentsController> logger,
        IHttpClientFactory httpClientFactory,
        IBackgroundTaskQueue taskQueue)
    {
        _db = db;
        _env = env;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _taskQueue = taskQueue;
        _docsRoot =
            Environment.GetEnvironmentVariable("MEETING_DOCS_DIR") ??
            Path.Combine(Directory.GetCurrentDirectory(), "uploads", "meeting-docs");
        Directory.CreateDirectory(_docsRoot);
    }

    private string GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue(ClaimTypes.Name)
        ?? string.Empty;

    private string GetUsername() =>
        User.FindFirstValue("username") ?? string.Empty;

    private async Task<bool> HasParticipantAsync(Guid meetingId, string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return false;
        return await _db.MeetingParticipants.AnyAsync(p =>
            p.MeetingId == meetingId && p.UserId == userId);
    }

    private async Task<bool> IsInviteeAsync(Guid meetingId, string username)
    {
        if (string.IsNullOrWhiteSpace(username)) return false;
        var normalized = username.Trim().ToLower();
        return await _db.MeetingInvitees.AnyAsync(i =>
            i.MeetingId == meetingId && i.Username.ToLower() == normalized);
    }

    private Task<bool> IsHostAsync(Meeting meeting, string userId, string username) =>
        MeetingHostAuth.IsAnyHostAsync(_db, meeting, userId, string.IsNullOrWhiteSpace(username) ? null : username);

    private static string GetSafeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        return string.IsNullOrWhiteSpace(name) ? "upload.bin" : name;
    }

    private static string TruncateForLog(string? value, int maxLen = 2000)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.Length <= maxLen) return value;
        return value[..maxLen] + "...";
    }

    private async Task TryEmbedFileToRagAsync(
        Guid meetingId,
        MeetingDocument doc,
        string? authHeader,
        CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(doc.StoragePath) || !System.IO.File.Exists(doc.StoragePath))
            {
                _logger.LogWarning(
                    "[embed/file] Skip because storage file missing. meetingId={MeetingId} docId={DocId} path={StoragePath}",
                    meetingId,
                    doc.Id,
                    doc.StoragePath);
                return;
            }

            using var fileStream = System.IO.File.OpenRead(doc.StoragePath);
            using var multipart = new MultipartFormDataContent();
            using var fileContent = new StreamContent(fileStream);
            if (!MediaTypeHeaderValue.TryParse(doc.ContentType, out var mediaType))
            {
                mediaType = new MediaTypeHeaderValue("application/octet-stream");
            }
            fileContent.Headers.ContentType = mediaType;
            var docIdValue = doc.Id.ToString();
            //var collectionValue = meetingId.ToString();
            var collectionValue = $"docs-{meetingId}";

            multipart.Add(fileContent, "file", doc.FileName);
            multipart.Add(new StringContent(docIdValue), "doc_id");
            multipart.Add(new StringContent(collectionValue), "collection");

            _logger.LogInformation(
                "[embed/file] Payload -> multipartType={MultipartType} doc_id={DocId} collection={Collection} fileName={FileName} fileSize={Size} fileContentType={FileContentType}",
                multipart.Headers.ContentType?.ToString() ?? "",
                docIdValue,
                collectionValue,
                doc.FileName,
                doc.Size,
                fileContent.Headers.ContentType?.ToString() ?? "");

            using var request = new HttpRequestMessage(HttpMethod.Post, RagEmbedFileUrl)
            {
                Content = multipart
            };

            AuthenticationHeaderValue? parsedAuth = null;
            var hasAuthHeader = !string.IsNullOrWhiteSpace(authHeader)
                && AuthenticationHeaderValue.TryParse(authHeader, out parsedAuth);
            if (hasAuthHeader && parsedAuth != null)
            {
                request.Headers.Authorization = parsedAuth;
            }

            _logger.LogInformation(
                "[embed/file] Request -> url={Url} meetingId={MeetingId} docId={DocId} fileName={FileName} size={Size} contentType={ContentType} hasAuth={HasAuth}",
                RagEmbedFileUrl,
                meetingId,
                doc.Id,
                doc.FileName,
                doc.Size,
                doc.ContentType,
                hasAuthHeader);

            var startedAt = DateTime.UtcNow;
            var client = _httpClientFactory.CreateClient("RagEmbed");
            using var response = await client.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogInformation(
                "[embed/file] Response <- status={StatusCode} ok={IsSuccess} elapsedMs={ElapsedMs} meetingId={MeetingId} docId={DocId} body={Body}",
                (int)response.StatusCode,
                response.IsSuccessStatusCode,
                (long)(DateTime.UtcNow - startedAt).TotalMilliseconds,
                meetingId,
                doc.Id,
                TruncateForLog(body));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "[embed/file] Request failed. meetingId={MeetingId} docId={DocId}",
                meetingId,
                doc.Id);
        }
    }

    public sealed class UpdateDocumentVisibilityRequest
    {
        public bool IsShared { get; set; }
    }

    public sealed class UploadMeetingDocumentRequest
    {
        public IFormFile File { get; set; } = default!;
    }

    [HttpGet("{meetingId}/documents")]
    public async Task<IActionResult> ListDocuments(Guid meetingId)
    {
        var userId = GetUserId();
        var username = GetUsername();
        var userRole = User.FindFirstValue(ClaimTypes.Role);
        var meeting = await _db.Meetings.FirstOrDefaultAsync(m => m.Id == meetingId);
        if (meeting == null) return NotFound("Meeting not found");

        var isAdmin = string.Equals(userRole, "Admin", StringComparison.OrdinalIgnoreCase);
        var canView =
            isAdmin
            || await HasParticipantAsync(meetingId, userId)
            || await IsHostAsync(meeting, userId, username)
            || await IsInviteeAsync(meetingId, username);
        if (!canView) return Unauthorized("Only meeting members can view meeting documents");

        var isHostOrCoHost = await IsHostAsync(meeting, userId, username);

        var docs = await _db.MeetingDocuments
            .Where(d =>
                d.MeetingId == meetingId
                && (d.IsShared || isHostOrCoHost || d.UploaderUserId == userId))
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new MeetingDocumentDto
            {
                Id = d.Id,
                MeetingId = d.MeetingId,
                FileName = d.FileName,
                ContentType = d.ContentType,
                Size = d.Size,
                UploaderUserId = d.UploaderUserId,
                UploaderName = d.UploaderName,
                CreatedAt = d.CreatedAt,
                IsShared = d.IsShared,
                FileEndpoint = $"/api/meeting/{meetingId}/documents/{d.Id}/file"
            })
            .ToListAsync();

        return Ok(docs);
    }

    [HttpPost("{meetingId}/documents/upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(100_000_000)] // ~100MB
    public async Task<IActionResult> UploadDocument(Guid meetingId, [FromForm] UploadMeetingDocumentRequest request)
    {
        var userId = GetUserId();
        var username = GetUsername();

        var meeting = await _db.Meetings.FirstOrDefaultAsync(m => m.Id == meetingId);
        if (meeting == null) return NotFound("Meeting not found");

        var isHost = await IsHostAsync(meeting, userId, username);
        if (!isHost) return Unauthorized("Only host can upload documents");
        if (meeting.EndedAt.HasValue) return BadRequest("Meeting has ended");

        var file = request.File;
        if (file == null || file.Length == 0)
        {
            return BadRequest("File is required");
        }

        var docId = Guid.NewGuid();
        var safeName = GetSafeFileName(file.FileName);
        var contentType = string.IsNullOrWhiteSpace(file.ContentType)
            ? "application/octet-stream"
            : file.ContentType;

        var meetingDir = Path.Combine(_docsRoot, meetingId.ToString());
        Directory.CreateDirectory(meetingDir);

        var storageFileName = $"{docId}_{safeName}";
        var storagePath = Path.Combine(meetingDir, storageFileName);

        await using (var stream = System.IO.File.Create(storagePath))
        {
            await file.CopyToAsync(stream);
        }

        var uploaderName = !string.IsNullOrWhiteSpace(username) ? username : userId;

        var doc = new MeetingDocument
        {
            Id = docId,
            MeetingId = meetingId,
            FileName = safeName,
            ContentType = contentType,
            Size = file.Length,
            UploaderUserId = userId,
            UploaderName = uploaderName,
            CreatedAt = DateTime.UtcNow,
            IsShared = true,
            StoragePath = storagePath,
        };

        _db.MeetingDocuments.Add(doc);
        await _db.SaveChangesAsync();

        // Best-effort: forward uploaded file to RAG embed endpoint in background.
        var authHeader = Request.Headers.Authorization.ToString();
        _taskQueue.QueueBackgroundWorkItem(async (token) =>
        {
            await TryEmbedFileToRagAsync(meetingId, doc, authHeader, token);
        });
        _logger.LogInformation(
            "[embed/file] Queued background job. meetingId={MeetingId} docId={DocId}",
            meetingId,
            doc.Id);

        return Ok(new MeetingDocumentDto
        {
            Id = doc.Id,
            MeetingId = doc.MeetingId,
            FileName = doc.FileName,
            ContentType = doc.ContentType,
            Size = doc.Size,
            UploaderUserId = doc.UploaderUserId,
            UploaderName = doc.UploaderName,
            CreatedAt = doc.CreatedAt,
            IsShared = doc.IsShared,
            FileEndpoint = $"/api/meeting/{meetingId}/documents/{doc.Id}/file"
        });
    }

    [HttpPatch("{meetingId}/documents/{documentId}/visibility")]
    public async Task<IActionResult> UpdateDocumentVisibility(
        Guid meetingId,
        Guid documentId,
        [FromBody] UpdateDocumentVisibilityRequest request)
    {
        var userId = GetUserId();
        var username = GetUsername();

        var meeting = await _db.Meetings.FirstOrDefaultAsync(m => m.Id == meetingId);
        if (meeting == null) return NotFound("Meeting not found");

        var isHost = await IsHostAsync(meeting, userId, username);
        if (!isHost) return Unauthorized("Only host can change document visibility");

        var doc = await _db.MeetingDocuments.FirstOrDefaultAsync(d =>
            d.MeetingId == meetingId && d.Id == documentId);
        if (doc == null) return NotFound("Document not found");

        doc.IsShared = request.IsShared;
        await _db.SaveChangesAsync();

        return Ok(new MeetingDocumentDto
        {
            Id = doc.Id,
            MeetingId = doc.MeetingId,
            FileName = doc.FileName,
            ContentType = doc.ContentType,
            Size = doc.Size,
            UploaderUserId = doc.UploaderUserId,
            UploaderName = doc.UploaderName,
            CreatedAt = doc.CreatedAt,
            IsShared = doc.IsShared,
            FileEndpoint = $"/api/meeting/{meetingId}/documents/{doc.Id}/file"
        });
    }

    [HttpGet("{meetingId}/documents/{documentId}/file")]
    public async Task<IActionResult> DownloadFile(Guid meetingId, Guid documentId)
    {
        var userId = GetUserId();
        var username = GetUsername();
        var userRole = User.FindFirstValue(ClaimTypes.Role);
        var meeting = await _db.Meetings.FirstOrDefaultAsync(m => m.Id == meetingId);
        if (meeting == null) return NotFound("Meeting not found");

        var isAdmin = string.Equals(userRole, "Admin", StringComparison.OrdinalIgnoreCase);
        var canView =
            isAdmin
            || await HasParticipantAsync(meetingId, userId)
            || await IsHostAsync(meeting, userId, username)
            || await IsInviteeAsync(meetingId, username);
        if (!canView) return Unauthorized("Only meeting members can download documents");

        var isHostOrCoHost = await IsHostAsync(meeting, userId, username);

        var doc = await _db.MeetingDocuments.FirstOrDefaultAsync(d =>
            d.MeetingId == meetingId && d.Id == documentId);

        if (doc == null) return NotFound("Document not found");

        if (!doc.IsShared && !(isHostOrCoHost || doc.UploaderUserId == userId))
        {
            return Forbid("Document is hidden for non-related participants");
        }

        if (string.IsNullOrWhiteSpace(doc.StoragePath) || !System.IO.File.Exists(doc.StoragePath))
        {
            return NotFound("Document file missing");
        }

        var contentType = string.IsNullOrWhiteSpace(doc.ContentType)
            ? "application/octet-stream"
            : doc.ContentType;

        return PhysicalFile(doc.StoragePath, contentType, doc.FileName);
    }

    [HttpDelete("{meetingId}/documents/{documentId}")]
    public async Task<IActionResult> DeleteDocument(Guid meetingId, Guid documentId)
    {
        var userId = GetUserId();
        var username = GetUsername();

        var meeting = await _db.Meetings.FirstOrDefaultAsync(m => m.Id == meetingId);
        if (meeting == null) return NotFound("Meeting not found");

        var isHost = await IsHostAsync(meeting, userId, username);
        if (!isHost) return Unauthorized("Only host can delete documents");

        var doc = await _db.MeetingDocuments.FirstOrDefaultAsync(d =>
            d.MeetingId == meetingId && d.Id == documentId);

        if (doc == null) return NotFound("Document not found");

        // Xóa file trước rồi mới xóa DB record (DB record vẫn sẽ bị xóa nếu file không tồn tại).
        if (!string.IsNullOrWhiteSpace(doc.StoragePath) && System.IO.File.Exists(doc.StoragePath))
        {
            try
            {
                System.IO.File.Delete(doc.StoragePath);
            }
            catch
            {
                // Không chặn xóa DB nếu xóa file thất bại (tránh làm hệ thống kẹt).
            }
        }

        _db.MeetingDocuments.Remove(doc);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Đã xóa tài liệu" });
    }
}

