using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using MeetingBackend.Data;
using MeetingBackend.DTOs.Meeting;
using MeetingBackend.Entities;
using MeetingBackend.Mappers;
using MeetingBackend.Policies;
using MeetingBackend.Services;
using MeetingBackend.Services.Meeting;
using Npgsql;

namespace MeetingBackend.Controllers;

[ApiController]
[Route("api/meeting")]
[Authorize] // 🔐 TẤT CẢ API PHẢI LOGIN
public class MeetingController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IMeetingApplicationService _meetingApplicationService;

    public MeetingController(
        AppDbContext db,
        IMeetingApplicationService meetingApplicationService)
    {
        _db = db;
        _meetingApplicationService = meetingApplicationService;
    }

    private CurrentUserContext CurrentUser() => new()
    {
        UserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                 ?? User.FindFirstValue(ClaimTypes.Name),
        Username = User.FindFirstValue("username"),
        Role = User.FindFirstValue(ClaimTypes.Role),
    };

    private IActionResult ToActionResult<T>(MeetingAppResult<T> result)
    {
        return result.Status switch
        {
            MeetingAppStatus.Ok => Ok(result.Data),
            MeetingAppStatus.BadRequest => BadRequest(result.Message),
            MeetingAppStatus.Unauthorized => Unauthorized(result.Message),
            MeetingAppStatus.NotFound => NotFound(result.Message),
            _ => BadRequest("Yeu cau khong hop le"),
        };
    }

    private async Task TryCreateNotificationAsync(
        string recipientUserId,
        Guid meetingId,
        string meetingTitle,
        string type,
        string message,
        string actorUsername,
        CancellationToken ct = default)
    {
        try
        {
            _db.MeetingNotifications.Add(new MeetingNotification
            {
                Id = Guid.NewGuid(),
                RecipientUserId = recipientUserId,
                MeetingId = meetingId,
                MeetingTitle = meetingTitle,
                Type = type,
                Message = message,
                ActorUsername = actorUsername,
                CreatedAt = DateTime.UtcNow,
            });

            await _db.SaveChangesAsync(ct);
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            // relation does not exist: migrations may not have been applied yet.
            // Try to migrate once and then retry the insert.
            Console.WriteLine($"MeetingNotifications table missing during write; attempting migrate: {ex.MessageText}");

            try
            {
                await _db.Database.MigrateAsync(ct);

                // Clear failed tracked state before retry.
                _db.ChangeTracker.Clear();

                _db.MeetingNotifications.Add(new MeetingNotification
                {
                    Id = Guid.NewGuid(),
                    RecipientUserId = recipientUserId,
                    MeetingId = meetingId,
                    MeetingTitle = meetingTitle,
                    Type = type,
                    Message = message,
                    ActorUsername = actorUsername,
                    CreatedAt = DateTime.UtcNow,
                });

                await _db.SaveChangesAsync(ct);
            }
            catch (Exception retryEx)
            {
                var details = retryEx.InnerException?.Message ?? retryEx.Message;
                Console.WriteLine($"Notification write retry failed ({type}): {details}");
            }
        }
        catch (Exception ex)
        {
            var pg = ex as PostgresException;
            var details = pg != null
                ? $"{pg.SqlState} {pg.MessageText}"
                : (ex.InnerException?.Message ?? ex.Message);
            Console.WriteLine($"Notification write failed ({type}): {details}");
        }
    }

    // ==========================
    // USER/ADMIN TẠO MEETING
    // ==========================
    [HttpPost("create")]
    [Authorize(Policy = AuthorizationPolicies.UserOrAdmin)]
    public async Task<IActionResult> Create(CreateMeetingRequestDto request)
    {
        var result = await _meetingApplicationService.CreateAsync(CurrentUser(), request, HttpContext.RequestAborted);
        return ToActionResult(result);
    }

    // ==========================
    // JOIN MEETING BY LINK (KHÔNG CẦN PASSCODE)
    // ==========================
    [HttpPost("join-by-link")]
    public async Task<IActionResult> JoinByLink([FromBody] JoinByLinkRequestDto req)
    {
        var result = await _meetingApplicationService.JoinByLinkAsync(CurrentUser(), req, HttpContext.RequestAborted);
        return ToActionResult(result);
    }

    // ==========================
    // JOIN MEETING BY ID/CODE + PASSCODE
    // ==========================
    [HttpPost("join")]
    public async Task<IActionResult> Join(JoinMeetingRequestDto req)
    {
        var result = await _meetingApplicationService.JoinAsync(CurrentUser(), req, HttpContext.RequestAborted);
        return ToActionResult(result);
    }

    // ==========================
    // USER JOIN MEETING BY CODE
    // ==========================
    [HttpPost("join-by-code")]
    public async Task<IActionResult> JoinByCode([FromBody] JoinMeetingRequestDto req)
    {
        var result = await _meetingApplicationService.JoinByCodeAsync(CurrentUser(), req, HttpContext.RequestAborted);
        return ToActionResult(result);
    }

    // ==========================
    // LẤY DANH SÁCH MEETING
    // User: chỉ thấy meeting của mình
    // Admin: thấy tất cả meetings
    // ==========================
    [HttpGet]
    public async Task<IActionResult> GetMeetings()
    {
        var result = await _meetingApplicationService.GetMeetingsAsync(CurrentUser(), HttpContext.RequestAborted);
        return ToActionResult(result);
    }

    // ==========================
    // LẤY CHI TIẾT CUỘC HỌP THEO ID
    // ==========================
    [HttpGet("{meetingId:guid}")]
    public async Task<IActionResult> GetMeetingById(Guid meetingId)
    {
        var result = await _meetingApplicationService.GetMeetingByIdAsync(CurrentUser(), meetingId, HttpContext.RequestAborted);
        return ToActionResult(result);
    }

    // ==========================
    // HOST CHỈNH SỬA CUỘC HỌP (khi chưa diễn ra)
    // ==========================
    [HttpPut("{meetingId:guid}")]
    [Authorize]
    public async Task<IActionResult> UpdateMeeting(Guid meetingId, [FromBody] UpdateMeetingRequestDto request)
    {
        var result = await _meetingApplicationService.UpdateMeetingAsync(CurrentUser(), meetingId, request, HttpContext.RequestAborted);
        return ToActionResult(result);
    }

    // ==========================
    // GHI LẠI KHI USER LEAVE MEETING
    // ==========================
    [HttpPost("leave")]
    [Authorize]
    public async Task<IActionResult> Leave([FromBody] LeaveMeetingRequestDto req)
    {
        var result = await _meetingApplicationService.LeaveAsync(CurrentUser(), req, HttpContext.RequestAborted);
        return ToActionResult(result);
    }

    // ==========================
    // HOST KẾT THÚC CUỘC HỌP
    // ==========================
    [HttpPost("{meetingId}/end")]
    [Authorize]
    public async Task<IActionResult> EndMeeting(Guid meetingId)
    {
        var result = await _meetingApplicationService.EndMeetingAsync(CurrentUser(), meetingId, HttpContext.RequestAborted);
        return ToActionResult(result);
    }

    // ==========================
    // DANH SÁCH MỜI THAM GIA
    // ==========================
    [HttpGet("{meetingId:guid}/invitees")]
    [Authorize]
    public async Task<IActionResult> ListInvitees(Guid meetingId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue(ClaimTypes.Name);
        var userRole = User.FindFirstValue(ClaimTypes.Role);
        var username = User.FindFirstValue("username");

        if (string.IsNullOrEmpty(userId))
            return Unauthorized("User identity not found");

        var meeting = await _db.Meetings.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == meetingId);
        if (meeting == null)
            return NotFound("Meeting not found");

        if (userRole != "Admin" && !await MeetingHostAuth.IsAnyHostAsync(_db, meeting, userId, username))
            return Unauthorized("Only meeting host or Admin can view invitees");

        var inviteeRows = await _db.MeetingInvitees
            .AsNoTracking()
            .Where(x => x.MeetingId == meetingId)
            .OrderBy(x => x.Username)
            .ToListAsync();

        if (inviteeRows.Count == 0)
            return Ok(new List<MeetingInviteeDto>());

        var namesToResolve = inviteeRows
            .Select(x => x.Username)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToLower())
            .Distinct()
            .ToList();

        var userMap = await _db.Users
            .AsNoTracking()
            .Where(u => namesToResolve.Contains(u.Username.ToLower()))
            .ToDictionaryAsync(
                u => u.Username.ToLower(),
                u => string.IsNullOrWhiteSpace(u.FullName) ? u.Username : u.FullName!);

        var userLangs = await _db.UserLanguages
            .AsNoTracking()
            .Where(ul => namesToResolve.Contains(ul.User.Username.ToLower()) && ul.IsPrimary)
            .ToDictionaryAsync(ul => ul.User.Username.ToLower(), ul => ul.LanguageCode);

        var list = inviteeRows.Select(row =>
        {
            var un = row.Username.Trim().ToLower();
            return new MeetingInviteeDto
            {
                Username = row.Username,
                FullName = userMap.TryGetValue(un, out var fn) ? fn : row.Username,
                PrimaryLanguage = userLangs.TryGetValue(un, out var lang) ? lang : null
            };
        }).ToList();

        return Ok(list);
    }

    [HttpPost("{meetingId:guid}/invitees")]
    [Authorize]
    public async Task<IActionResult> AddInvitee(Guid meetingId, [FromBody] AddMeetingInviteeRequestDto request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue(ClaimTypes.Name);
        var userRole = User.FindFirstValue(ClaimTypes.Role);
        var username = User.FindFirstValue("username") ?? string.Empty;

        if (string.IsNullOrEmpty(userId))
            return Unauthorized("User identity not found");

        if (string.IsNullOrWhiteSpace(request.Username))
            return BadRequest("Username is required");

        var meeting = await _db.Meetings.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == meetingId);
        if (meeting == null)
            return NotFound("Meeting not found");

        if (userRole != "Admin" && !await MeetingHostAuth.IsAnyHostAsync(_db, meeting, userId, username))
            return Unauthorized("Only meeting host or Admin can add invitees");

        var targetUsername = request.Username.Trim();
        var target = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username.ToLower() == targetUsername.ToLower());
        if (target == null)
            return NotFound(new { message = "Không tìm thấy người dùng với username này" });

        if (MeetingHostAuth.IsPrimaryHost(meeting, target.Id.ToString(), target.Username))
            return BadRequest("Chủ trì không cần thêm vào danh sách mời");

        if (await MeetingHostAuth.IsCoHostAsync(_db, meetingId, target.Id.ToString()))
            return BadRequest("Người này đã là đồng chủ trì");

        var exists = await _db.MeetingInvitees
            .AnyAsync(x => x.MeetingId == meetingId && x.Username.ToLower() == target.Username.ToLower());
        if (exists)
            return Conflict(new { message = "Người dùng đã có trong danh sách mời" });

        _db.MeetingInvitees.Add(new MeetingInvitee
        {
            Id = Guid.NewGuid(),
            MeetingId = meetingId,
            Username = target.Username,
        });

        await _db.SaveChangesAsync();

        await TryCreateNotificationAsync(
            target.Id.ToString(),
            meetingId,
            meeting.Title,
            "invite_added",
            $"{username} đã thêm bạn vào cuộc họp \"{meeting.Title}\"",
            username,
            HttpContext.RequestAborted);

        var primaryLang = await _db.UserLanguages
            .AsNoTracking()
            .Where(ul => ul.UserId == target.Id && ul.IsPrimary)
            .Select(ul => ul.LanguageCode)
            .FirstOrDefaultAsync();

        return Ok(new MeetingInviteeDto
        {
            Username = target.Username,
            FullName = string.IsNullOrWhiteSpace(target.FullName) ? target.Username : target.FullName!.Trim(),
            PrimaryLanguage = primaryLang,
        });
    }

    [HttpDelete("{meetingId:guid}/invitees/{username}")]
    [Authorize]
    public async Task<IActionResult> RemoveInvitee(Guid meetingId, string username)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue(ClaimTypes.Name);
        var userRole = User.FindFirstValue(ClaimTypes.Role);
        var actorUsername = User.FindFirstValue("username") ?? string.Empty;

        if (string.IsNullOrEmpty(userId))
            return Unauthorized("User identity not found");

        var meeting = await _db.Meetings.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == meetingId);
        if (meeting == null)
            return NotFound("Meeting not found");

        if (userRole != "Admin" && !await MeetingHostAuth.IsAnyHostAsync(_db, meeting, userId, actorUsername))
            return Unauthorized("Only meeting host or Admin can remove invitees");

        var target = Uri.UnescapeDataString(username ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(target))
            return BadRequest("Username is required");

        var row = await _db.MeetingInvitees
            .FirstOrDefaultAsync(x => x.MeetingId == meetingId && x.Username.ToLower() == target.ToLower());
        if (row == null)
            return NotFound("Invitee not found");

        _db.MeetingInvitees.Remove(row);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Removed" });
    }

    // ==========================
    // ĐỒNG CHỦ TRÌ (nâng từ danh sách mời)
    // ==========================
    [HttpGet("{meetingId:guid}/co-hosts")]
    [Authorize]
    public async Task<IActionResult> ListCoHosts(Guid meetingId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue(ClaimTypes.Name);
        var userRole = User.FindFirstValue(ClaimTypes.Role);
        var username = User.FindFirstValue("username");

        if (string.IsNullOrEmpty(userId))
            return Unauthorized("User identity not found");

        var meeting = await _db.Meetings.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == meetingId);
        if (meeting == null)
            return NotFound("Meeting not found");

        if (userRole != "Admin" && !await MeetingHostAuth.IsAnyHostAsync(_db, meeting, userId, username))
            return Unauthorized("Only meeting host or Admin can view co-hosts");

        var rows = await _db.MeetingCoHosts
            .AsNoTracking()
            .Where(x => x.MeetingId == meetingId)
            .OrderBy(x => x.Username)
            .ToListAsync();

        if (rows.Count == 0)
            return Ok(new List<MeetingCoHostDto>());

        var namesToResolve = rows
            .Select(x => x.Username)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToLower())
            .Distinct()
            .ToList();

        var userMap = await _db.Users
            .AsNoTracking()
            .Where(u => namesToResolve.Contains(u.Username.ToLower()))
            .ToDictionaryAsync(
                u => u.Username.ToLower(),
                u => string.IsNullOrWhiteSpace(u.FullName) ? u.Username : u.FullName!);

        var list = rows.Select(row =>
        {
            var un = row.Username.Trim().ToLower();
            return new MeetingCoHostDto
            {
                HostUserId = row.HostUserId,
                Username = row.Username,
                FullName = userMap.TryGetValue(un, out var fn) ? fn : row.Username,
            };
        }).ToList();

        return Ok(list);
    }

    [HttpPost("{meetingId:guid}/co-hosts/promote")]
    [Authorize]
    public async Task<IActionResult> PromoteInviteeToCoHost(Guid meetingId, [FromBody] PromoteInviteeToCoHostRequestDto request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue(ClaimTypes.Name);
        var userRole = User.FindFirstValue(ClaimTypes.Role);
        var username = User.FindFirstValue("username") ?? string.Empty;

        if (string.IsNullOrEmpty(userId))
            return Unauthorized("User identity not found");

        if (string.IsNullOrWhiteSpace(request.Username))
            return BadRequest("Username is required");

        var meeting = await _db.Meetings.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == meetingId);
        if (meeting == null)
            return NotFound("Meeting not found");

        if (userRole != "Admin" && !await MeetingHostAuth.IsAnyHostAsync(_db, meeting, userId, username))
            return Unauthorized("Only meeting host or Admin can promote invitees");

        var targetUsername = request.Username.Trim();
        var inviteeRow = await _db.MeetingInvitees
            .FirstOrDefaultAsync(x => x.MeetingId == meetingId && x.Username.ToLower() == targetUsername.ToLower());
        if (inviteeRow == null)
            return BadRequest(new { message = "Người này không nằm trong danh sách mời" });

        var target = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username.ToLower() == inviteeRow.Username.Trim().ToLower());
        if (target == null)
            return NotFound(new { message = "Không tìm thấy người dùng" });

        if (MeetingHostAuth.IsPrimaryHost(meeting, target.Id.ToString(), target.Username))
            return BadRequest("Người này đã là chủ trì");

        if (await MeetingHostAuth.IsCoHostAsync(_db, meetingId, target.Id.ToString()))
            return Conflict(new { message = "Người này đã là đồng chủ trì" });

        _db.MeetingCoHosts.Add(new MeetingCoHost
        {
            Id = Guid.NewGuid(),
            MeetingId = meetingId,
            HostUserId = target.Id.ToString(),
            Username = target.Username,
        });
        _db.MeetingInvitees.Remove(inviteeRow);

        await _db.SaveChangesAsync();

        await TryCreateNotificationAsync(
            target.Id.ToString(),
            meetingId,
            meeting.Title,
            "cohost_granted",
            $"{username} đã cấp quyền chủ trì cho bạn tại cuộc họp \"{meeting.Title}\"",
            username,
            HttpContext.RequestAborted);

        return Ok(new MeetingCoHostDto
        {
            HostUserId = target.Id.ToString(),
            Username = target.Username,
            FullName = string.IsNullOrWhiteSpace(target.FullName) ? target.Username : target.FullName!.Trim(),
        });
    }

    [HttpPost("{meetingId:guid}/co-hosts/demote")]
    [Authorize]
    public async Task<IActionResult> DemoteCoHostToInvitee(Guid meetingId, [FromBody] DemoteCoHostToInviteeRequestDto request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue(ClaimTypes.Name);
        var userRole = User.FindFirstValue(ClaimTypes.Role);
        var username = User.FindFirstValue("username") ?? string.Empty;

        if (string.IsNullOrEmpty(userId))
            return Unauthorized("User identity not found");
        if (string.IsNullOrWhiteSpace(request.Username))
            return BadRequest("Username is required");

        var meeting = await _db.Meetings.AsNoTracking().FirstOrDefaultAsync(m => m.Id == meetingId);
        if (meeting == null)
            return NotFound("Meeting not found");
        if (userRole != "Admin" && !await MeetingHostAuth.IsAnyHostAsync(_db, meeting, userId, username))
            return Unauthorized("Only meeting host or Admin can change role");

        var targetUsername = request.Username.Trim();
        var cohost = await _db.MeetingCoHosts
            .FirstOrDefaultAsync(x => x.MeetingId == meetingId && x.Username.ToLower() == targetUsername.ToLower());
        if (cohost == null)
            return NotFound(new { message = "Không tìm thấy đồng chủ trì" });

        _db.MeetingCoHosts.Remove(cohost);
        var inviteeExists = await _db.MeetingInvitees
            .AnyAsync(x => x.MeetingId == meetingId && x.Username.ToLower() == cohost.Username.ToLower());
        if (!inviteeExists)
        {
            _db.MeetingInvitees.Add(new MeetingInvitee
            {
                Id = Guid.NewGuid(),
                MeetingId = meetingId,
                Username = cohost.Username,
            });
        }

        await _db.SaveChangesAsync();

        var demotedUser = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username.ToLower() == cohost.Username.ToLower());

        if (demotedUser != null)
        {
            await TryCreateNotificationAsync(
                demotedUser.Id.ToString(),
                meetingId,
                meeting.Title,
                "cohost_removed",
                $"{username} đã gỡ bỏ quyền chủ trì của bạn tại cuộc họp \"{meeting.Title}\"",
                username,
                HttpContext.RequestAborted);
        }

        return Ok(new { message = "Role changed to member" });
    }

    [HttpDelete("{meetingId:guid}/co-hosts/{hostUserId}")]
    [Authorize]
    public async Task<IActionResult> RemoveCoHost(Guid meetingId, string hostUserId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue(ClaimTypes.Name);
        var userRole = User.FindFirstValue(ClaimTypes.Role);
        var actorUsername = User.FindFirstValue("username") ?? string.Empty;

        if (string.IsNullOrEmpty(userId))
            return Unauthorized("User identity not found");

        var meeting = await _db.Meetings.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == meetingId);
        if (meeting == null)
            return NotFound("Meeting not found");

        if (userRole != "Admin" && !await MeetingHostAuth.IsAnyHostAsync(_db, meeting, userId, actorUsername))
            return Unauthorized("Only meeting host or Admin can remove co-hosts");

        var decoded = Uri.UnescapeDataString(hostUserId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(decoded))
            return BadRequest("hostUserId is required");

        var row = await _db.MeetingCoHosts
            .FirstOrDefaultAsync(x => x.MeetingId == meetingId && x.HostUserId == decoded);
        if (row == null)
            return NotFound("Co-host not found");

        _db.MeetingCoHosts.Remove(row);

        await _db.SaveChangesAsync();

        await TryCreateNotificationAsync(
            decoded,
            meetingId,
            meeting.Title,
            "cohost_removed",
            $"{actorUsername} đã gỡ bỏ quyền chủ trì của bạn tại cuộc họp \"{meeting.Title}\"",
            actorUsername,
            HttpContext.RequestAborted);

        return Ok(new { message = "Removed" });
    }

    // ==========================
    // XEM LỊCH SỬ VÀO/RA CỦA MEETING
    // Host: chỉ xem được meeting của mình
    // Admin: xem được tất cả meetings
    // ==========================
    [HttpGet("{meetingId}/history")]
    [Authorize]
    public async Task<IActionResult> GetMeetingHistory(Guid meetingId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue(ClaimTypes.Name);
        var userRole = User.FindFirstValue(ClaimTypes.Role);
        var username = User.FindFirstValue("username");

        if (string.IsNullOrEmpty(userId))
            return Unauthorized("User identity not found");

        var meeting = await _db.Meetings
            .FirstOrDefaultAsync(m => m.Id == meetingId);

        if (meeting == null)
            return NotFound("Meeting not found");

        if (userRole != "Admin" && !await MeetingHostAuth.IsAnyHostAsync(_db, meeting, userId, username))
            return Unauthorized("Only meeting host or Admin can view history");

        var participants = await _db.MeetingParticipants
            .Where(p => p.MeetingId == meetingId)
            .OrderByDescending(p => p.JoinedAt)
            .ToListAsync();

        var history = participants.Select(MeetingMapper.ToMeetingHistoryItemDto).ToList();
        return Ok(history);
    }

    // ==========================
    // LẤY LỊCH SỬ THAM GIA CỦA USER HIỆN TẠI
    // ==========================
    [HttpGet("my-history")]
    [Authorize]
    public async Task<IActionResult> GetMyHistory()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue(ClaimTypes.Name);

        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized("User identity not found");
        }

        var history = await _db.MeetingParticipants
            .Where(p => p.UserId == userId)
            .Join(
                _db.Meetings,
                participant => participant.MeetingId,
                meeting => meeting.Id,
                (participant, meeting) => new MyHistoryItemDto
                {
                    Id = participant.Id,
                    MeetingId = participant.MeetingId,
                    MeetingTitle = meeting.Title,
                    Username = participant.Username,
                    JoinedAt = participant.JoinedAt,
                    LeftAt = participant.LeftAt,
                    Duration = participant.LeftAt.HasValue
                        ? (participant.LeftAt.Value - participant.JoinedAt).TotalMinutes
                        : null,
                    MeetingCode = meeting.MeetingCode,
                    HostName = meeting.HostName,
                    Location = meeting.Location
                }
            )
            .OrderByDescending(h => h.JoinedAt)
            .ToListAsync();

        return Ok(history);
    }

    // ==========================
    // LẤY THÔNG BÁO CỦA USER
    // ==========================
    [HttpGet("my-notifications")]
    [Authorize]
    public async Task<IActionResult> GetMyNotifications()
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? User.FindFirstValue(ClaimTypes.Name);

            if (string.IsNullOrEmpty(userId))
                return Unauthorized("User identity not found");

            async Task<List<MeetingNotificationDto>> LoadNotificationsAsync()
            {
                return await _db.MeetingNotifications
                    .Where(n => n.RecipientUserId == userId)
                    .OrderByDescending(n => n.CreatedAt)
                    .Take(10)
                    .Select(n => new MeetingNotificationDto
                    {
                        Id = n.Id,
                        MeetingId = n.MeetingId,
                        MeetingTitle = n.MeetingTitle,
                        Type = n.Type,
                        Message = n.Message,
                        ActorUsername = n.ActorUsername,
                        CreatedAt = n.CreatedAt,
                        OpenedAt = n.OpenedAt,
                    })
                    .ToListAsync();
            }

            var notifications = await LoadNotificationsAsync();

            return Ok(notifications);
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            // relation does not exist (e.g., migrations not applied yet)
            Console.WriteLine($"MeetingNotifications table missing: {ex.MessageText}");
            return Ok(new List<MeetingNotificationDto>());
        }
        catch (PostgresException ex) when (ex.SqlState == "42703")
        {
            // column does not exist; attempt to migrate once and retry.
            Console.WriteLine($"MeetingNotifications column missing: {ex.MessageText}");

            try
            {
                await _db.Database.MigrateAsync();
                _db.ChangeTracker.Clear();

                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                             ?? User.FindFirstValue(ClaimTypes.Name);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("User identity not found");
                }

                var notifications = await _db.MeetingNotifications
                    .Where(n => n.RecipientUserId == userId)
                    .OrderByDescending(n => n.CreatedAt)
                    .Take(10)
                    .Select(n => new MeetingNotificationDto
                    {
                        Id = n.Id,
                        MeetingId = n.MeetingId,
                        MeetingTitle = n.MeetingTitle,
                        Type = n.Type,
                        Message = n.Message,
                        ActorUsername = n.ActorUsername,
                        CreatedAt = n.CreatedAt,
                        OpenedAt = n.OpenedAt,
                    })
                    .ToListAsync();

                return Ok(notifications);
            }
            catch (Exception retryEx)
            {
                Console.WriteLine($"MeetingNotifications retry after migrate failed: {retryEx.Message}");
                return Ok(new List<MeetingNotificationDto>());
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetMyNotifications: {ex.Message}");
            return StatusCode(500, new { error = ex.Message, details = ex.InnerException?.Message });
        }
    }

    // ==========================
    // ĐỀM THÔNG BÁO ĐÃ ĐƯỢC XEM
    // ==========================
    [HttpPost("notifications/{notificationId:guid}/open")]
    [Authorize]
    public async Task<IActionResult> OpenNotification(Guid notificationId)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? User.FindFirstValue(ClaimTypes.Name);

            if (string.IsNullOrEmpty(userId))
                return Unauthorized("User identity not found");

            var notification = await _db.MeetingNotifications
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.RecipientUserId == userId);

            if (notification == null)
                return NotFound("Notification not found");

            notification.OpenedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Ok(new { openedAt = notification.OpenedAt });
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            // If the table isn't present yet, behave like it's not found.
            Console.WriteLine($"MeetingNotifications table missing: {ex.MessageText}");
            return NotFound("Notification not found");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in OpenNotification: {ex.Message}");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ==========================
    // HOST HỦY CUỘC HỌP
    // ==========================
    [HttpPost("{meetingId:guid}/cancel")]
    [Authorize]
    public async Task<IActionResult> CancelMeeting(Guid meetingId)
    {
        var result = await _meetingApplicationService.CancelMeetingAsync(CurrentUser(), meetingId, HttpContext.RequestAborted);
        return ToActionResult(result);
    }
}
