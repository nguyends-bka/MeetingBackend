using BCrypt.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using MeetingBackend.Data;
using MeetingBackend.Entities;
using MeetingBackend.Models;

namespace MeetingBackend.Controllers;

[ApiController]
[Route("api/user")]
[Authorize]
public class UserController : ControllerBase
{
    private readonly AppDbContext _db;

    public UserController(AppDbContext db)
    {
        _db = db;
    }

    // ==========================
    // LẤY THÔNG TIN USER HIỆN TẠI
    // ==========================
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue(ClaimTypes.Name);

        if (string.IsNullOrEmpty(userId))
            return Unauthorized("User identity not found");

        var user = await _db.Users
            .Where(u => u.Id.ToString() == userId)
            .Select(u => new
            {
                u.Id,
                u.Username,
                u.Role,
                u.FullName,
                u.Email,
                u.CreatedAt
            })
            .FirstOrDefaultAsync();

        if (user == null)
            return NotFound("User not found");

        return Ok(user);
    }

    // ==========================
    // CẬP NHẬT THÔNG TIN CÁ NHÂN (FullName, Email)
    // ==========================
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue(ClaimTypes.Name);

        if (string.IsNullOrEmpty(userId))
            return Unauthorized("User identity not found");

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id.ToString() == userId);

        if (user == null)
            return NotFound("User not found");

        // Kiểm tra email đã tồn tại chưa (nếu có email và khác email hiện tại)
        if (!string.IsNullOrWhiteSpace(request.Email) && 
            request.Email != user.Email &&
            await _db.Users.AnyAsync(u => u.Email == request.Email && u.Id.ToString() != userId))
        {
            return BadRequest(new { message = "Email đã được sử dụng. Vui lòng chọn email khác." });
        }

        // Cập nhật thông tin
        if (request.FullName != null)
            user.FullName = string.IsNullOrWhiteSpace(request.FullName) ? null : request.FullName.Trim();

        if (request.Email != null)
            user.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLower();

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Cập nhật thông tin thành công",
            user = new
            {
                user.Id,
                user.Username,
                user.Role,
                user.FullName,
                user.Email
            }
        });
    }

    // ==========================
    // ĐỔI MẬT KHẨU
    // ==========================
    [HttpPut("profile/password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue(ClaimTypes.Name);

        if (string.IsNullOrEmpty(userId))
            return Unauthorized("User identity not found");

        if (string.IsNullOrWhiteSpace(request.OldPassword))
            return BadRequest(new { message = "Mật khẩu cũ không được để trống" });

        if (string.IsNullOrWhiteSpace(request.NewPassword))
            return BadRequest(new { message = "Mật khẩu mới không được để trống" });

        if (request.NewPassword.Length < 6)
            return BadRequest(new { message = "Mật khẩu mới phải có ít nhất 6 ký tự" });

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id.ToString() == userId);

        if (user == null)
            return NotFound("User not found");

        // Kiểm tra mật khẩu cũ
        if (!BCrypt.Net.BCrypt.Verify(request.OldPassword, user.PasswordHash))
            return BadRequest(new { message = "Mật khẩu cũ không đúng" });

        // Cập nhật mật khẩu mới
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Đổi mật khẩu thành công" });
    }
}
