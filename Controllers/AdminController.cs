using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using MeetingBackend.Constants;
using MeetingBackend.Data;
using MeetingBackend.DTOs.Admin;
using MeetingBackend.DTOs.Catalog;
using MeetingBackend.Entities;
using MeetingBackend.Mappers;
using MeetingBackend.Policies;

namespace MeetingBackend.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)] // Dynamic role check from database
public class AdminController : ControllerBase
{
    private readonly AppDbContext _db;

    public AdminController(AppDbContext db)
    {
        _db = db;
    }

    // ==========================
    // LẤY DANH SÁCH TẤT CẢ USERS
    // ==========================
    [HttpGet("users")]
    public async Task<IActionResult> GetAllUsers()
    {
        var users = await _db.Users
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync();

        var unitIds = users
            .Where(x => x.OrganizationUnitId.HasValue)
            .Select(x => x.OrganizationUnitId!.Value)
            .Distinct()
            .ToList();
        var unitMap = await _db.OrganizationUnits
            .AsNoTracking()
            .Where(x => unitIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name);

        // Lấy tất cả UserCountries + UserLanguages cho các users cùng lúc (tránh N+1)
        var userIds = users.Select(u => u.Id).ToList();

        var allUserCountries = await _db.UserCountries
            .AsNoTracking()
            .Where(uc => userIds.Contains(uc.UserId))
            .Join(_db.Countries, uc => uc.CountryCode, c => c.Code,
                (uc, c) => new { uc.UserId, c.Code, c.CountryName })
            .ToListAsync();

        var allUserLanguages = await _db.UserLanguages
            .AsNoTracking()
            .Where(ul => userIds.Contains(ul.UserId))
            .Join(_db.Languages, ul => ul.LanguageCode, l => l.Code,
                (ul, l) => new { ul.UserId, l.Code, l.LanguageName, ul.IsPrimary })
            .ToListAsync();

        var countriesByUser = allUserCountries
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.Select(x => new UserCountryResponseDto
            {
                Code = x.Code,
                CountryName = x.CountryName
            }).ToList());

