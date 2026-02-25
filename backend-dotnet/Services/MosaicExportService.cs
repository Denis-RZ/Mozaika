using System.Globalization;
using System.Text;
using Mozaika.Api.Contracts;
using Mozaika.Api.Database.Entities;
using PdfSharpCore;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Mozaika.Api.Services;

public enum MosaicExportFormat
{
    Png,
    Jpeg,
    Svg,
    Pdf,
    MaterialsCsv,
    GridCsv,
    AssemblyKitPdf,
    ModulesCsv,
}

public sealed class MosaicExportResult
{
    public required byte[] Content { get; init; }
    public required string ContentType { get; init; }
    public required string FileName { get; init; }
}

public sealed class MosaicExportService
{
    private sealed record PaletteColor(
        int Id,
        string Name,
        string RalCode,
        string RgbHex,
        byte R,
        byte G,
        byte B
    );

    private sealed class ValidatedExport
    {
        public required int[][] GridColorIds { get; init; }
        public required int Rows { get; init; }
        public required int Columns { get; init; }
        public required Dictionary<int, int> CountsByColorId { get; init; }
        public required Dictionary<int, PaletteColor> PaletteById { get; init; }
        public required double FieldWidthMm { get; init; }
        public required double FieldHeightMm { get; init; }
        public required double CellSizeMm { get; init; }
        public required double GapMm { get; init; }
        public required double OffsetXMm { get; init; }
        public required double OffsetYMm { get; init; }
        public required string GroutColorHex { get; init; }
        public required int Dpi { get; init; }
        public required bool MirrorHorizontal { get; init; }
        public required bool IncludeLegend { get; init; }
        public required int ModuleChipColumns { get; init; }
        public required int ModuleChipRows { get; init; }
        public required int ModuleStartNumber { get; init; }
        public required bool IncludeColorNumbers { get; init; }
        public required Dictionary<int, int> ColorNumberByColorId { get; init; }
        public required string NormalizedFileName { get; init; }
    }

    public MosaicExportResult Export(
        MosaicExportFormat format,
        MosaicExportRequest request,
        IReadOnlyList<ColorEntity> availableColors
    )
    {
        var validated = ValidateAndPrepare(request, availableColors);

        return format switch
        {
            MosaicExportFormat.Png => new MosaicExportResult
            {
                Content = RenderPng(validated),
                ContentType = "image/png",
                FileName = $"{validated.NormalizedFileName}.png",
            },
            MosaicExportFormat.Jpeg => new MosaicExportResult
            {
                Content = RenderJpeg(validated),
                ContentType = "image/jpeg",
                FileName = $"{validated.NormalizedFileName}.jpg",
            },
            MosaicExportFormat.Svg => new MosaicExportResult
            {
                Content = RenderSvg(validated),
                ContentType = "image/svg+xml",
                FileName = $"{validated.NormalizedFileName}.svg",
            },
            MosaicExportFormat.Pdf => new MosaicExportResult
            {
                Content = RenderPdf(validated),
                ContentType = "application/pdf",
                FileName = $"{validated.NormalizedFileName}.pdf",
            },
            MosaicExportFormat.MaterialsCsv => new MosaicExportResult
            {
                Content = RenderMaterialsCsv(validated),
                ContentType = "text/csv; charset=utf-8",
                FileName = $"{validated.NormalizedFileName}-materials.csv",
            },
            MosaicExportFormat.GridCsv => new MosaicExportResult
            {
                Content = RenderGridCsv(validated),
                ContentType = "text/csv; charset=utf-8",
                FileName = $"{validated.NormalizedFileName}-grid.csv",
            },
            MosaicExportFormat.AssemblyKitPdf => new MosaicExportResult
            {
                Content = RenderAssemblyKitPdf(validated),
                ContentType = "application/pdf",
                FileName = $"{validated.NormalizedFileName}-assembly-kit.pdf",
            },
            MosaicExportFormat.ModulesCsv => new MosaicExportResult
            {
                Content = RenderModulesCsv(validated),
                ContentType = "text/csv; charset=utf-8",
                FileName = $"{validated.NormalizedFileName}-modules.csv",
            },
            _ => throw new MosaicGenerationException("Неподдерживаемый формат экспорта."),
        };
    }

