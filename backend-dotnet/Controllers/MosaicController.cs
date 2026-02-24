using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Mozaika.Api.Contracts;
using Mozaika.Api.Database;
using Mozaika.Api.Options;
using Mozaika.Api.Security;
using Mozaika.Api.Services;

namespace Mozaika.Api.Controllers;

[ApiController]
[Route("api/mosaic")]
public sealed class MosaicController(
    MozaikaDbContext dbContext,
    MosaicService mosaicService,
    MosaicExportService mosaicExportService,
    IOptions<MozaikaOptions> appOptions
) : ApiControllerBase
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
        var authError = RequireAnyRole(AppRoles.Admin, AppRoles.Customer);
        if (authError is not null)
        {
            return authError;
        }

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

    [HttpPost("replace-color")]
    public async Task<ActionResult<MosaicGenerateResponse>> ReplaceColor([FromBody] MosaicReplaceColorRequest payload)
    {
        var authError = RequireAnyRole(AppRoles.Admin, AppRoles.Customer);
        if (authError is not null)
        {
            return authError;
        }

        if (payload.FromColorId == payload.ToColorId)
        {
            return BadRequest(new ApiError("Исходный и целевой цвет совпадают. Выберите разные цвета."));
        }

        string normalizedGroutHex;
        try
        {
            normalizedGroutHex = ColorMath.NormalizeHex(payload.GroutColorHex);
        }
        catch (ArgumentException ex)
        {
            return UnprocessableEntity(new ApiError(ex.Message));
        }

        var colors = await dbContext.Colors
            .AsNoTracking()
            .OrderBy(item => item.Id)
            .ToListAsync();

        if (colors.Count == 0)
        {
            return BadRequest(new ApiError("Палитра пуста. Добавьте хотя бы один цвет."));
        }

        try
        {
            var response = mosaicService.ReplaceColor(
                gridColorIds: payload.GridColorIds,
                availableColors: colors,
                fromColorId: payload.FromColorId,
                toColorId: payload.ToColorId,
                fieldWidthMm: payload.FieldWidthMm,
                fieldHeightMm: payload.FieldHeightMm,
                cellSizeMm: payload.CellSizeMm,
                gapMm: payload.GapMm,
                offsetXMm: payload.OffsetXMm,
                offsetYMm: payload.OffsetYMm,
                groutColorHex: normalizedGroutHex,
                requestedMaxColors: payload.RequestedMaxColors
            );

            return Ok(response);
        }
        catch (MosaicGenerationException ex)
        {
            return BadRequest(new ApiError(ex.Message));
        }
    }

    [HttpPost("export/png")]
    public Task<IActionResult> ExportPng([FromBody] MosaicExportRequest payload) =>
        Export(payload, MosaicExportFormat.Png);

    [HttpPost("export/jpeg")]
    public Task<IActionResult> ExportJpeg([FromBody] MosaicExportRequest payload) =>
        Export(payload, MosaicExportFormat.Jpeg);

    [HttpPost("export/svg")]
    public Task<IActionResult> ExportSvg([FromBody] MosaicExportRequest payload) =>
        Export(payload, MosaicExportFormat.Svg);

    [HttpPost("export/pdf")]
    public Task<IActionResult> ExportPdf([FromBody] MosaicExportRequest payload) =>
        Export(payload, MosaicExportFormat.Pdf);

    [HttpPost("export/materials-csv")]
    public Task<IActionResult> ExportMaterialsCsv([FromBody] MosaicExportRequest payload) =>
        Export(payload, MosaicExportFormat.MaterialsCsv);

    [HttpPost("export/grid-csv")]
    public Task<IActionResult> ExportGridCsv([FromBody] MosaicExportRequest payload) =>
        Export(payload, MosaicExportFormat.GridCsv);

    [HttpPost("export/modules-csv")]
    public Task<IActionResult> ExportModulesCsv([FromBody] MosaicExportRequest payload) =>
        Export(payload, MosaicExportFormat.ModulesCsv);

    [HttpPost("export/assembly-kit-pdf")]
    public Task<IActionResult> ExportAssemblyKitPdf([FromBody] MosaicExportRequest payload) =>
        Export(payload, MosaicExportFormat.AssemblyKitPdf);

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

    private async Task<IActionResult> Export(MosaicExportRequest payload, MosaicExportFormat format)
    {
        var authError = RequireAnyRole(AppRoles.Admin, AppRoles.Customer);
        if (authError is not null)
        {
            return authError;
        }

        var colors = await dbContext.Colors
            .AsNoTracking()
            .OrderBy(item => item.Id)
            .ToListAsync();

        if (colors.Count == 0)
        {
            return BadRequest(new ApiError("Палитра пуста. Добавьте хотя бы один цвет."));
        }

        try
        {
            var file = mosaicExportService.Export(format, payload, colors);
            return File(file.Content, file.ContentType, file.FileName);
        }
        catch (MosaicGenerationException ex)
        {
            return BadRequest(new ApiError(ex.Message));
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
