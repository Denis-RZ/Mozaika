using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Mozaika.Api.Contracts;
using Mozaika.Api.Database;
using Mozaika.Api.Options;
using Mozaika.Api.Services;

namespace Mozaika.Api.Controllers;

[ApiController]
[Route("api/mosaic")]
public sealed class MosaicController(
    MozaikaDbContext dbContext,
    MosaicService mosaicService,
    IOptions<MozaikaOptions> appOptions
) : ControllerBase
{
    [HttpPost("generate")]
    public async Task<ActionResult<MosaicGenerateResponse>> Generate(
        [FromForm(Name = "image")] IFormFile? image,
        [FromForm(Name = "field_width_mm")] double? fieldWidthMm,
        [FromForm(Name = "field_height_mm")] double? fieldHeightMm,
        [FromForm(Name = "cell_size_mm")] double? cellSizeMm,
        [FromForm(Name = "gap_mm")] double? gapMm,
        [FromForm(Name = "grout_color_id")] int? groutColorId,
        [FromForm(Name = "grout_color_hex")] string? groutColorHex,
        [FromForm(Name = "max_colors")] int? maxColors,
        [FromForm(Name = "include_color_ids")] string? includeColorIdsRaw,
        [FromForm(Name = "exclude_color_ids")] string? excludeColorIdsRaw,
        [FromForm(Name = "offset_x_mm")] double offsetXMm = 0,
        [FromForm(Name = "offset_y_mm")] double offsetYMm = 0
    )
    {
        if (image is null)
        {
            return BadRequest(new ApiError("Перед генерацией загрузите изображение."));
        }

        if (!string.IsNullOrWhiteSpace(image.ContentType) &&
            !image.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new ApiError("Загруженный файл должен быть изображением."));
        }

        await using var stream = new MemoryStream();
        await image.CopyToAsync(stream);
        var sourceBytes = stream.ToArray();

        if (sourceBytes.Length == 0)
        {
            return BadRequest(new ApiError("Загруженное изображение пустое."));
        }

        var maxUploadBytes = Math.Max(1, appOptions.Value.MaxUploadMb) * 1024L * 1024L;
        if (sourceBytes.Length > maxUploadBytes)
        {
            return StatusCode(
                StatusCodes.Status413PayloadTooLarge,
                new ApiError($"Размер изображения превышает лимит {appOptions.Value.MaxUploadMb} МБ.")
            );
        }

        HashSet<int> includeColorIds;
        HashSet<int> excludeColorIds;

        try
        {
            includeColorIds = ParseIds(includeColorIdsRaw);
            excludeColorIds = ParseIds(excludeColorIdsRaw);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiError(ex.Message));
        }

        var settings = await DbHelpers.GetOrCreateSettingsAsync(dbContext);
        var resolvedFieldWidth = fieldWidthMm ?? settings.DefaultFieldWidthMm;
        var resolvedFieldHeight = fieldHeightMm ?? settings.DefaultFieldHeightMm;
        var resolvedCellSize = cellSizeMm ?? settings.DefaultCellSizeMm;
        var resolvedGap = gapMm ?? settings.DefaultGapMm;

        var groutHexResult = await ResolveGroutColorHex(groutColorId, groutColorHex);
        if (!groutHexResult.IsSuccess)
        {
            return StatusCode(groutHexResult.StatusCode, new ApiError(groutHexResult.Error!));
        }

        var availableColors = await dbContext.Colors
            .AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.Id)
            .ToListAsync();

        if (availableColors.Count == 0)
        {
            return BadRequest(new ApiError("Палитра пуста. Добавьте хотя бы один активный цвет."));
        }

        try
        {
            var response = mosaicService.Generate(
                imageBytes: sourceBytes,
                availableColors: availableColors,
                fieldWidthMm: resolvedFieldWidth,
                fieldHeightMm: resolvedFieldHeight,
                cellSizeMm: resolvedCellSize,
                gapMm: resolvedGap,
                groutColorHex: groutHexResult.Hex!,
                maxColors: maxColors,
                includeColorIds: includeColorIds,
                excludeColorIds: excludeColorIds,
                offsetXMm: offsetXMm,
                offsetYMm: offsetYMm
            );

            return Ok(response);
        }
        catch (MosaicGenerationException ex)
        {
            return BadRequest(new ApiError(ex.Message));
        }
    }

    private static HashSet<int> ParseIds(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        try
        {
            return raw
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(int.Parse)
                .ToHashSet();
        }
        catch (Exception)
        {
            throw new ArgumentException("Поля include_color_ids/exclude_color_ids должны содержать целые id через запятую.");
        }
    }

    private async Task<(bool IsSuccess, string? Hex, int StatusCode, string? Error)> ResolveGroutColorHex(
        int? groutColorId,
        string? groutColorHex
    )
    {
        if (groutColorId is not null)
        {
            var groutColor = await dbContext.GroutColors
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == groutColorId.Value);

            if (groutColor is null || !groutColor.IsActive)
            {
                return (false, null, StatusCodes.Status400BadRequest, "Выбранный цвет заполнения недоступен.");
            }

            return (true, ColorMath.NormalizeHex(groutColor.RgbHex), StatusCodes.Status200OK, null);
        }

        if (!string.IsNullOrWhiteSpace(groutColorHex))
        {
            try
            {
                return (true, ColorMath.NormalizeHex(groutColorHex), StatusCodes.Status200OK, null);
            }
            catch (ArgumentException ex)
            {
                return (false, null, StatusCodes.Status422UnprocessableEntity, ex.Message);
            }
        }

        var firstActiveGrout = await dbContext.GroutColors
            .AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.Id)
            .FirstOrDefaultAsync();

        var fallbackHex = firstActiveGrout?.RgbHex ?? "#FFFFFF";
        return (true, ColorMath.NormalizeHex(fallbackHex), StatusCodes.Status200OK, null);
    }
}
