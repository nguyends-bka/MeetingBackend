using System.Security.Claims;
using MeetingBackend.Data;
using MeetingBackend.DTOs.Organization;
using MeetingBackend.Entities;
using MeetingBackend.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MeetingBackend.Controllers;

[ApiController]
[Route("api/admin/organization-units")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class OrganizationController : ControllerBase
{
    private readonly AppDbContext _db;

    public OrganizationController(AppDbContext db)
    {
        _db = db;
    }

    private string CurrentActor() =>
        User.FindFirstValue("username")
        ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue(ClaimTypes.Name)
        ?? "unknown";

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var units = await _db.OrganizationUnits
            .AsNoTracking()
            .OrderBy(x => x.Level)
            .ThenBy(x => x.Name)
            .ToListAsync();

        var unitById = units.ToDictionary(x => x.Id);

        var memberCounts = await _db.Users
            .AsNoTracking()
            .Where(x => x.OrganizationUnitId.HasValue)
            .GroupBy(x => x.OrganizationUnitId!.Value)
            .Select(g => new { UnitId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UnitId, x => x.Count);

        var response = units.Select(x => ToDto(x, unitById, memberCounts)).ToList();
        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var unit = await _db.OrganizationUnits.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (unit == null)
            return NotFound("Organization unit not found");

        var unitById = await _db.OrganizationUnits
            .AsNoTracking()
            .ToDictionaryAsync(x => x.Id);

        var memberCount = await _db.Users.AsNoTracking().CountAsync(x => x.OrganizationUnitId == id);
        var dto = ToDto(unit, unitById, new Dictionary<Guid, int> { [id] = memberCount });
        return Ok(dto);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] OrganizationUnitUpsertRequestDto dto)
    {
        var validationError = await ValidatePayloadAsync(dto, null);
        if (validationError != null) return validationError;

        var actor = CurrentActor();
        var now = DateTime.UtcNow;
        var normalizedCode = dto.Code.Trim().ToUpperInvariant();

        var entity = new OrganizationUnit
        {
            Id = Guid.NewGuid(),
            Name = dto.Name.Trim(),
            Code = normalizedCode,
            Level = dto.Level,
            ParentId = dto.Level == 1 ? null : dto.ParentId,
            Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
            IsActive = dto.IsActive,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = actor,
            UpdatedBy = actor,
        };

        _db.OrganizationUnits.Add(entity);
        await _db.SaveChangesAsync();
        return Ok(new { id = entity.Id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] OrganizationUnitUpsertRequestDto dto)
    {
        var entity = await _db.OrganizationUnits.FirstOrDefaultAsync(x => x.Id == id);
        if (entity == null)
            return NotFound("Organization unit not found");

        var validationError = await ValidatePayloadAsync(dto, id);
        if (validationError != null) return validationError;

        entity.Name = dto.Name.Trim();
        entity.Code = dto.Code.Trim().ToUpperInvariant();
        entity.Level = dto.Level;
        entity.ParentId = dto.Level == 1 ? null : dto.ParentId;
        entity.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
        entity.IsActive = dto.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = CurrentActor();

        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var entity = await _db.OrganizationUnits.FirstOrDefaultAsync(x => x.Id == id);
        if (entity == null)
            return NotFound("Organization unit not found");

        var hasChildren = await _db.OrganizationUnits.AsNoTracking().AnyAsync(x => x.ParentId == id);
        if (hasChildren)
            return BadRequest("Cannot delete unit that still has child units");

        var hasMembers = await _db.Users.AsNoTracking().AnyAsync(x => x.OrganizationUnitId == id);
        if (hasMembers)
            return BadRequest("Cannot delete unit that still has assigned users");

        _db.OrganizationUnits.Remove(entity);
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    private async Task<IActionResult?> ValidatePayloadAsync(OrganizationUnitUpsertRequestDto dto, Guid? currentId)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest("Name is required");
        if (string.IsNullOrWhiteSpace(dto.Code))
            return BadRequest("Code is required");
        if (dto.Level is < 1 or > 5)
            return BadRequest("Level must be between 1 and 5");

        var normalizedCode = dto.Code.Trim().ToUpperInvariant();
        var codeExists = await _db.OrganizationUnits
            .AsNoTracking()
            .AnyAsync(x => x.Code == normalizedCode && (!currentId.HasValue || x.Id != currentId.Value));
        if (codeExists)
            return Conflict("Code already exists");

        if (dto.Level == 1)
            return null;

        if (!dto.ParentId.HasValue || dto.ParentId == Guid.Empty)
            return BadRequest("ParentId is required when level > 1");
        if (currentId.HasValue && dto.ParentId.Value == currentId.Value)
            return BadRequest("ParentId cannot be same as Id");

        var parent = await _db.OrganizationUnits
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == dto.ParentId.Value);
        if (parent == null)
            return BadRequest("Parent unit not found");
        if (parent.Level != dto.Level - 1)
            return BadRequest("Parent level must be exactly child level - 1");

        return null;
    }

    private static OrganizationUnitResponseDto ToDto(
        OrganizationUnit unit,
        IReadOnlyDictionary<Guid, OrganizationUnit> unitById,
        IReadOnlyDictionary<Guid, int> memberCounts)
    {
        string? parentName = null;
        if (unit.ParentId.HasValue && unitById.TryGetValue(unit.ParentId.Value, out var parent))
        {
            parentName = parent.Name;
        }

        return new OrganizationUnitResponseDto
        {
            Id = unit.Id,
            Name = unit.Name,
            Code = unit.Code,
            Level = unit.Level,
            ParentId = unit.ParentId,
            ParentName = parentName,
            Description = unit.Description,
            IsActive = unit.IsActive,
            MemberCount = memberCounts.TryGetValue(unit.Id, out var count) ? count : 0,
            CreatedAt = unit.CreatedAt,
            UpdatedAt = unit.UpdatedAt,
            CreatedBy = unit.CreatedBy,
            UpdatedBy = unit.UpdatedBy,
        };
    }
}
