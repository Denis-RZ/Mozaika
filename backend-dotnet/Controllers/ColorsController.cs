using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Mozaika.Api.Contracts;
using Mozaika.Api.Database;
using Mozaika.Api.Database.Entities;
using Mozaika.Api.Security;
using Mozaika.Api.Services;

namespace Mozaika.Api.Controllers;

[ApiController]
[Route("api/colors")]
public sealed class ColorsController(MozaikaDbContext dbContext) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ColorReadResponse>>> List(
        [FromQuery(Name = "include_inactive")] bool includeInactive = false
    )
    {
        var query = dbContext.Colors
            .AsNoTracking()
            .OrderBy(item => item.Id)
            .AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(item => item.IsActive);
        }

        var payload = await query
            .Select(item => item.ToRead())
            .ToListAsync();

        return Ok(payload);
    }

    [HttpPost]
    public async Task<ActionResult<ColorReadResponse>> Create([FromBody] ColorCreateRequest payload)
    {
        var authError = RequireAnyRole(AppRoles.Admin);
        if (authError is not null)
        {
            return authError;
        }

        var parsed = ValidateCreatePayload(payload);
        if (!parsed.IsValid)
        {
            return BadRequest(new ApiError(parsed.Error!));
        }

        var duplicateExists = await dbContext.Colors.AnyAsync(item =>
            item.Name == parsed.Name ||
            item.RalCode == parsed.RalCode ||
            item.RgbHex == parsed.RgbHex
        );

        if (duplicateExists)
        {
            return Conflict(new ApiError("Цвет с таким названием, RAL или HEX уже существует."));
        }

        var entity = new ColorEntity
        {
            Name = parsed.Name!,
            RalCode = parsed.RalCode!,
            RgbHex = parsed.RgbHex!,
            IsActive = parsed.IsActive,
            CreatedAt = DateTime.UtcNow,
        };

        dbContext.Colors.Add(entity);
        await dbContext.SaveChangesAsync();

        return StatusCode(StatusCodes.Status201Created, entity.ToRead());
    }

    [HttpPost("bulk")]
    public async Task<ActionResult<List<ColorReadResponse>>> CreateBulk([FromBody] ColorBulkCreateRequest payload)
    {
        var authError = RequireAnyRole(AppRoles.Admin);
        if (authError is not null)
        {
            return authError;
        }

        if (payload.Colors.Count == 0)
        {
            return BadRequest(new ApiError("Список для пакетного импорта пуст."));
        }

        if (payload.Colors.Count > 500)
        {
            return BadRequest(new ApiError("Можно загрузить не более 500 цветов за раз."));
        }

        var existing = await dbContext.Colors.AsNoTracking().ToListAsync();
        var existingNames = existing.Select(item => item.Name).ToHashSet(StringComparer.Ordinal);
        var existingRals = existing.Select(item => item.RalCode).ToHashSet(StringComparer.Ordinal);
        var existingHex = existing.Select(item => item.RgbHex).ToHashSet(StringComparer.Ordinal);

        var entities = new List<ColorEntity>();

        foreach (var item in payload.Colors)
        {
            var parsed = ValidateCreatePayload(item);
            if (!parsed.IsValid)
            {
                return BadRequest(new ApiError(parsed.Error!));
            }

            if (existingNames.Contains(parsed.Name!) || existingRals.Contains(parsed.RalCode!) || existingHex.Contains(parsed.RgbHex!))
            {
                return Conflict(new ApiError($"Найден дубликат для цвета '{parsed.Name}'."));
            }

            existingNames.Add(parsed.Name!);
            existingRals.Add(parsed.RalCode!);
            existingHex.Add(parsed.RgbHex!);

            entities.Add(new ColorEntity
            {
                Name = parsed.Name!,
                RalCode = parsed.RalCode!,
                RgbHex = parsed.RgbHex!,
                IsActive = parsed.IsActive,
                CreatedAt = DateTime.UtcNow,
            });
        }

        dbContext.Colors.AddRange(entities);
        await dbContext.SaveChangesAsync();

        return StatusCode(StatusCodes.Status201Created, entities.Select(entity => entity.ToRead()).ToList());
    }

    [HttpPatch("{colorId:int}")]
    public async Task<ActionResult<ColorReadResponse>> Update(int colorId, [FromBody] ColorUpdateRequest payload)
    {
        var authError = RequireAnyRole(AppRoles.Admin);
        if (authError is not null)
        {
            return authError;
        }

        if (colorId <= 0)
        {
            return BadRequest(new ApiError("Некорректный id цвета."));
        }

        if (!payload.HasAnyValue())
        {
            return BadRequest(new ApiError("Нужно передать хотя бы одно поле для обновления."));
        }

        var entity = await dbContext.Colors.FirstOrDefaultAsync(item => item.Id == colorId);
        if (entity is null)
        {
            return NotFound(new ApiError("Цвет не найден."));
        }

        var nextName = entity.Name;
        var nextRal = entity.RalCode;
        var nextHex = entity.RgbHex;
        var nextIsActive = entity.IsActive;

        if (payload.Name is not null)
        {
            nextName = payload.Name.Trim();
            if (string.IsNullOrWhiteSpace(nextName))
            {
                return BadRequest(new ApiError("Название цвета обязательно."));
            }
        }

        if (payload.RalCode is not null)
        {
            nextRal = payload.RalCode.Trim();
            if (string.IsNullOrWhiteSpace(nextRal))
            {
                return BadRequest(new ApiError("RAL обязателен."));
            }
        }

        if (payload.RgbHex is not null)
        {
            try
            {
                nextHex = ColorMath.NormalizeHex(payload.RgbHex);
            }
            catch (ArgumentException ex)
            {
                return UnprocessableEntity(new ApiError(ex.Message));
            }
        }

        if (payload.IsActive is not null)
        {
            nextIsActive = payload.IsActive.Value;
        }

        var duplicateExists = await dbContext.Colors.AnyAsync(item =>
            item.Id != entity.Id &&
            (item.Name == nextName || item.RalCode == nextRal || item.RgbHex == nextHex)
        );

        if (duplicateExists)
        {
            return Conflict(new ApiError("Цвет с таким названием, RAL или HEX уже существует."));
        }

        entity.Name = nextName;
        entity.RalCode = nextRal;
        entity.RgbHex = nextHex;
        entity.IsActive = nextIsActive;

        await dbContext.SaveChangesAsync();
        return Ok(entity.ToRead());
    }

    [HttpDelete("{colorId:int}")]
    public async Task<ActionResult<ColorReadResponse>> Deactivate(int colorId)
    {
        var authError = RequireAnyRole(AppRoles.Admin);
        if (authError is not null)
        {
            return authError;
        }

        if (colorId <= 0)
        {
            return BadRequest(new ApiError("Некорректный id цвета."));
        }

        var entity = await dbContext.Colors.FirstOrDefaultAsync(item => item.Id == colorId);
        if (entity is null)
        {
            return NotFound(new ApiError("Цвет не найден."));
        }

        entity.IsActive = false;
        await dbContext.SaveChangesAsync();

        return Ok(entity.ToRead());
    }

    private static (bool IsValid, string? Name, string? RalCode, string? RgbHex, bool IsActive, string? Error) ValidateCreatePayload(ColorCreateRequest payload)
    {
        var name = payload.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return (false, null, null, null, false, "Название цвета обязательно.");
        }

        var ralCode = payload.RalCode.Trim();
        if (string.IsNullOrWhiteSpace(ralCode))
        {
            return (false, null, null, null, false, "RAL обязателен.");
        }

        string rgbHex;
        try
        {
            rgbHex = ColorMath.NormalizeHex(payload.RgbHex);
        }
        catch (ArgumentException ex)
        {
            return (false, null, null, null, false, ex.Message);
        }

        var isActive = payload.IsActive ?? true;
        return (true, name, ralCode, rgbHex, isActive, null);
    }
}
