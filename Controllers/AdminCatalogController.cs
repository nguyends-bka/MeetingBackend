using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MeetingBackend.Data;
using MeetingBackend.DTOs.Admin;
using MeetingBackend.DTOs.Catalog;
using MeetingBackend.Entities;
using MeetingBackend.Policies;

namespace MeetingBackend.Controllers;

/// <summary>
/// Admin quản lý danh mục Countries và Languages.
/// Không xóa cứng — dùng IsActive = false để vô hiệu hóa.
/// </summary>
[ApiController]
[Route("api/admin/catalog")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class AdminCatalogController : ControllerBase
{
    private readonly AppDbContext _db;

    public AdminCatalogController(AppDbContext db)
    {
        _db = db;
    }

    // ════════════════════════════════════════════════
    // COUNTRIES
    // ════════════════════════════════════════════════

    /// <summary>Lấy toàn bộ danh sách Countries (kể cả IsActive = false) để Admin quản lý.</summary>
    [HttpGet("countries")]
    public async Task<IActionResult> GetAllCountries()
    {
        var items = await _db.Countries
            .AsNoTracking()
            .OrderBy(x => x.CountryName)
            .Select(x => new { x.Code, x.CountryName, x.IsActive })
            .ToListAsync();
        return Ok(items);
    }

    /// <summary>Thêm một Country mới.</summary>
    [HttpPost("countries")]
    public async Task<IActionResult> CreateCountry([FromBody] UpsertCountryRequestDto request)
    {
        var code = request.Code.Trim().ToUpper();
        if (string.IsNullOrWhiteSpace(code))
            return BadRequest(new { message = "Code không được để trống" });
        if (string.IsNullOrWhiteSpace(request.CountryName))
            return BadRequest(new { message = "CountryName không được để trống" });

        var exists = await _db.Countries.AnyAsync(c => c.Code == code);
        if (exists)
            return Conflict(new { message = $"Country với code '{code}' đã tồn tại" });

        var entity = new Country
        {
            Code = code,
            CountryName = request.CountryName.Trim(),
            IsActive = request.IsActive
        };
        _db.Countries.Add(entity);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAllCountries), new { },
            new { entity.Code, entity.CountryName, entity.IsActive });
    }

    /// <summary>Cập nhật CountryName và/hoặc IsActive. Không cho đổi Code.</summary>
    [HttpPut("countries/{code}")]
    public async Task<IActionResult> UpdateCountry(string code, [FromBody] UpsertCountryRequestDto request)
    {
        var normalizedCode = code.Trim().ToUpper();
        var entity = await _db.Countries.FindAsync(normalizedCode);
        if (entity == null)
            return NotFound(new { message = $"Country '{normalizedCode}' không tồn tại" });

        if (string.IsNullOrWhiteSpace(request.CountryName))
            return BadRequest(new { message = "CountryName không được để trống" });

        entity.CountryName = request.CountryName.Trim();
        entity.IsActive = request.IsActive;
        await _db.SaveChangesAsync();

        return Ok(new { entity.Code, entity.CountryName, entity.IsActive });
    }

    // ════════════════════════════════════════════════
    // LANGUAGES
    // ════════════════════════════════════════════════

    /// <summary>Lấy toàn bộ danh sách Languages (kể cả IsActive = false) để Admin quản lý.</summary>
    [HttpGet("languages")]
    public async Task<IActionResult> GetAllLanguages()
    {
        var items = await _db.Languages
            .AsNoTracking()
            .OrderBy(x => x.LanguageName)
            .Select(x => new { x.Code, x.LanguageName, x.IsActive })
            .ToListAsync();
        return Ok(items);
    }

    /// <summary>Thêm một Language mới.</summary>
    [HttpPost("languages")]
    public async Task<IActionResult> CreateLanguage([FromBody] UpsertLanguageRequestDto request)
    {
        var code = request.Code.Trim().ToLower();
        if (string.IsNullOrWhiteSpace(code))
            return BadRequest(new { message = "Code không được để trống" });
        if (string.IsNullOrWhiteSpace(request.LanguageName))
            return BadRequest(new { message = "LanguageName không được để trống" });

        var exists = await _db.Languages.AnyAsync(l => l.Code == code);
        if (exists)
            return Conflict(new { message = $"Language với code '{code}' đã tồn tại" });

        var entity = new Language
        {
            Code = code,
            LanguageName = request.LanguageName.Trim(),
            IsActive = request.IsActive
        };
        _db.Languages.Add(entity);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAllLanguages), new { },
            new { entity.Code, entity.LanguageName, entity.IsActive });
    }

    /// <summary>Cập nhật LanguageName và/hoặc IsActive. Không cho đổi Code.</summary>
    [HttpPut("languages/{code}")]
    public async Task<IActionResult> UpdateLanguage(string code, [FromBody] UpsertLanguageRequestDto request)
    {
        var normalizedCode = code.Trim().ToLower();
        var entity = await _db.Languages.FindAsync(normalizedCode);
        if (entity == null)
            return NotFound(new { message = $"Language '{normalizedCode}' không tồn tại" });

        if (string.IsNullOrWhiteSpace(request.LanguageName))
            return BadRequest(new { message = "LanguageName không được để trống" });

        entity.LanguageName = request.LanguageName.Trim();
        entity.IsActive = request.IsActive;
        await _db.SaveChangesAsync();

        return Ok(new { entity.Code, entity.LanguageName, entity.IsActive });
    }
}
