using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MeetingBackend.Data;
using MeetingBackend.DTOs.Meeting;
using MeetingBackend.Entities;
using System.Security.Claims;

namespace MeetingBackend.Controllers;

[ApiController]
[Route("api/meeting")]
[Authorize]
public class MeetingDocumentsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly string _docsRoot;

    public MeetingDocumentsController(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
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

    private async Task<bool> IsHostAsync(Meeting meeting, string userId, string username)
    {
        if (string.Equals(meeting.HostIdentity, userId, StringComparison.OrdinalIgnoreCase)) return true;
        if (!string.IsNullOrWhiteSpace(username) &&
            string.Equals(meeting.HostIdentity, username, StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    private static string GetSafeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        return string.IsNullOrWhiteSpace(name) ? "upload.bin" : name;
    }

    [HttpGet("{meetingId}/documents")]
    public async Task<IActionResult> ListDocuments(Guid meetingId)
    {
        var userId = GetUserId();
        if (!await HasParticipantAsync(meetingId, userId))
        {
            return Unauthorized("Only participants can view meeting documents");
        }

        var docs = await _db.MeetingDocuments
            .Where(d => d.MeetingId == meetingId)
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
                FileEndpoint = $"/api/meeting/{meetingId}/documents/{d.Id}/file"
            })
            .ToListAsync();

        return Ok(docs);
    }

    [HttpPost("{meetingId}/documents/upload")]
    [RequestSizeLimit(100_000_000)] // ~100MB
    public async Task<IActionResult> UploadDocument(Guid meetingId, [FromForm] IFormFile file)
    {
        var userId = GetUserId();
        var username = GetUsername();
        if (!await HasParticipantAsync(meetingId, userId))
        {
            return Unauthorized("Only participants can upload meeting documents");
        }

        var meeting = await _db.Meetings.FirstOrDefaultAsync(m => m.Id == meetingId);
        if (meeting == null) return NotFound("Meeting not found");

        var isHost = await IsHostAsync(meeting, userId, username);
        if (!isHost) return Unauthorized("Only host can upload documents");
        if (meeting.EndedAt.HasValue) return BadRequest("Meeting has ended");

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
            StoragePath = storagePath,
        };

        _db.MeetingDocuments.Add(doc);
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
            FileEndpoint = $"/api/meeting/{meetingId}/documents/{doc.Id}/file"
        });
    }

    [HttpGet("{meetingId}/documents/{documentId}/file")]
    public async Task<IActionResult> DownloadFile(Guid meetingId, Guid documentId)
    {
        var userId = GetUserId();
        if (!await HasParticipantAsync(meetingId, userId))
        {
            return Unauthorized("Only participants can download documents");
        }

        var doc = await _db.MeetingDocuments.FirstOrDefaultAsync(d =>
            d.MeetingId == meetingId && d.Id == documentId);

        if (doc == null) return NotFound("Document not found");

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
        if (!await HasParticipantAsync(meetingId, userId))
        {
            return Unauthorized("Only participants can delete meeting documents");
        }

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

