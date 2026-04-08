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

    public AuthController(AppDbContext db, JwtTokenService jwt)
    {
        _db = db;
        _jwt = jwt;
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
        // Lưu ý:
        // - Thiết bị gửi embedding dạng vector (float[]) cho frontend.
        // - Frontend gửi embedding về backend để so khớp với FaceEmbedding đã lưu trong DB.
        if (req.Embedding == null || req.Embedding.Length == 0)
            return BadRequest(new { message = "Embedding không hợp lệ" });

        // Cosine similarity (giá trị -1..1). Ngưỡng cần tinh chỉnh theo model thiết bị.
        const float threshold = 0.85f;
        var candidates = await _db.Users
            .Where(u => u.FaceEmbedding != null && u.FaceEmbedding.Length == req.Embedding.Length)
            .ToListAsync();

        if (candidates.Count == 0)
            return Unauthorized(new { message = "Không có dữ liệu khuôn mặt phù hợp" });

        User? bestUser = null;
        float bestScore = float.MinValue;

        foreach (var candidate in candidates)
        {
            if (candidate.FaceEmbedding == null) continue;
            var score = CosineSimilarity(candidate.FaceEmbedding, req.Embedding);
            if (score > bestScore)
            {
                bestScore = score;
                bestUser = candidate;
            }
        }

        if (bestUser == null || bestScore < threshold)
            return Unauthorized(new { message = "Face không khớp" });

        var token = _jwt.CreateToken(bestUser);

        var response = new LoginResponseDto
        {
            Token = token,
            User = UserMapper.ToAuthUserDto(bestUser)
        };

        return Ok(response);
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        double dot = 0;
        double normA = 0;
        double normB = 0;

        for (int i = 0; i < a.Length; i++)
        {
            dot += (double)a[i] * b[i];
            normA += (double)a[i] * a[i];
            normB += (double)b[i] * b[i];
        }

        if (normA <= 0 || normB <= 0) return 0;
        return (float)(dot / (Math.Sqrt(normA) * Math.Sqrt(normB)));
    }
}
