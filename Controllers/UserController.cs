using BCrypt.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using MeetingBackend.Data;
using MeetingBackend.DTOs.Catalog;
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

    // ==========================
    // DANH MỤC QUỐC GIA (public trong scope đăng nhập)
    // ==========================
    /// <summary>Lấy danh sách Countries đang active để hiển thị dropdown.</summary>
    [HttpGet("countries")]
    public async Task<IActionResult> ListCountries()
    {
        var items = await _db.Countries
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.CountryName)
            .Select(x => new CountryDto { Code = x.Code, CountryName = x.CountryName })
            .ToListAsync();
        return Ok(items);
    }

    // ==========================
    // DANH MỤC NGÔN NGỮ (public trong scope đăng nhập)
    // ==========================
    /// <summary>Lấy danh sách Languages đang active để hiển thị dropdown.</summary>
    [HttpGet("languages")]
    public async Task<IActionResult> ListLanguages()
    {
        var items = await _db.Languages
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.LanguageName)
            .Select(x => new LanguageDto { Code = x.Code, LanguageName = x.LanguageName })
            .ToListAsync();
        return Ok(items);
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

        var userGuid = user.Id;

        var countries = await _db.UserCountries
            .AsNoTracking()
            .Where(uc => uc.UserId == userGuid)
            .Join(_db.Countries, uc => uc.CountryCode, c => c.Code,
                (uc, c) => new UserCountryResponseDto { Code = c.Code, CountryName = c.CountryName })
            .ToListAsync();

        var languages = await _db.UserLanguages
            .AsNoTracking()
            .Where(ul => ul.UserId == userGuid)
            .Join(_db.Languages, ul => ul.LanguageCode, l => l.Code,
                (ul, l) => new UserLanguageResponseDto
                {
                    Code = l.Code,
                    LanguageName = l.LanguageName,
                    IsPrimary = ul.IsPrimary
                })
            .OrderByDescending(l => l.IsPrimary)
            .ThenBy(l => l.LanguageName)
            .ToListAsync();

        var response = UserMapper.ToUserProfileDto(user, orgName, countries, languages);
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
    // CẬP NHẬT THÔNG TIN CÁ NHÂN
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

        if (string.IsNullOrWhiteSpace(request.FullName))
            return BadRequest(new { message = "Họ và tên là bắt buộc" });

        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new { message = "Email là bắt buộc" });

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

        if (!string.IsNullOrWhiteSpace(request.Avatar))
        {
            try
            {
                _ = Convert.FromBase64String(request.Avatar);
            }
            catch (FormatException)
            {
                return BadRequest(new { message = "Avatar phải là chuỗi Base64 hợp lệ" });
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

        // ── Validate CountryCodes ─────────────────────────────────────────────
        if (request.CountryCodes != null)
        {
            var distinctCodes = request.CountryCodes.Select(c => c.Trim().ToUpper()).Distinct().ToList();
            if (distinctCodes.Count != request.CountryCodes.Count)
                return BadRequest(new { message = "Danh sách quốc gia có mã bị trùng" });

            if (distinctCodes.Count > 0)
            {
                var validCodes = await _db.Countries
                    .AsNoTracking()
                    .Where(c => distinctCodes.Contains(c.Code) && c.IsActive)
                    .Select(c => c.Code)
                    .ToListAsync();

                var invalidCodes = distinctCodes.Except(validCodes).ToList();
                if (invalidCodes.Count > 0)
                    return BadRequest(new { message = $"Mã quốc gia không hợp lệ hoặc đã bị vô hiệu hóa: {string.Join(", ", invalidCodes)}" });
            }
        }

        // ── Validate Languages ────────────────────────────────────────────────
        if (request.Languages != null)
        {
            var langCodes = request.Languages.Select(l => l.Code.Trim().ToLower()).ToList();
            if (langCodes.Distinct().Count() != langCodes.Count)
                return BadRequest(new { message = "Danh sách ngôn ngữ có mã bị trùng" });

            if (request.Languages.Count > 0)
            {
                var primaryCount = request.Languages.Count(l => l.IsPrimary);
                if (primaryCount == 0)
                    return BadRequest(new { message = "Phải chọn đúng 1 ngôn ngữ ưu tiên (IsPrimary = true)" });
                if (primaryCount > 1)
                    return BadRequest(new { message = "Chỉ được chọn 1 ngôn ngữ ưu tiên (IsPrimary = true)" });

                var validLangCodes = await _db.Languages
                    .AsNoTracking()
                    .Where(l => langCodes.Contains(l.Code) && l.IsActive)
                    .Select(l => l.Code)
                    .ToListAsync();

                var invalidLangCodes = langCodes.Except(validLangCodes).ToList();
                if (invalidLangCodes.Count > 0)
                    return BadRequest(new { message = $"Mã ngôn ngữ không hợp lệ hoặc đã bị vô hiệu hóa: {string.Join(", ", invalidLangCodes)}" });
            }
        }

        // ── Cập nhật thông tin cơ bản ─────────────────────────────────────────
        user.FullName = string.IsNullOrWhiteSpace(request.FullName) ? null : request.FullName.Trim();
        user.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLowerInvariant();
        user.Position = string.IsNullOrWhiteSpace(request.Position) ? null : request.Position.Trim();
        user.AcademicRank = string.IsNullOrWhiteSpace(request.AcademicRank) ? null : request.AcademicRank.Trim();
        user.AcademicDegree = string.IsNullOrWhiteSpace(request.AcademicDegree) ? null : request.AcademicDegree.Trim();
        user.OrganizationUnitId = request.OrganizationUnitId;
        user.Avatar = string.IsNullOrWhiteSpace(request.Avatar) ? null : request.Avatar.Trim();

        // ── Cập nhật UserCountries (replace hoàn toàn nếu được gửi) ──────────
        if (request.CountryCodes != null)
        {
            var existingCountries = await _db.UserCountries
                .Where(uc => uc.UserId == user.Id)
                .ToListAsync();

            var newCodes = request.CountryCodes.Select(c => c.Trim().ToUpper()).Distinct().ToHashSet();

            // 1. Xóa các quốc gia không còn trong danh sách mới
            var toDelete = existingCountries.Where(ec => !newCodes.Contains(ec.CountryCode)).ToList();
            _db.UserCountries.RemoveRange(toDelete);

            // 2. Thêm mới các quốc gia chưa tồn tại
            var existingCodes = existingCountries.Select(ec => ec.CountryCode).ToHashSet();
            foreach (var code in newCodes)
            {
                if (!existingCodes.Contains(code))
                {
                    _db.UserCountries.Add(new UserCountry { UserId = user.Id, CountryCode = code });
                }
            }
        }

        // ── Cập nhật UserLanguages (replace hoàn toàn nếu được gửi) ─────────
        if (request.Languages != null)
        {
            var existingLangs = await _db.UserLanguages
                .Where(ul => ul.UserId == user.Id)
                .ToListAsync();

            var newLangs = request.Languages.ToList();
            var newCodes = newLangs.Select(l => l.Code.Trim().ToLower()).ToHashSet();

            // 1. Xóa các ngôn ngữ không còn trong danh sách mới
            var toDelete = existingLangs.Where(el => !newCodes.Contains(el.LanguageCode)).ToList();
            _db.UserLanguages.RemoveRange(toDelete);

            // 2. Cập nhật hoặc thêm mới các ngôn ngữ
            foreach (var lang in newLangs)
            {
                var code = lang.Code.Trim().ToLower();
                var existing = existingLangs.FirstOrDefault(el => el.LanguageCode == code);

                if (existing != null)
                {
                    // Cập nhật trường IsPrimary của bản ghi hiện có
                    existing.IsPrimary = lang.IsPrimary;
                }
                else
                {
                    // Thêm bản ghi mới
                    _db.UserLanguages.Add(new UserLanguage
                    {
                        UserId = user.Id,
                        LanguageCode = code,
                        IsPrimary = lang.IsPrimary
                    });
                }
            }
        }

        await _db.SaveChangesAsync();

        // Lấy orgName để trả về response
        string? orgName = null;
        if (user.OrganizationUnitId.HasValue)
        {
            orgName = await _db.OrganizationUnits
                .AsNoTracking()
                .Where(x => x.Id == user.OrganizationUnitId.Value)
                .Select(x => x.Name)
                .FirstOrDefaultAsync();
        }

        // Lấy countries/languages mới nhất để trả về response
        var updatedCountries = await _db.UserCountries
            .AsNoTracking()
            .Where(uc => uc.UserId == user.Id)
            .Join(_db.Countries, uc => uc.CountryCode, c => c.Code,
                (uc, c) => new UserCountryResponseDto { Code = c.Code, CountryName = c.CountryName })
            .ToListAsync();

        var updatedLanguages = await _db.UserLanguages
            .AsNoTracking()
            .Where(ul => ul.UserId == user.Id)
            .Join(_db.Languages, ul => ul.LanguageCode, l => l.Code,
                (ul, l) => new UserLanguageResponseDto
                {
                    Code = l.Code,
                    LanguageName = l.LanguageName,
                    IsPrimary = ul.IsPrimary
                })
            .OrderByDescending(l => l.IsPrimary)
            .ThenBy(l => l.LanguageName)
            .ToListAsync();

        var response = new UpdateProfileResponseDto
        {
            Message = "Cập nhật thông tin thành công",
            User = UserMapper.ToUserDto(user, orgName, updatedCountries, updatedLanguages)
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

        if (!TryBuildMultiAngleEmbedding(request, out var mergedEmbedding, out var errorMessage))
            return BadRequest(new { message = errorMessage });

        if (!Guid.TryParse(userId, out var userGuid))
            return Unauthorized("User identity not found");

        var userExists = await _db.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == userGuid);

        if (!userExists)
            return NotFound("User not found");

        // Không load entity đầy đủ để tránh lỗi materialize dữ liệu FaceEmbedding cũ (1D).
        // Chỉ attach stub và cập nhật riêng trường FaceEmbedding.
        var user = new User { Id = userGuid };
        _db.Users.Attach(user);
        user.FaceEmbedding = mergedEmbedding;
        _db.Entry(user).Property(x => x.FaceEmbedding).IsModified = true;
        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Đăng ký khuôn mặt thành công",
            hasFaceEmbedding = true,
        });
    }

    private static bool TryBuildMultiAngleEmbedding(
        RegisterFaceEmbeddingRequestDto request,
        out short[,] mergedEmbedding,
        out string errorMessage)
    {
        mergedEmbedding = new short[0, 0];
        errorMessage = string.Empty;

        if (request.Straight == null || request.Right == null || request.Left == null || request.Up == null)
        {
            errorMessage = "Thiếu embedding cho một trong các góc: straight/right/left/up";
            return false;
        }

        if (request.Straight.Length == 0 || request.Right.Length == 0 || request.Left.Length == 0 || request.Up.Length == 0)
        {
            errorMessage = "Embedding không hợp lệ";
            return false;
        }

        var dim = request.Straight.Length;
        if (request.Right.Length != dim || request.Left.Length != dim || request.Up.Length != dim)
        {
            errorMessage = "Embedding các góc phải cùng kích thước";
            return false;
        }

        mergedEmbedding = new short[4, dim];
        for (var i = 0; i < dim; i++)
        {
            var straight = request.Straight[i];
            var right = request.Right[i];
            var left = request.Left[i];
            var up = request.Up[i];

            if (straight < -128 || straight > 127 ||
                right < -128 || right > 127 ||
                left < -128 || left > 127 ||
                up < -128 || up > 127)
            {
                errorMessage = "Mỗi phần tử embedding phải nằm trong khoảng -128..127";
                return false;
            }

            mergedEmbedding[0, i] = (short)straight;
            mergedEmbedding[1, i] = (short)right;
            mergedEmbedding[2, i] = (short)left;
            mergedEmbedding[3, i] = (short)up;
        }
        return true;
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

    [HttpGet("lookup-languages")]
    public async Task<IActionResult> LookupLanguages([FromQuery] string usernames)
    {
        if (string.IsNullOrWhiteSpace(usernames))
            return Ok(new Dictionary<string, UserLanguagesLookupDto>());

        var cleanNames = usernames
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim().ToLower())
            .Distinct()
            .ToList();

        var guids = new List<Guid>();
        var nameList = new List<string>();

        foreach (var item in cleanNames)
        {
            if (Guid.TryParse(item, out var guid))
            {
                guids.Add(guid);
            }
            else
            {
                nameList.Add(item);
            }
        }

        // Lấy tất cả các ngôn ngữ của người dùng khớp theo Username hoặc UserId
        var userLangsList = await (from ul in _db.UserLanguages
                                   join u in _db.Users on ul.UserId equals u.Id
                                   where nameList.Contains(u.Username.ToLower()) || guids.Contains(ul.UserId)
                                   select new
                                   {
                                       Username = u.Username.ToLower(),
                                       UserIdStr = u.Id.ToString().ToLower(),
                                       ul.LanguageCode,
                                       ul.IsPrimary
                                   })
                                   .ToListAsync();

        var resultDict = new Dictionary<string, UserLanguagesLookupDto>();

        // Gom nhóm theo UserId
        var groupedByUserId = userLangsList.GroupBy(x => x.UserIdStr);
        foreach (var group in groupedByUserId)
        {
            var userIdStr = group.Key;
            var username = group.First().Username;

            var preferred = group.FirstOrDefault(x => x.IsPrimary)?.LanguageCode 
                            ?? group.FirstOrDefault()?.LanguageCode 
                            ?? "vi";
            var allLangs = group.Select(x => x.LanguageCode).Distinct().ToList();

            var dto = new UserLanguagesLookupDto
            {
                PreferredLanguage = preferred,
                Languages = allLangs
            };

            // Lưu cả key là username và key là UserIdStr để client có thể tìm thấy theo cách nào cũng được
            resultDict[userIdStr] = dto;
            resultDict[username] = dto;
        }

        return Ok(resultDict);
    }
}