        var languagesByUser = allUserLanguages
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g
                .OrderByDescending(x => x.IsPrimary)
                .ThenBy(x => x.LanguageName)
                .Select(x => new UserLanguageResponseDto
                {
                    Code = x.Code,
                    LanguageName = x.LanguageName,
                    IsPrimary = x.IsPrimary
                }).ToList());

        var response = users.Select(u => new AdminUserDto
        {
            Id = u.Id,
            Username = u.Username,
            Role = u.Role,
            FullName = u.FullName,
            Email = u.Email,
            Position = u.Position,
            AcademicRank = u.AcademicRank,
            AcademicDegree = u.AcademicDegree,
            OrganizationUnitId = u.OrganizationUnitId,
            OrganizationUnitName = u.OrganizationUnitId.HasValue && unitMap.TryGetValue(u.OrganizationUnitId.Value, out var unitName)
                ? unitName
                : null,
            HasAvatar = !string.IsNullOrWhiteSpace(u.Avatar),
            CreatedAt = u.CreatedAt,
            Countries = countriesByUser.TryGetValue(u.Id, out var c) ? c : [],
            Languages = languagesByUser.TryGetValue(u.Id, out var l) ? l : []
        }).ToList();

        return Ok(response);
    }


    // ==========================
    // CẬP NHẬT ROLE CỦA USER
    // ==========================
    [HttpPut("users/{userId}/role")]
    public async Task<IActionResult> UpdateUserRole(Guid userId, [FromBody] UpdateUserRoleRequestDto request)
    {
        if (string.IsNullOrEmpty(request.Role) || 
            (request.Role != Roles.Admin && request.Role != Roles.User))
        {
            return BadRequest(new { message = "Role phải là 'Admin' hoặc 'User'" });
        }

        var user = await _db.Users.FindAsync(userId);
        if (user == null)
            return NotFound(new { message = "User không tồn tại" });

        // Không cho phép Admin tự đổi role của chính mình
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                           ?? User.FindFirstValue(ClaimTypes.Name);
        if (user.Id.ToString() == currentUserId && request.Role != Roles.Admin)
        {
            return BadRequest(new { message = "Bạn không thể tự đổi role của chính mình" });
        }

        user.Role = request.Role;
        await _db.SaveChangesAsync();

        var response = new UpdateUserRoleResponseDto
        {
            Message = "Cập nhật role thành công",
            User = new UserRoleDto
            {
                Id = user.Id,
                Username = user.Username,
                Role = user.Role
            }
        };

        return Ok(response);
    }

    // ==========================
    // XÓA USER
    // ==========================
    [HttpDelete("users/{userId}")]
    public async Task<IActionResult> DeleteUser(Guid userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null)
            return NotFound(new { message = "User không tồn tại" });

        // Không cho phép Admin tự xóa chính mình
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                           ?? User.FindFirstValue(ClaimTypes.Name);
        if (user.Id.ToString() == currentUserId)
        {
            return BadRequest(new { message = "Bạn không thể xóa chính mình" });
        }

        var uid = user.Id.ToString();
        var hasMeetings = await _db.Meetings.AnyAsync(m => m.HostIdentity == uid);
        var isCoHostElsewhere = await _db.MeetingCoHosts.AnyAsync(c => c.HostUserId == uid);

        if (hasMeetings || isCoHostElsewhere)
        {
            return BadRequest(new { message = "Không thể xóa user đang là chủ trì/đồng chủ trì cuộc họp. Vui lòng xóa hoặc chuyển quyền trước." });
        }

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();

        var response = new DeleteUserResponseDto
        {
            Message = "Xóa user thành công"
        };

        return Ok(response);
    }

    // ==========================
    // LẤY THỐNG KÊ TỔNG QUAN
    // ==========================
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var totalUsers = await _db.Users.CountAsync();
        var totalAdmins = await _db.Users.CountAsync(u => u.Role == Roles.Admin);
        var totalUsersRole = await _db.Users.CountAsync(u => u.Role == Roles.User);
        var totalMeetings = await _db.Meetings.CountAsync();
        var totalParticipants = await _db.MeetingParticipants.CountAsync();

        var response = new AdminStatsDto
        {
            TotalUsers = totalUsers,
            TotalAdmins = totalAdmins,
            TotalUsersRole = totalUsersRole,
            TotalMeetings = totalMeetings,
            TotalParticipants = totalParticipants
        };

        return Ok(response);
    }

    // ==========================
    // LẤY DANH SÁCH TẤT CẢ MEETINGS (Admin only)
    // ==========================
    [HttpGet("meetings")]
    public async Task<IActionResult> GetAllMeetings()
    {
        var meetings = await _db.Meetings
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();

        var meetingIds = meetings.Select(m => m.Id).ToList();

        var participantCounts = await _db.MeetingParticipants
            .Where(p => meetingIds.Contains(p.MeetingId))
            .GroupBy(p => p.MeetingId)
            .Select(g => new { MeetingId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.MeetingId, x => x.Count);

        var activeCounts = await _db.MeetingParticipants
            .Where(p => meetingIds.Contains(p.MeetingId) && p.LeftAt == null)
            .GroupBy(p => p.MeetingId)
            .Select(g => new { MeetingId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.MeetingId, x => x.Count);

        var response = meetings.Select(meeting => new AdminMeetingDto
        {
            Id = meeting.Id,
            Title = meeting.Title,
            HostName = meeting.HostName,
            HostIdentity = meeting.HostIdentity,
            MeetingCode = meeting.MeetingCode,
            Passcode = meeting.Passcode,
            RoomName = meeting.RoomName,
            Location = meeting.Location,
            CreatedAt = meeting.CreatedAt,
            StartedAt = meeting.StartedAt,
            EndedAt = meeting.EndedAt,
            ParticipantCount = participantCounts.TryGetValue(meeting.Id, out var pc) ? pc : 0,
            ActiveParticipantCount = activeCounts.TryGetValue(meeting.Id, out var ac) ? ac : 0,
            Status = meeting.Status.ToString().ToLower(),
            EstimatedEndAt = meeting.EstimatedEndAt
        }).ToList();

        // Enrich Host Info
        var hostIdentities = response
            .Select(d => d.HostIdentity)
            .Where(hid => !string.IsNullOrEmpty(hid))
            .Distinct()
            .ToList();

        if (hostIdentities.Count > 0)
        {
            var hostGuids = new List<Guid>();
            var hostUsernames = new List<string>();

            foreach (var hid in hostIdentities)
            {
                if (Guid.TryParse(hid, out var g))
                {
                    hostGuids.Add(g);
                }
                else
                {
                    hostUsernames.Add(hid);
                }
            }

            var hostUsers = await _db.Users
                .AsNoTracking()
                .Where(u => hostGuids.Contains(u.Id) || hostUsernames.Contains(u.Username))
                .Select(u => new { u.Id, u.Username, u.FullName })
                .ToListAsync();

            var hostMapById = hostUsers.ToDictionary(u => u.Id.ToString().ToLower(), u => u);
            var hostMapByUsername = hostUsers.ToDictionary(u => u.Username.ToLower(), u => u);

            foreach (var dto in response)
            {
                if (string.IsNullOrEmpty(dto.HostIdentity))
                    continue;

                var lookupKey = dto.HostIdentity.ToLower();
                if (hostMapById.TryGetValue(lookupKey, out var userObj) || hostMapByUsername.TryGetValue(lookupKey, out userObj))
                {
                    dto.HostName = !string.IsNullOrWhiteSpace(userObj.FullName) ? userObj.FullName : userObj.Username;
                    dto.HostIdentity = userObj.Username;
                }
            }
        }

        return Ok(response);
    }

    // ==========================
    // XÓA MEETING (Admin only)
    // ==========================
    [HttpDelete("meetings/{meetingId}")]
    public async Task<IActionResult> DeleteMeeting(Guid meetingId)
    {
        var meeting = await _db.Meetings
            .FirstOrDefaultAsync(m => m.Id == meetingId);

        if (meeting == null)
            return NotFound(new { message = "Meeting không tồn tại" });

        // Xóa tất cả participants trước
        var participants = await _db.MeetingParticipants
            .Where(p => p.MeetingId == meetingId)
            .ToListAsync();
        
        _db.MeetingParticipants.RemoveRange(participants);

        // Xóa meeting
        _db.Meetings.Remove(meeting);
        await _db.SaveChangesAsync();

        var response = new DeleteMeetingResponseDto
        {
            Message = "Xóa meeting thành công"
        };

        return Ok(response);
    }
}
