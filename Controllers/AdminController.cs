using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using MeetingBackend.Constants;
using MeetingBackend.Data;
using MeetingBackend.Entities;
using MeetingBackend.Models;

namespace MeetingBackend.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = Roles.Admin)] // Chỉ Admin mới truy cập được
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
            .Select(u => new
            {
                u.Id,
                u.Username,
                u.Role,
                u.CreatedAt
            })
            .ToListAsync();

        return Ok(users);
    }

    // ==========================
    // CẬP NHẬT ROLE CỦA USER
    // ==========================
    [HttpPut("users/{userId}/role")]
    public async Task<IActionResult> UpdateUserRole(Guid userId, [FromBody] UpdateUserRoleRequest request)
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

        return Ok(new
        {
            message = "Cập nhật role thành công",
            user = new
            {
                user.Id,
                user.Username,
                user.Role
            }
        });
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

        return Ok(new { message = "Xóa user thành công" });
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

        return Ok(new
        {
            totalUsers,
            totalAdmins,
            totalUsersRole,
            totalMeetings,
            totalParticipants
        });
    }

    // ==========================
    // LẤY DANH SÁCH TẤT CẢ MEETINGS (Admin only)
    // ==========================
    [HttpGet("meetings")]
    public async Task<IActionResult> GetAllMeetings()
    {
        var meetings = await _db.Meetings
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => new
            {
                m.Id,
                m.Title,
                m.HostName,
                m.HostIdentity,
                m.MeetingCode,
                m.Passcode,
                m.RoomName,
                m.CreatedAt,
                ParticipantCount = _db.MeetingParticipants.Count(p => p.MeetingId == m.Id),
                ActiveParticipantCount = _db.MeetingParticipants.Count(p => p.MeetingId == m.Id && p.LeftAt == null)
            })
            .ToListAsync();

        return Ok(meetings);
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

        return Ok(new { message = "Xóa meeting thành công" });
    }
}
