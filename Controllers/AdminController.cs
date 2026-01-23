using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using MeetingBackend.Constants;
using MeetingBackend.Data;
using MeetingBackend.DTOs.Admin;
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

        var response = users.Select(u => new AdminUserDto
        {
            Id = u.Id,
            Username = u.Username,
            Role = u.Role,
            CreatedAt = u.CreatedAt
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

        // Kiểm tra xem user có phải là host của meeting nào không
        var hasMeetings = await _db.Meetings
            .AnyAsync(m => m.HostIdentity == user.Id.ToString());

        if (hasMeetings)
        {
            return BadRequest(new { message = "Không thể xóa user đang là host của meeting. Vui lòng xóa các meeting trước." });
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

        var response = new List<AdminMeetingDto>();
        foreach (var meeting in meetings)
        {
            var participantCount = await _db.MeetingParticipants.CountAsync(p => p.MeetingId == meeting.Id);
            var activeParticipantCount = await _db.MeetingParticipants.CountAsync(p => p.MeetingId == meeting.Id && p.LeftAt == null);

            response.Add(new AdminMeetingDto
            {
                Id = meeting.Id,
                Title = meeting.Title,
                HostName = meeting.HostName,
                HostIdentity = meeting.HostIdentity,
                MeetingCode = meeting.MeetingCode,
                Passcode = meeting.Passcode,
                RoomName = meeting.RoomName,
                CreatedAt = meeting.CreatedAt,
                ParticipantCount = participantCount,
                ActiveParticipantCount = activeParticipantCount
            });
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