    private static ValidatedExport ValidateAndPrepare(
        MosaicExportRequest request,
        IReadOnlyList<ColorEntity> availableColors
    )
    {
        if (request.FieldWidthMm <= 0 || request.FieldHeightMm <= 0)
        {
            throw new MosaicGenerationException("Ширина и высота поля должны быть больше нуля.");
        }

        if (request.CellSizeMm <= 0)
        {
            throw new MosaicGenerationException("Размер ячейки должен быть больше нуля.");
        }

        if (request.GapMm < 0)
        {
            throw new MosaicGenerationException("Расстояние между ячейками не может быть отрицательным.");
        }

        if (request.Dpi < 72 || request.Dpi > 600)
        {
            throw new MosaicGenerationException("Параметр dpi должен быть в диапазоне 72..600.");
        }

        if (request.ModuleChipColumns <= 0 || request.ModuleChipRows <= 0)
        {
            throw new MosaicGenerationException("Размер монтажной плитки должен быть больше нуля.");
        }

        if (request.ModuleChipColumns > 256 || request.ModuleChipRows > 256)
        {
            throw new MosaicGenerationException("Размер монтажной плитки слишком большой (максимум 256x256 чипов).");
        }

        if (request.ModuleStartNumber <= 0)
        {
            throw new MosaicGenerationException("Нумерация монтажных плиток должна начинаться с положительного числа.");
        }

        if (request.GridColorIds.Length == 0 || request.GridColorIds[0].Length == 0)
        {
            throw new MosaicGenerationException("Сетка мозаики не может быть пустой.");
        }

        var rows = request.GridColorIds.Length;
        var columns = request.GridColorIds[0].Length;

        for (var row = 0; row < rows; row++)
        {
            if (request.GridColorIds[row].Length != columns)
            {
                throw new MosaicGenerationException("Сетка мозаики должна быть прямоугольной.");
            }
        }

        var normalizedGroutHex = ColorMath.NormalizeHex(request.GroutColorHex);
        var normalizedFileName = NormalizeFileName(request.FileName);

        if (availableColors.Count == 0)
        {
            throw new MosaicGenerationException("Палитра пуста. Добавьте хотя бы один цвет.");
        }

        var paletteById = availableColors.ToDictionary(
            item => item.Id,
            item =>
            {
                var hex = ColorMath.NormalizeHex(item.RgbHex);
                var rgb = ColorMath.HexToRgb(hex);
                return new PaletteColor(
                    item.Id,
                    item.Name,
                    item.RalCode,
                    hex,
                    rgb.R,
                    rgb.G,
                    rgb.B
                );
            }
        );

        var countsByColorId = new Dictionary<int, int>();
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var colorId = request.GridColorIds[row][column];
                if (colorId <= 0)
                {
                    throw new MosaicGenerationException("Сетка содержит некорректные id цветов.");
                }

                if (!paletteById.ContainsKey(colorId))
                {
                    throw new MosaicGenerationException($"В сетке найден неизвестный id цвета: {colorId}.");
                }

                countsByColorId[colorId] = countsByColorId.TryGetValue(colorId, out var count) ? count + 1 : 1;
            }
        }

        var colorNumberByColorId = countsByColorId
            .OrderByDescending(item => item.Value)
            .ThenBy(item => item.Key)
            .Select((item, index) => (item.Key, Number: index + 1))
            .ToDictionary(item => item.Key, item => item.Number);

        return new ValidatedExport
        {
            GridColorIds = request.GridColorIds,
            Rows = rows,
            Columns = columns,
            CountsByColorId = countsByColorId,
            PaletteById = paletteById,
            FieldWidthMm = request.FieldWidthMm,
            FieldHeightMm = request.FieldHeightMm,
            CellSizeMm = request.CellSizeMm,
            GapMm = request.GapMm,
            OffsetXMm = request.OffsetXMm,
            OffsetYMm = request.OffsetYMm,
            GroutColorHex = normalizedGroutHex,
            Dpi = request.Dpi,
            MirrorHorizontal = request.MirrorHorizontal,
            IncludeLegend = request.IncludeLegend,
            ModuleChipColumns = request.ModuleChipColumns,
            ModuleChipRows = request.ModuleChipRows,
            ModuleStartNumber = request.ModuleStartNumber,
            IncludeColorNumbers = request.IncludeColorNumbers,
            ColorNumberByColorId = colorNumberByColorId,
            NormalizedFileName = normalizedFileName,
        };
    }

    private sealed class ModuleBlock
    {
        public required int Number { get; init; }
        public required int ModuleRowFromBottom { get; init; }
        public required int ModuleColumnFromLeft { get; init; }
        public required int RowStart { get; init; }
        public required int RowEndExclusive { get; init; }
        public required int ColumnStart { get; init; }
        public required int ColumnEndExclusive { get; init; }
    }

    private static List<ModuleBlock> BuildModules(ValidatedExport data)
    {
        var moduleRows = (int)Math.Ceiling(data.Rows / (double)data.ModuleChipRows);
        var moduleColumns = (int)Math.Ceiling(data.Columns / (double)data.ModuleChipColumns);
        var modules = new List<ModuleBlock>(moduleRows * moduleColumns);
        var nextNumber = data.ModuleStartNumber;

        for (var moduleRowFromBottom = 0; moduleRowFromBottom < moduleRows; moduleRowFromBottom++)
        {
            var rowStart = Math.Max(0, data.Rows - ((moduleRowFromBottom + 1) * data.ModuleChipRows));
            var rowEndExclusive = data.Rows - (moduleRowFromBottom * data.ModuleChipRows);
            rowEndExclusive = Math.Min(data.Rows, rowEndExclusive);

            for (var moduleColumnFromLeft = 0; moduleColumnFromLeft < moduleColumns; moduleColumnFromLeft++)
            {
                var columnStart = moduleColumnFromLeft * data.ModuleChipColumns;
                var columnEndExclusive = Math.Min(data.Columns, columnStart + data.ModuleChipColumns);
                modules.Add(new ModuleBlock
                {
                    Number = nextNumber++,
                    ModuleRowFromBottom = moduleRowFromBottom + 1,
                    ModuleColumnFromLeft = moduleColumnFromLeft + 1,
                    RowStart = rowStart,
                    RowEndExclusive = rowEndExclusive,
                    ColumnStart = columnStart,
                    ColumnEndExclusive = columnEndExclusive,
                });
            }
        }

        return modules;
    }

    private static string NormalizeFileName(string fileName)
    {
        var source = string.IsNullOrWhiteSpace(fileName) ? "mozaika" : fileName.Trim();
        var invalidChars = Path.GetInvalidFileNameChars().ToHashSet();
        var builder = new StringBuilder(source.Length);
        foreach (var character in source)
        {
            if (invalidChars.Contains(character))
            {
                continue;
            }

            builder.Append(character);
        }

        var normalized = builder.ToString().Trim();
        return string.IsNullOrWhiteSpace(normalized) ? "mozaika" : normalized;
    }

    private static byte[] RenderPng(ValidatedExport data)
    {
        using var image = RenderRasterImage(data);
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }

    private static byte[] RenderJpeg(ValidatedExport data)
    {
        using var image = RenderRasterImage(data);
        using var stream = new MemoryStream();
        image.SaveAsJpeg(stream, new JpegEncoder
        {
            Quality = 92,
        });
        return stream.ToArray();
    }

    private static Image<Rgb24> RenderRasterImage(ValidatedExport data)
    {
        var pxPerMm = data.Dpi / 25.4;
        var fieldWidthPx = Math.Max(1, (int)Math.Round(data.FieldWidthMm * pxPerMm));
        var fieldHeightPx = Math.Max(1, (int)Math.Round(data.FieldHeightMm * pxPerMm));
        var cellPx = Math.Max(1, (int)Math.Round(data.CellSizeMm * pxPerMm));
        var gapPx = Math.Max(0, (int)Math.Round(data.GapMm * pxPerMm));
        var stepPx = cellPx + gapPx;
        var originXPx = (int)Math.Round(data.OffsetXMm * pxPerMm);
        var originYPx = (int)Math.Round(data.OffsetYMm * pxPerMm);

        var groutRgb = ColorMath.HexToRgb(data.GroutColorHex);
        var image = new Image<Rgb24>(fieldWidthPx, fieldHeightPx, new Rgb24(groutRgb.R, groutRgb.G, groutRgb.B));

        for (var row = 0; row < data.Rows; row++)
        {
            var y0 = originYPx + (row * stepPx);
            var y1 = Math.Min(y0 + cellPx, fieldHeightPx);

            if (y0 >= fieldHeightPx)
            {
                break;
            }

            if (y1 <= 0)
            {
                continue;
            }

            for (var column = 0; column < data.Columns; column++)
            {
                var x0 = originXPx + (column * stepPx);
                var x1 = Math.Min(x0 + cellPx, fieldWidthPx);

                if (x0 >= fieldWidthPx)
                {
                    break;
                }

                if (x1 <= 0)
                {
                    continue;
                }

                var colorId = data.GridColorIds[row][column];
                var palette = data.PaletteById[colorId];
                var fill = new Rgb24(palette.R, palette.G, palette.B);
                var clipY0 = Math.Max(0, y0);
                var clipX0 = Math.Max(0, x0);

                for (var y = clipY0; y < y1; y++)
                {
                    for (var x = clipX0; x < x1; x++)
                    {
                        image[x, y] = fill;
                    }
                }
            }
        }

        if (data.MirrorHorizontal)
        {
            image.Mutate(ctx => ctx.Flip(FlipMode.Horizontal));
        }

        return image;
    }

    private static byte[] RenderSvg(ValidatedExport data)
    {
        static string F(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

        var builder = new StringBuilder(128 * 1024);
        builder.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        builder.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" version=\"1.1\" width=\"")
            .Append(F(data.FieldWidthMm))
            .Append("mm\" height=\"")
            .Append(F(data.FieldHeightMm))
            .Append("mm\" viewBox=\"0 0 ")
            .Append(F(data.FieldWidthMm))
            .Append(' ')
            .Append(F(data.FieldHeightMm))
            .AppendLine("\">");
        builder.Append("  <rect x=\"0\" y=\"0\" width=\"")
            .Append(F(data.FieldWidthMm))
            .Append("\" height=\"")
            .Append(F(data.FieldHeightMm))
            .Append("\" fill=\"")
            .Append(data.GroutColorHex)
            .AppendLine("\" />");
        builder.Append("  <defs><clipPath id=\"field-clip\"><rect x=\"0\" y=\"0\" width=\"")
            .Append(F(data.FieldWidthMm))
            .Append("\" height=\"")
            .Append(F(data.FieldHeightMm))
            .AppendLine("\" /></clipPath></defs>");
        builder.Append("  <g clip-path=\"url(#field-clip)\"");
        if (data.MirrorHorizontal)
        {
            builder.Append(" transform=\"translate(")
                .Append(F(data.FieldWidthMm))
                .Append(" 0) scale(-1 1)\"");
        }

        builder.AppendLine(">");

        var pitch = data.CellSizeMm + data.GapMm;
        for (var row = 0; row < data.Rows; row++)
        {
            var y = data.OffsetYMm + (row * pitch);
            for (var column = 0; column < data.Columns; column++)
            {
                var x = data.OffsetXMm + (column * pitch);
                var color = data.PaletteById[data.GridColorIds[row][column]];
                builder.Append("    <rect x=\"")
                    .Append(F(x))
                    .Append("\" y=\"")
                    .Append(F(y))
                    .Append("\" width=\"")
                    .Append(F(data.CellSizeMm))
                    .Append("\" height=\"")
                    .Append(F(data.CellSizeMm))
                    .Append("\" fill=\"")
                    .Append(color.RgbHex)
                    .AppendLine("\" />");
            }
        }

        builder.AppendLine("  </g>");
        builder.AppendLine("</svg>");

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private static byte[] RenderPdf(ValidatedExport data)
    {
        using var document = new PdfDocument();

        var mosaicPage = document.AddPage();
        mosaicPage.Size = PageSize.A4;
        mosaicPage.Orientation = data.FieldWidthMm >= data.FieldHeightMm
            ? PageOrientation.Landscape
            : PageOrientation.Portrait;

        using (var gfx = XGraphics.FromPdfPage(mosaicPage))
        {
            var titleFont = new XFont("Arial", 14, XFontStyle.Bold);
            var metaFont = new XFont("Arial", 9, XFontStyle.Regular);
            var margin = MmToPoint(10);
            var contentTop = margin + MmToPoint(10);

            gfx.DrawString(
                "Экспорт мозаики",
                titleFont,
                XBrushes.Black,
                new XRect(margin, margin, mosaicPage.Width - (2 * margin), MmToPoint(8)),
                XStringFormats.TopLeft
            );
            gfx.DrawString(
                $"Поле: {data.FieldWidthMm:0.#} x {data.FieldHeightMm:0.#} мм | Чип: {data.CellSizeMm:0.##} мм | Шов: {data.GapMm:0.##} мм",
                metaFont,
                XBrushes.Black,
                new XRect(margin, margin + MmToPoint(5), mosaicPage.Width - (2 * margin), MmToPoint(8)),
                XStringFormats.TopLeft
            );

            var availableWidth = mosaicPage.Width - (2 * margin);
            var availableHeight = mosaicPage.Height - contentTop - margin;
            var widthScale = availableWidth / data.FieldWidthMm;
            var heightScale = availableHeight / data.FieldHeightMm;
            var scale = Math.Min(widthScale, heightScale);

            var drawWidth = data.FieldWidthMm * scale;
            var drawHeight = data.FieldHeightMm * scale;
            var drawX = margin + ((availableWidth - drawWidth) / 2);
            var drawY = contentTop + ((availableHeight - drawHeight) / 2);
            DrawMosaicPreview(gfx, data, drawX, drawY, drawWidth, drawHeight);
        }

        if (data.IncludeLegend)
        {
            AppendLegendPages(document, data);
        }

        using var output = new MemoryStream();
        document.Save(output, closeStream: false);
        return output.ToArray();
    }

    private static void DrawMosaicPreview(
        XGraphics gfx,
        ValidatedExport data,
        double drawX,
        double drawY,
        double drawWidth,
        double drawHeight
    )
    {
        var grout = ColorMath.HexToRgb(data.GroutColorHex);
        gfx.DrawRectangle(
            new XSolidBrush(XColor.FromArgb(grout.R, grout.G, grout.B)),
            drawX,
            drawY,
            drawWidth,
            drawHeight
        );

        var scaleX = drawWidth / data.FieldWidthMm;
        var scaleY = drawHeight / data.FieldHeightMm;
        var pitchMm = data.CellSizeMm + data.GapMm;
        var clipState = gfx.Save();
        gfx.IntersectClip(new XRect(drawX, drawY, drawWidth, drawHeight));

        for (var row = 0; row < data.Rows; row++)
        {
            var yMm = data.OffsetYMm + (row * pitchMm);
            for (var column = 0; column < data.Columns; column++)
            {
                var xMm = data.OffsetXMm + (column * pitchMm);
                if (data.MirrorHorizontal)
                {
                    xMm = data.FieldWidthMm - xMm - data.CellSizeMm;
                }

                var xEndMm = xMm + data.CellSizeMm;
                var yEndMm = yMm + data.CellSizeMm;
                if (xEndMm <= 0 || yEndMm <= 0 || xMm >= data.FieldWidthMm || yMm >= data.FieldHeightMm)
                {
                    continue;
                }

                var color = data.PaletteById[data.GridColorIds[row][column]];
                var brush = new XSolidBrush(XColor.FromArgb(color.R, color.G, color.B));
                var rectX = drawX + (xMm * scaleX);
                var rectY = drawY + (yMm * scaleY);
                var rectWidth = data.CellSizeMm * scaleX;
                var rectHeight = data.CellSizeMm * scaleY;
                gfx.DrawRectangle(brush, rectX, rectY, rectWidth, rectHeight);
            }
        }

        gfx.Restore(clipState);
        gfx.DrawRectangle(XPens.Black, drawX, drawY, drawWidth, drawHeight);
    }

    private static void AppendLegendPages(PdfDocument document, ValidatedExport data)
    {
        var totalCells = data.Rows * data.Columns;
        var sorted = data.CountsByColorId
            .OrderByDescending(item => item.Value)
            .ThenBy(item => item.Key)
            .ToList();

        const double lineHeightMm = 6.0;
        var margin = MmToPoint(12);
        var lineHeight = MmToPoint(lineHeightMm);
        var titleHeight = MmToPoint(10);

        PdfPage? page = null;
        XGraphics? gfx = null;
        var headingFont = new XFont("Arial", 13, XFontStyle.Bold);
        var rowFont = new XFont("Arial", 9, XFontStyle.Regular);
        double y = 0;

        void EnsurePage()
        {
            if (page is not null && gfx is not null)
            {
                return;
            }

            page = document.AddPage();
            page.Size = PageSize.A4;
            page.Orientation = PageOrientation.Portrait;
            gfx = XGraphics.FromPdfPage(page);
            y = margin;
            gfx.DrawString(
                "Легенда / Ведомость материалов",
                headingFont,
                XBrushes.Black,
                new XRect(margin, y, page.Width - (2 * margin), titleHeight),
                XStringFormats.TopLeft
            );
            y += titleHeight;
        }

        foreach (var item in sorted)
        {
            EnsurePage();
            if (page is null || gfx is null)
            {
                continue;
            }

            if ((y + lineHeight) > (page.Height - margin))
            {
                gfx.Dispose();
                page = null;
                gfx = null;
                EnsurePage();
                if (page is null || gfx is null)
                {
                    continue;
                }
            }

            var color = data.PaletteById[item.Key];
            var ratio = (double)item.Value / totalCells;
            var rowText = $"{color.Name} | {color.RalCode} | {color.RgbHex} | чипов: {item.Value} | {ratio:P1}";

            var swatchSize = MmToPoint(4.5);
            var swatchY = y + (lineHeight - swatchSize) / 2;
            gfx.DrawRectangle(
                new XSolidBrush(XColor.FromArgb(color.R, color.G, color.B)),
                margin,
                swatchY,
                swatchSize,
                swatchSize
            );
            gfx.DrawRectangle(XPens.Black, margin, swatchY, swatchSize, swatchSize);

            gfx.DrawString(
                rowText,
                rowFont,
                XBrushes.Black,
                new XRect(margin + swatchSize + MmToPoint(3), y, page.Width - (2 * margin) - swatchSize, lineHeight),
                XStringFormats.CenterLeft
            );

            y += lineHeight;
        }

        gfx?.Dispose();
    }

    private static byte[] RenderMaterialsCsv(ValidatedExport data)
    {
        var totalCells = data.Rows * data.Columns;
        var builder = new StringBuilder();
        builder.AppendLine("color_id,color_name,ral_code,hex,cells,ratio_percent");

        foreach (var item in data.CountsByColorId.OrderByDescending(item => item.Value).ThenBy(item => item.Key))
        {
            var color = data.PaletteById[item.Key];
            var ratioPercent = (100.0 * item.Value / totalCells).ToString("0.###", CultureInfo.InvariantCulture);
            builder.Append(item.Key)
                .Append(',')
                .Append(CsvEscape(color.Name))
                .Append(',')
                .Append(CsvEscape(color.RalCode))
                .Append(',')
                .Append(color.RgbHex)
                .Append(',')
                .Append(item.Value)
                .Append(',')
                .AppendLine(ratioPercent);
        }

        return EncodeUtf8WithBom(builder.ToString());
    }

    private static byte[] RenderGridCsv(ValidatedExport data)
    {
        var builder = new StringBuilder(256 * 1024);
        builder.AppendLine("row_top,row_bottom,column_left,column_right,color_id,color_name,ral_code,hex");

        for (var row = 0; row < data.Rows; row++)
        {
            var rowTop = row + 1;
            var rowBottom = data.Rows - row;

            for (var column = 0; column < data.Columns; column++)
            {
                var displayColumn = column + 1;
                var columnFromRight = data.Columns - column;
                var sourceColumn = data.MirrorHorizontal
                    ? data.Columns - 1 - column
                    : column;

                var colorId = data.GridColorIds[row][sourceColumn];
                var color = data.PaletteById[colorId];

                builder.Append(rowTop)
                    .Append(',')
                    .Append(rowBottom)
                    .Append(',')
                    .Append(displayColumn)
                    .Append(',')
                    .Append(columnFromRight)
                    .Append(',')
                    .Append(colorId)
                    .Append(',')
                    .Append(CsvEscape(color.Name))
                    .Append(',')
                    .Append(CsvEscape(color.RalCode))
                    .Append(',')
                    .AppendLine(color.RgbHex);
            }
        }

        return EncodeUtf8WithBom(builder.ToString());
    }

    private static byte[] RenderModulesCsv(ValidatedExport data)
    {
        var modules = BuildModules(data);
        var builder = new StringBuilder(96 * 1024);
        builder.AppendLine("module_number,module_row_from_bottom,module_column_from_left,row_top_start,row_top_end,row_bottom_start,row_bottom_end,column_left_start,column_left_end,column_right_start,column_right_end,cells,used_colors,dominant_color_id,dominant_color_name,dominant_color_ral,dominant_color_hex");

        foreach (var module in modules)
        {
            var counts = GetModuleColorCounts(data, module);
            var dominantColor = counts
                .OrderByDescending(item => item.Value)
                .ThenBy(item => item.Key)
                .Select(item => data.PaletteById[item.Key])
                .FirstOrDefault();

            var rowTopStart = module.RowStart + 1;
            var rowTopEnd = module.RowEndExclusive;
            var rowBottomStart = data.Rows - module.RowEndExclusive + 1;
            var rowBottomEnd = data.Rows - module.RowStart;
            var columnLeftStart = module.ColumnStart + 1;
            var columnLeftEnd = module.ColumnEndExclusive;
            var columnRightStart = data.Columns - module.ColumnEndExclusive + 1;
            var columnRightEnd = data.Columns - module.ColumnStart;
            var cells = (module.RowEndExclusive - module.RowStart) * (module.ColumnEndExclusive - module.ColumnStart);

            builder.Append(module.Number)
                .Append(',')
                .Append(module.ModuleRowFromBottom)
                .Append(',')
                .Append(module.ModuleColumnFromLeft)
                .Append(',')
                .Append(rowTopStart)
                .Append(',')
                .Append(rowTopEnd)
                .Append(',')
                .Append(rowBottomStart)
                .Append(',')
                .Append(rowBottomEnd)
                .Append(',')
                .Append(columnLeftStart)
                .Append(',')
                .Append(columnLeftEnd)
                .Append(',')
                .Append(columnRightStart)
                .Append(',')
                .Append(columnRightEnd)
                .Append(',')
                .Append(cells)
                .Append(',')
                .Append(counts.Count)
                .Append(',')
                .Append(dominantColor?.Id ?? 0)
                .Append(',')
                .Append(CsvEscape(dominantColor?.Name ?? string.Empty))
                .Append(',')
                .Append(CsvEscape(dominantColor?.RalCode ?? string.Empty))
                .Append(',')
                .AppendLine(dominantColor?.RgbHex ?? string.Empty);
        }

        return EncodeUtf8WithBom(builder.ToString());
    }

    private static byte[] RenderAssemblyKitPdf(ValidatedExport data)
    {
        using var document = new PdfDocument();
        var modules = BuildModules(data);

        AppendAssemblyOverviewPage(document, data, modules);
        foreach (var module in modules)
        {
            AppendAssemblyModulePage(document, data, module);
        }

        if (data.IncludeLegend)
        {
            AppendLegendPages(document, data);
        }

        using var stream = new MemoryStream();
        document.Save(stream, closeStream: false);
        return stream.ToArray();
    }

    private static void AppendAssemblyOverviewPage(
        PdfDocument document,
        ValidatedExport data,
        IReadOnlyList<ModuleBlock> modules
    )
    {
        var page = document.AddPage();
        page.Size = PageSize.A4;
        page.Orientation = data.Columns >= data.Rows ? PageOrientation.Landscape : PageOrientation.Portrait;

        using var gfx = XGraphics.FromPdfPage(page);
        var titleFont = new XFont("Arial", 14, XFontStyle.Bold);
        var textFont = new XFont("Arial", 9, XFontStyle.Regular);
        var moduleFont = new XFont("Arial", 8, XFontStyle.Bold);
        var margin = MmToPoint(10);
        var mapTop = margin + MmToPoint(13);
        var mapWidth = page.Width - (2 * margin);
        var mapHeight = page.Height - mapTop - margin;

        gfx.DrawString(
            "Карта сборки (модули)",
            titleFont,
            XBrushes.Black,
            new XRect(margin, margin, page.Width - (2 * margin), MmToPoint(7)),
            XStringFormats.TopLeft
        );
        gfx.DrawString(
            $"Модулей: {modules.Count} | Нумерация снизу слева, слева направо | Стартовый №: {data.ModuleStartNumber}",
            textFont,
            XBrushes.Black,
            new XRect(margin, margin + MmToPoint(6), page.Width - (2 * margin), MmToPoint(6)),
            XStringFormats.TopLeft
        );

        gfx.DrawRectangle(XPens.Black, margin, mapTop, mapWidth, mapHeight);

        foreach (var module in modules)
        {
            var relativeX0 = module.ColumnStart / (double)data.Columns;
            var relativeX1 = module.ColumnEndExclusive / (double)data.Columns;
            var relativeY0 = module.RowStart / (double)data.Rows;
            var relativeY1 = module.RowEndExclusive / (double)data.Rows;

            double drawX0;
            double drawX1;
            if (data.MirrorHorizontal)
            {
                drawX0 = margin + ((1 - relativeX1) * mapWidth);
                drawX1 = margin + ((1 - relativeX0) * mapWidth);
            }
            else
            {
                drawX0 = margin + (relativeX0 * mapWidth);
                drawX1 = margin + (relativeX1 * mapWidth);
            }

            var drawY0 = mapTop + (relativeY0 * mapHeight);
            var drawY1 = mapTop + (relativeY1 * mapHeight);
            var rectWidth = Math.Max(1, drawX1 - drawX0);
            var rectHeight = Math.Max(1, drawY1 - drawY0);

            gfx.DrawRectangle(XPens.DarkSlateGray, drawX0, drawY0, rectWidth, rectHeight);
            gfx.DrawString(
                module.Number.ToString(CultureInfo.InvariantCulture),
                moduleFont,
                XBrushes.DarkBlue,
                new XRect(drawX0, drawY0, rectWidth, rectHeight),
                XStringFormats.Center
            );
        }
    }

    private static void AppendAssemblyModulePage(PdfDocument document, ValidatedExport data, ModuleBlock module)
    {
        var page = document.AddPage();
        page.Size = PageSize.A4;
        page.Orientation = PageOrientation.Portrait;

        using var gfx = XGraphics.FromPdfPage(page);
        var titleFont = new XFont("Arial", 13, XFontStyle.Bold);
        var infoFont = new XFont("Arial", 8.5, XFontStyle.Regular);
        var numberFont = new XFont("Arial", 7, XFontStyle.Regular);
        var margin = MmToPoint(9);
        var headerHeight = MmToPoint(18);
        var legendHeight = MmToPoint(38);
        var gridTop = margin + headerHeight;
        var gridHeight = page.Height - gridTop - margin - legendHeight;
        var gridWidth = page.Width - (2 * margin);

        var rows = module.RowEndExclusive - module.RowStart;
        var columns = module.ColumnEndExclusive - module.ColumnStart;
        var cellSize = Math.Max(2.5, Math.Min(gridWidth / columns, gridHeight / rows));
        var drawWidth = cellSize * columns;
        var drawHeight = cellSize * rows;
        var drawX = margin + ((gridWidth - drawWidth) / 2);
        var drawY = gridTop + ((gridHeight - drawHeight) / 2);

        gfx.DrawString(
            $"Модуль №{module.Number}",
            titleFont,
            XBrushes.Black,
            new XRect(margin, margin, page.Width - (2 * margin), MmToPoint(8)),
            XStringFormats.TopLeft
        );
        gfx.DrawString(
            $"Ряд снизу={module.ModuleRowFromBottom}, колонка слева={module.ModuleColumnFromLeft} | Чипов: {columns} x {rows} | Зеркально: {(data.MirrorHorizontal ? "да" : "нет")}",
            infoFont,
            XBrushes.Black,
            new XRect(margin, margin + MmToPoint(6), page.Width - (2 * margin), MmToPoint(8)),
            XStringFormats.TopLeft
        );
        gfx.DrawString(
            "На этом листе ряды идут сверху вниз. Для подбора чипов используйте номера цветов и легенду.",
            infoFont,
            XBrushes.Black,
            new XRect(margin, margin + MmToPoint(11), page.Width - (2 * margin), MmToPoint(8)),
            XStringFormats.TopLeft
        );

        var grout = ColorMath.HexToRgb(data.GroutColorHex);
        gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(grout.R, grout.G, grout.B)), drawX, drawY, drawWidth, drawHeight);

        var canDrawNumbers = data.IncludeColorNumbers && cellSize >= 7.5;
        for (var row = 0; row < rows; row++)
        {
            var sourceRow = module.RowStart + row;
            for (var column = 0; column < columns; column++)
            {
                var sourceColumn = module.ColumnStart + (data.MirrorHorizontal ? (columns - 1 - column) : column);
                var colorId = data.GridColorIds[sourceRow][sourceColumn];
                var color = data.PaletteById[colorId];

                var x = drawX + (column * cellSize);
                var y = drawY + (row * cellSize);
                gfx.DrawRectangle(
                    new XSolidBrush(XColor.FromArgb(color.R, color.G, color.B)),
                    x,
                    y,
                    cellSize,
                    cellSize
                );
                gfx.DrawRectangle(XPens.Gray, x, y, cellSize, cellSize);

                if (canDrawNumbers && data.ColorNumberByColorId.TryGetValue(colorId, out var colorNumber))
                {
                    var textColor = IsLightColor(color) ? XBrushes.Black : XBrushes.White;
                    gfx.DrawString(
                        colorNumber.ToString(CultureInfo.InvariantCulture),
                        numberFont,
                        textColor,
                        new XRect(x, y, cellSize, cellSize),
                        XStringFormats.Center
                    );
                }
            }
        }

        DrawModuleLegend(gfx, data, module, margin, page.Height - margin - legendHeight, page.Width - (2 * margin), legendHeight, infoFont);
    }

    private static void DrawModuleLegend(
        XGraphics gfx,
        ValidatedExport data,
        ModuleBlock module,
        double x,
        double y,
        double width,
        double height,
        XFont font
    )
    {
        var counts = GetModuleColorCounts(data, module);
        var sorted = counts.OrderByDescending(item => item.Value).ThenBy(item => item.Key).ToList();
        if (sorted.Count == 0)
        {
            return;
        }

        gfx.DrawRectangle(XPens.LightGray, x, y, width, height);
        gfx.DrawString(
            "Легенда (номер -> цвет)",
            new XFont("Arial", 8.5, XFontStyle.Bold),
            XBrushes.Black,
            new XRect(x + MmToPoint(1), y + MmToPoint(1), width - MmToPoint(2), MmToPoint(5)),
            XStringFormats.TopLeft
        );

        var rowHeight = MmToPoint(5.2);
        var columns = Math.Max(1, Math.Min(3, (int)Math.Floor(width / MmToPoint(62))));
        var colWidth = width / columns;
        var startY = y + MmToPoint(6.5);
        var maxRowsPerColumn = Math.Max(1, (int)Math.Floor((height - MmToPoint(8)) / rowHeight));

        for (var index = 0; index < sorted.Count; index++)
        {
            var col = index / maxRowsPerColumn;
            var row = index % maxRowsPerColumn;
            if (col >= columns)
            {
                break;
            }

            var item = sorted[index];
            var color = data.PaletteById[item.Key];
            var colorNumber = data.ColorNumberByColorId[item.Key];
            var rowX = x + (col * colWidth) + MmToPoint(1.2);
            var rowY = startY + (row * rowHeight);
            var swatch = MmToPoint(3.8);
            gfx.DrawRectangle(
                new XSolidBrush(XColor.FromArgb(color.R, color.G, color.B)),
                rowX,
                rowY + MmToPoint(0.6),
                swatch,
                swatch
            );
            gfx.DrawRectangle(XPens.Black, rowX, rowY + MmToPoint(0.6), swatch, swatch);
            gfx.DrawString(
                $"{colorNumber}: {color.Name} ({color.RalCode}) [{item.Value}]",
                font,
                XBrushes.Black,
                new XRect(rowX + swatch + MmToPoint(1.3), rowY, colWidth - swatch - MmToPoint(3), rowHeight),
                XStringFormats.CenterLeft
            );
        }
    }

    private static Dictionary<int, int> GetModuleColorCounts(ValidatedExport data, ModuleBlock module)
    {
        var counts = new Dictionary<int, int>();
        for (var row = module.RowStart; row < module.RowEndExclusive; row++)
        {
            for (var column = module.ColumnStart; column < module.ColumnEndExclusive; column++)
            {
                var colorId = data.GridColorIds[row][column];
                counts[colorId] = counts.TryGetValue(colorId, out var current) ? current + 1 : 1;
            }
        }

        return counts;
    }

    private static bool IsLightColor(PaletteColor color)
    {
        var luminance = (0.2126 * color.R) + (0.7152 * color.G) + (0.0722 * color.B);
        return luminance > 148;
    }

    private static string CsvEscape(string value)
    {
        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n') && !value.Contains('\r'))
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static byte[] EncodeUtf8WithBom(string value)
    {
        var utf8 = Encoding.UTF8;
        var bom = utf8.GetPreamble();
        var payload = utf8.GetBytes(value);
        var combined = new byte[bom.Length + payload.Length];
        Buffer.BlockCopy(bom, 0, combined, 0, bom.Length);
        Buffer.BlockCopy(payload, 0, combined, bom.Length, payload.Length);
        return combined;
    }

    private static double MmToPoint(double mm) => mm * 72.0 / 25.4;
}
