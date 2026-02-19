using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Mozaika.Api.Contracts;
using Mozaika.Api.Database;
using Mozaika.Api.Database.Entities;
using Mozaika.Api.Services;

namespace Mozaika.Api.Controllers;

[ApiController]
[Route("api/admin")]
public sealed class AdminController(MozaikaDbContext dbContext) : ControllerBase
{
    [HttpGet("settings")]
    public async Task<ActionResult<AdminSettingsReadResponse>> GetSettings()
    {
        var settings = await DbHelpers.GetOrCreateSettingsAsync(dbContext);
        return Ok(settings.ToRead());
    }

    [HttpPut("settings")]
    public async Task<ActionResult<AdminSettingsReadResponse>> UpdateSettings([FromBody] AdminSettingsUpdateRequest payload)
    {
        if (!payload.HasAnyValue())
        {
            return BadRequest(new ApiError("Нужно передать хотя бы одно поле для обновления."));
        }

        if (payload.DefaultFieldWidthMm is not null && payload.DefaultFieldWidthMm <= 0)
        {
            return BadRequest(new ApiError("Ширина поля должна быть больше нуля."));
        }

        if (payload.DefaultFieldHeightMm is not null && payload.DefaultFieldHeightMm <= 0)
        {
            return BadRequest(new ApiError("Высота поля должна быть больше нуля."));
        }

        if (payload.DefaultCellSizeMm is not null && payload.DefaultCellSizeMm <= 0)
        {
            return BadRequest(new ApiError("Размер ячейки должен быть больше нуля."));
        }

        if (payload.DefaultGapMm is not null && payload.DefaultGapMm < 0)
        {
            return BadRequest(new ApiError("Расстояние между ячейками не может быть отрицательным."));
        }

        var settings = await DbHelpers.GetOrCreateSettingsAsync(dbContext);

        if (payload.DefaultFieldWidthMm is not null)
        {
            settings.DefaultFieldWidthMm = payload.DefaultFieldWidthMm.Value;
        }

        if (payload.DefaultFieldHeightMm is not null)
        {
            settings.DefaultFieldHeightMm = payload.DefaultFieldHeightMm.Value;
        }

        if (payload.DefaultCellSizeMm is not null)
        {
            settings.DefaultCellSizeMm = payload.DefaultCellSizeMm.Value;
        }

        if (payload.DefaultGapMm is not null)
        {
            settings.DefaultGapMm = payload.DefaultGapMm.Value;
        }

        settings.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        return Ok(settings.ToRead());
    }

    [HttpGet("grout-colors")]
    public async Task<ActionResult<List<GroutColorReadResponse>>> ListGroutColors(
        [FromQuery(Name = "include_inactive")] bool includeInactive = false
    )
    {
        var query = dbContext.GroutColors
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

    [HttpPost("grout-colors")]
    public async Task<ActionResult<GroutColorReadResponse>> CreateGroutColor([FromBody] GroutColorCreateRequest payload)
    {
        var parsed = ValidateCreatePayload(payload);
        if (!parsed.IsValid)
        {
            return BadRequest(new ApiError(parsed.Error!));
        }

        var duplicateExists = await dbContext.GroutColors.AnyAsync(item =>
            item.Name == parsed.Name ||
            item.RgbHex == parsed.RgbHex
        );

        if (duplicateExists)
        {
            return Conflict(new ApiError("Цвет заполнения с таким названием или HEX уже существует."));
        }

        var entity = new GroutColorEntity
        {
            Name = parsed.Name!,
            RgbHex = parsed.RgbHex!,
            IsActive = parsed.IsActive,
            CreatedAt = DateTime.UtcNow,
        };

        dbContext.GroutColors.Add(entity);
        await dbContext.SaveChangesAsync();

        return StatusCode(StatusCodes.Status201Created, entity.ToRead());
    }

    [HttpPatch("grout-colors/{groutColorId:int}")]
    public async Task<ActionResult<GroutColorReadResponse>> UpdateGroutColor(
        int groutColorId,
        [FromBody] GroutColorUpdateRequest payload
    )
    {
        if (groutColorId <= 0)
        {
            return BadRequest(new ApiError("Некорректный id цвета заполнения."));
        }

        if (!payload.HasAnyValue())
        {
            return BadRequest(new ApiError("Нужно передать хотя бы одно поле для обновления."));
        }

        var entity = await dbContext.GroutColors.FirstOrDefaultAsync(item => item.Id == groutColorId);
        if (entity is null)
        {
            return NotFound(new ApiError("Цвет заполнения не найден."));
        }

        var nextName = entity.Name;
        var nextHex = entity.RgbHex;
        var nextIsActive = entity.IsActive;

        if (payload.Name is not null)
        {
            nextName = payload.Name.Trim();
            if (string.IsNullOrWhiteSpace(nextName))
            {
                return BadRequest(new ApiError("Название цвета заполнения обязательно."));
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

        var duplicateExists = await dbContext.GroutColors.AnyAsync(item =>
            item.Id != entity.Id &&
            (item.Name == nextName || item.RgbHex == nextHex)
        );

        if (duplicateExists)
        {
            return Conflict(new ApiError("Цвет заполнения с таким названием или HEX уже существует."));
        }

        entity.Name = nextName;
        entity.RgbHex = nextHex;
        entity.IsActive = nextIsActive;

        await dbContext.SaveChangesAsync();
        return Ok(entity.ToRead());
    }

    [HttpDelete("grout-colors/{groutColorId:int}")]
    public async Task<ActionResult<GroutColorReadResponse>> DeactivateGroutColor(int groutColorId)
    {
        if (groutColorId <= 0)
        {
            return BadRequest(new ApiError("Некорректный id цвета заполнения."));
        }

        var entity = await dbContext.GroutColors.FirstOrDefaultAsync(item => item.Id == groutColorId);
        if (entity is null)
        {
            return NotFound(new ApiError("Цвет заполнения не найден."));
        }

        entity.IsActive = false;
        await dbContext.SaveChangesAsync();

        return Ok(entity.ToRead());
    }

    private static (bool IsValid, string? Name, string? RgbHex, bool IsActive, string? Error) ValidateCreatePayload(GroutColorCreateRequest payload)
    {
        var name = payload.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return (false, null, null, false, "Название цвета заполнения обязательно.");
        }

        string rgbHex;
        try
        {
            rgbHex = ColorMath.NormalizeHex(payload.RgbHex);
        }
        catch (ArgumentException ex)
        {
            return (false, null, null, false, ex.Message);
        }

        return (true, name, rgbHex, payload.IsActive ?? true, null);
    }
}
