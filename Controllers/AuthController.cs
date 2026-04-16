using BCrypt.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MeetingBackend.Constants;
using MeetingBackend.Data;
using MeetingBackend.DTOs.Auth;
using MeetingBackend.Entities;
using MeetingBackend.Mappers;
using MeetingBackend.Services;

namespace MeetingBackend.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly JwtTokenService _jwt;
    private readonly IFaceAuthService _faceAuthService;

    public AuthController(AppDbContext db, JwtTokenService jwt, IFaceAuthService faceAuthService)
    {
        _db = db;
        _jwt = jwt;
        _faceAuthService = faceAuthService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequestDto req)
    {
        if (await _db.Users.AnyAsync(u => u.Username == req.Username))
            return BadRequest(new { message = "Tên đăng nhập đã tồn tại. Vui lòng chọn tên khác." });

        if (!string.IsNullOrWhiteSpace(req.AcademicRank)
            && req.AcademicRank != "GS"
            && req.AcademicRank != "PGS")
        {
            return BadRequest(new { message = "Học hàm chỉ nhận GS hoặc PGS" });
        }

        if (!string.IsNullOrWhiteSpace(req.AcademicDegree)
            && req.AcademicDegree != "TS"
            && req.AcademicDegree != "ThS"
            && req.AcademicDegree != "CN"
            && req.AcademicDegree != "KS")
        {
            return BadRequest(new { message = "Học vị chỉ nhận TS, ThS, CN hoặc KS" });
        }

        if (!string.IsNullOrWhiteSpace(req.FaceTemplate))
        {
            try
            {
                _ = Convert.FromBase64String(req.FaceTemplate);
            }
            catch (FormatException)
            {
                return BadRequest(new { message = "Face template phải là chuỗi Base64 hợp lệ" });
            }
        }

        if (req.OrganizationUnitId.HasValue)
        {
            var ouExists = await _db.OrganizationUnits
                .AsNoTracking()
                .AnyAsync(x => x.Id == req.OrganizationUnitId.Value);
            if (!ouExists)
            {
                return BadRequest(new { message = "Đơn vị công tác không tồn tại" });
            }
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = req.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            Role = Roles.User,
            FullName = string.IsNullOrWhiteSpace(req.FullName) ? null : req.FullName.Trim(),
            Email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim().ToLower(),
            Position = string.IsNullOrWhiteSpace(req.Position) ? null : req.Position.Trim(),
            AcademicRank = string.IsNullOrWhiteSpace(req.AcademicRank) ? null : req.AcademicRank.Trim(),
            AcademicDegree = string.IsNullOrWhiteSpace(req.AcademicDegree) ? null : req.AcademicDegree.Trim(),
            OrganizationUnitId = req.OrganizationUnitId,
            FaceTemplate = string.IsNullOrWhiteSpace(req.FaceTemplate) ? null : req.FaceTemplate.Trim(),
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Registration successful" });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequestDto req)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Username == req.Username);

        if (user == null)
            return Unauthorized(new { message = "Tên đăng nhập hoặc mật khẩu không đúng" });

        if (!BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return Unauthorized(new { message = "Tên đăng nhập hoặc mật khẩu không đúng" });

        // JWT includes role dynamically from database
        var token = _jwt.CreateToken(user);

        var response = new LoginResponseDto
        {
            Token = token,
            User = UserMapper.ToAuthUserDto(user)
        };

        return Ok(response);
    }

    [HttpPost("login/face")]
    public async Task<IActionResult> LoginWithFace(FaceLoginRequestDto req)
    {
        var authResult = await _faceAuthService.AuthenticateAsync(req.Embedding ?? Array.Empty<int>(), HttpContext.RequestAborted);

        if (!authResult.IsSuccess)
        {
            var message = authResult.ErrorMessage ?? "Face không hợp lệ";
            if (string.Equals(message, "Embedding không hợp lệ", StringComparison.Ordinal)
                || message.Contains("-128..127", StringComparison.Ordinal))
            {
                return BadRequest(new { message });
            }

            return Unauthorized(new { message });
        }

        var bestUser = authResult.User!;
        var token = _jwt.CreateToken(bestUser);

        var response = new LoginResponseDto
        {
            Token = token,
            User = UserMapper.ToAuthUserDto(bestUser)
        };

        return Ok(response);
    }
}
