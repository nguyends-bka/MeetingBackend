using BCrypt.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MeetingBackend.Constants;
using MeetingBackend.Data;
using MeetingBackend.Entities;
using MeetingBackend.Models;
using MeetingBackend.Services;

namespace MeetingBackend.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly JwtTokenService _jwt;

    public AuthController(AppDbContext db, JwtTokenService jwt)
    {
        _db = db;
        _jwt = jwt;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest req)
    {
        if (await _db.Users.AnyAsync(u => u.Username == req.Username))
            return BadRequest(new { message = "Tên đăng nhập đã tồn tại. Vui lòng chọn tên khác." });

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = req.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            Role = Roles.User,
            FullName = string.IsNullOrWhiteSpace(req.FullName) ? null : req.FullName.Trim(),
            Email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim().ToLower()
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        // Trả về JSON response giống login để frontend có thể parse
        return Ok(new
        {
            message = "Registration successful"
        });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest req)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Username == req.Username);

        if (user == null)
            return Unauthorized(new { message = "Tên đăng nhập hoặc mật khẩu không đúng" });

        if (!BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return Unauthorized(new { message = "Tên đăng nhập hoặc mật khẩu không đúng" });

        var token = _jwt.CreateToken(user);

        return Ok(new
        {
            token,
            user = new
            {
                user.Id,
                user.Username,
                user.Role
            }
        });
    }
}
