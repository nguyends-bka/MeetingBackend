using BCrypt.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using MeetingBackend.Data;
using MeetingBackend.DTOs.User;
using MeetingBackend.Entities;
using MeetingBackend.Mappers;

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
    /// <summary>Tra cứu user theo username (để mời họp, hiển thị họ tên).</summary>
    [HttpGet("lookup")]
    public async Task<IActionResult> LookupByUsername([FromQuery] string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return BadRequest(new { message = "Username không được để trống" });

        var u = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Username.ToLower() == username.Trim().ToLower());
        if (u == null)
            return NotFound(new { message = "Không tìm thấy người dùng" });

        return Ok(new UserLookupByUsernameDto
        {
            UserId = u.Id.ToString(),
            Username = u.Username,
            FullName = string.IsNullOrWhiteSpace(u.FullName) ? null : u.FullName.Trim()
        });
    }

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue(ClaimTypes.Name);

        if (string.IsNullOrEmpty(userId))
            return Unauthorized("User identity not found");

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id.ToString() == userId);

        if (user == null)
            return NotFound("User not found");

        string? orgName = null;
        if (user.OrganizationUnitId.HasValue)
        {
            orgName = await _db.OrganizationUnits
                .AsNoTracking()
                .Where(x => x.Id == user.OrganizationUnitId.Value)
                .Select(x => x.Name)
                .FirstOrDefaultAsync();
        }

        var response = UserMapper.ToUserProfileDto(user, orgName);
        return Ok(response);
    }

    [HttpGet("organization-units")]
    public async Task<IActionResult> ListOrganizationUnits()
    {
        var units = await _db.OrganizationUnits
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Level)
            .ThenBy(x => x.Name)
            .Select(x => new OrganizationUnitOptionDto
            {
                Id = x.Id,
                Name = x.Name,
                Level = x.Level,
                ParentId = x.ParentId,
                IsActive = x.IsActive,
            })
            .ToListAsync();
        return Ok(units);
    }

    // ==========================
    // CẬP NHẬT THÔNG TIN CÁ NHÂN (FullName, Email)
    // ==========================
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequestDto request)
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

        if (!string.IsNullOrWhiteSpace(request.AcademicRank)
            && request.AcademicRank != "GS"
            && request.AcademicRank != "PGS")
        {
            return BadRequest(new { message = "Học hàm chỉ nhận GS hoặc PGS" });
        }

        if (!string.IsNullOrWhiteSpace(request.AcademicDegree)
            && request.AcademicDegree != "TS"
            && request.AcademicDegree != "ThS"
            && request.AcademicDegree != "CN"
            && request.AcademicDegree != "KS")
        {
            return BadRequest(new { message = "Học vị chỉ nhận TS, ThS, CN hoặc KS" });
        }

        if (!string.IsNullOrWhiteSpace(request.FaceTemplate))
        {
            try
            {
                _ = Convert.FromBase64String(request.FaceTemplate);
            }
            catch (FormatException)
            {
                return BadRequest(new { message = "Face template phải là chuỗi Base64 hợp lệ" });
            }
        }

        if (request.OrganizationUnitId.HasValue)
        {
            var unitExists = await _db.OrganizationUnits
                .AsNoTracking()
                .AnyAsync(x => x.Id == request.OrganizationUnitId.Value);
            if (!unitExists)
            {
                return BadRequest(new { message = "Đơn vị công tác không tồn tại" });
            }
        }

        // Cập nhật thông tin
        if (request.FullName != null)
            user.FullName = string.IsNullOrWhiteSpace(request.FullName) ? null : request.FullName.Trim();

        if (request.Email != null)
            user.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLower();

        if (request.Position != null)
            user.Position = string.IsNullOrWhiteSpace(request.Position) ? null : request.Position.Trim();

        if (request.AcademicRank != null)
            user.AcademicRank = string.IsNullOrWhiteSpace(request.AcademicRank) ? null : request.AcademicRank.Trim();

        if (request.AcademicDegree != null)
            user.AcademicDegree = string.IsNullOrWhiteSpace(request.AcademicDegree) ? null : request.AcademicDegree.Trim();

        if (request.OrganizationUnitId != null)
            user.OrganizationUnitId = request.OrganizationUnitId;

        if (request.FaceTemplate != null)
            user.FaceTemplate = string.IsNullOrWhiteSpace(request.FaceTemplate) ? null : request.FaceTemplate.Trim();

        await _db.SaveChangesAsync();

        string? orgName = null;
        if (user.OrganizationUnitId.HasValue)
        {
            orgName = await _db.OrganizationUnits
                .AsNoTracking()
                .Where(x => x.Id == user.OrganizationUnitId.Value)
                .Select(x => x.Name)
                .FirstOrDefaultAsync();
        }

        var response = new UpdateProfileResponseDto
        {
            Message = "Cập nhật thông tin thành công",
            User = UserMapper.ToUserDto(user, orgName)
        };

        return Ok(response);
    }

    /// <summary>Lưu embedding khuôn mặt từ thiết bị (sau WebSocket /registerface) cho user đang đăng nhập.</summary>
    [HttpPut("profile/face-embedding")]
    public async Task<IActionResult> RegisterFaceEmbedding([FromBody] RegisterFaceEmbeddingRequestDto request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue(ClaimTypes.Name);

        if (string.IsNullOrEmpty(userId))
            return Unauthorized("User identity not found");

        if (request.Embedding == null || request.Embedding.Length == 0)
            return BadRequest(new { message = "Embedding không hợp lệ" });

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id.ToString() == userId);

        if (user == null)
            return NotFound("User not found");

        user.FaceEmbedding = request.Embedding;
        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Đăng ký khuôn mặt thành công",
            hasFaceEmbedding = true,
        });
    }

    // ==========================
    // ĐỔI MẬT KHẨU
    // ==========================
    [HttpPut("profile/password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestDto request)
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

        var response = new ChangePasswordResponseDto
        {
            Message = "Đổi mật khẩu thành công"
        };

        return Ok(response);
    }
}
