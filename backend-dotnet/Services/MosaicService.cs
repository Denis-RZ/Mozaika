using Mozaika.Api.Contracts;
using Mozaika.Api.Database.Entities;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Mozaika.Api.Services;

public sealed class MosaicGenerationException : Exception
{
    public MosaicGenerationException(string message)
        : base(message)
    {
    }

    public MosaicGenerationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class MosaicService
{
    private sealed record PaletteColor(
        int Id,
        string Name,
        string RalCode,
        string RgbHex,
        byte R,
        byte G,
        byte B,
        ColorMath.Lab Lab
    );

    private readonly record struct SourcePixel(byte R, byte G, byte B);

    private sealed class SourceGrid
    {
        public required SourcePixel[,] Pixels { get; init; }
        public required ColorMath.Lab[,] Labs { get; init; }
    }

    public MosaicGenerateResponse Generate(
        byte[] imageBytes,
        IReadOnlyList<ColorEntity> availableColors,
        double fieldWidthMm,
        double fieldHeightMm,
        double cellSizeMm,
        double gapMm,
        string groutColorHex,
        int? maxColors,
        ISet<int> includeColorIds,
        ISet<int> excludeColorIds,
        double offsetXMm,
        double offsetYMm
    )
    {
        if (includeColorIds.Intersect(excludeColorIds).Any())
        {
            throw new MosaicGenerationException("Один цвет нельзя одновременно включить и исключить.");
        }

        var (rows, columns, mosaicWidthMm, mosaicHeightMm) = ComputeGrid(fieldWidthMm, fieldHeightMm, cellSizeMm, gapMm);

        if (offsetXMm < 0 || offsetYMm < 0)
        {
            throw new MosaicGenerationException("Смещение мозаики не может быть отрицательным.");
        }

        if (offsetXMm + mosaicWidthMm > fieldWidthMm + 1e-6)
        {
            throw new MosaicGenerationException("Смещение X выводит мозаику за границы поля.");
        }

        if (offsetYMm + mosaicHeightMm > fieldHeightMm + 1e-6)
        {
            throw new MosaicGenerationException("Смещение Y выводит мозаику за границы поля.");
        }

        groutColorHex = ColorMath.NormalizeHex(groutColorHex);
        var source = PrepareSourcePixels(imageBytes, rows, columns);

        var activePalette = availableColors
            .Where(color => color.IsActive && !excludeColorIds.Contains(color.Id))
            .Select(color =>
            {
                var normalizedHex = ColorMath.NormalizeHex(color.RgbHex);
                var rgb = ColorMath.HexToRgb(normalizedHex);
                return new PaletteColor(
                    color.Id,
                    color.Name,
                    color.RalCode,
                    normalizedHex,
                    rgb.R,
                    rgb.G,
                    rgb.B,
                    ColorMath.RgbToLab(rgb.R, rgb.G, rgb.B)
                );
            })
            .ToList();

        var paletteIds = activePalette.Select(item => item.Id).ToHashSet();
        var missingIncludes = includeColorIds.Except(paletteIds).OrderBy(item => item).ToArray();
        if (missingIncludes.Length > 0)
        {
            throw new MosaicGenerationException($"Цвета с id {string.Join(", ", missingIncludes)} недоступны для включения.");
        }

        if (activePalette.Count == 0)
        {
            throw new MosaicGenerationException("Нет активных цветов для генерации.");
        }

        var requestedMaxColors = maxColors ?? activePalette.Count;
        if (requestedMaxColors <= 0)
        {
            throw new MosaicGenerationException("Параметр max_colors должен быть больше нуля.");
        }

        requestedMaxColors = Math.Min(requestedMaxColors, activePalette.Count);

        var initialGrid = NearestPaletteIndices(source.Labs, activePalette);
        var selectedIndices = PickFinalPalette(activePalette, initialGrid, requestedMaxColors, includeColorIds);
        var finalPalette = selectedIndices.Select(index => activePalette[index]).ToList();

        var finalGrid = NearestPaletteIndices(source.Labs, finalPalette);
        finalGrid = EnforceIncludedColorsPresence(finalGrid, source.Labs, finalPalette, includeColorIds);

        var counts = new int[finalPalette.Count];
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                counts[finalGrid[row, column]]++;
            }
        }

        var totalCells = rows * columns;
        var usedColors = new List<MosaicColorUsageResponse>();
        for (var index = 0; index < finalPalette.Count; index++)
        {
            var cellCount = counts[index];
            if (cellCount == 0)
            {
                continue;
            }

            var color = finalPalette[index];
            usedColors.Add(new MosaicColorUsageResponse
            {
                Id = color.Id,
                Name = color.Name,
                RalCode = color.RalCode,
                RgbHex = color.RgbHex,
                Cells = cellCount,
                Ratio = (double)cellCount / totalCells,
            });
        }

        var gridColorIds = new int[rows][];
        for (var row = 0; row < rows; row++)
        {
            var rowColorIds = new int[columns];
            for (var column = 0; column < columns; column++)
            {
                rowColorIds[column] = finalPalette[finalGrid[row, column]].Id;
            }

            gridColorIds[row] = rowColorIds;
        }

        var previewPngBase64 = RenderPreview(
            finalGrid,
            finalPalette,
            fieldWidthMm,
            fieldHeightMm,
            cellSizeMm,
            gapMm,
            offsetXMm,
            offsetYMm,
            groutColorHex
        );

        return new MosaicGenerateResponse
        {
            Rows = rows,
            Columns = columns,
            FieldWidthMm = fieldWidthMm,
            FieldHeightMm = fieldHeightMm,
            MosaicWidthMm = mosaicWidthMm,
            MosaicHeightMm = mosaicHeightMm,
            CellSizeMm = cellSizeMm,
            GapMm = gapMm,
            OffsetXMm = offsetXMm,
            OffsetYMm = offsetYMm,
            GroutColorHex = groutColorHex,
            RequestedMaxColors = requestedMaxColors,
            ActualColorsUsed = usedColors.Count,
            UsedColors = usedColors,
            GridColorIds = gridColorIds,
            PreviewPngBase64 = previewPngBase64,
        };
    }

    private static (int Rows, int Columns, double MosaicWidthMm, double MosaicHeightMm) ComputeGrid(
        double fieldWidthMm,
        double fieldHeightMm,
        double cellSizeMm,
        double gapMm
    )
    {
        if (fieldWidthMm <= 0 || fieldHeightMm <= 0)
        {
            throw new MosaicGenerationException("Ширина и высота поля должны быть больше нуля.");
        }

        if (cellSizeMm <= 0)
        {
            throw new MosaicGenerationException("Размер ячейки должен быть больше нуля.");
        }

        if (gapMm < 0)
        {
            throw new MosaicGenerationException("Расстояние между ячейками не может быть отрицательным.");
        }

        var pitch = cellSizeMm + gapMm;
        var columns = (int)Math.Floor((fieldWidthMm + gapMm) / pitch);
        var rows = (int)Math.Floor((fieldHeightMm + gapMm) / pitch);

        if (rows <= 0 || columns <= 0)
        {
            throw new MosaicGenerationException("С текущими параметрами на поле не помещается ни одной ячейки.");
        }

        var mosaicWidthMm = (columns * cellSizeMm) + ((columns - 1) * gapMm);
        var mosaicHeightMm = (rows * cellSizeMm) + ((rows - 1) * gapMm);

        return (rows, columns, mosaicWidthMm, mosaicHeightMm);
    }

    private static SourceGrid PrepareSourcePixels(byte[] imageBytes, int rows, int columns)
    {
        if (imageBytes.Length == 0)
        {
            throw new MosaicGenerationException("Файл изображения пустой.");
        }

        try
        {
            using var image = Image.Load<Rgb24>(imageBytes);
            var targetAspect = (double)columns / rows;
            var sourceAspect = (double)image.Width / image.Height;

            if (sourceAspect > targetAspect)
            {
                var newWidth = Math.Max(1, (int)Math.Round(image.Height * targetAspect));
                var left = (image.Width - newWidth) / 2;
                image.Mutate(context => context.Crop(new Rectangle(left, 0, newWidth, image.Height)));
            }
            else if (sourceAspect < targetAspect)
            {
                var newHeight = Math.Max(1, (int)Math.Round(image.Width / targetAspect));
                var top = (image.Height - newHeight) / 2;
                image.Mutate(context => context.Crop(new Rectangle(0, top, image.Width, newHeight)));
            }

            image.Mutate(context => context.Resize(new ResizeOptions
            {
                Size = new Size(columns, rows),
                Sampler = KnownResamplers.Box,
                Mode = ResizeMode.Stretch,
            }));

            var pixels = new SourcePixel[rows, columns];
            var labs = new ColorMath.Lab[rows, columns];

            for (var row = 0; row < rows; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    var pixel = image[column, row];
                    pixels[row, column] = new SourcePixel(pixel.R, pixel.G, pixel.B);
                    labs[row, column] = ColorMath.RgbToLab(pixel.R, pixel.G, pixel.B);
                }
            }

            return new SourceGrid
            {
                Pixels = pixels,
                Labs = labs,
            };
        }
        catch (MosaicGenerationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new MosaicGenerationException("Не удалось прочитать изображение.", ex);
        }
    }

    private static int[,] NearestPaletteIndices(ColorMath.Lab[,] pixelLabs, IReadOnlyList<PaletteColor> palette)
    {
        var rows = pixelLabs.GetLength(0);
        var columns = pixelLabs.GetLength(1);
        var grid = new int[rows, columns];

        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var pixelLab = pixelLabs[row, column];
                var bestIndex = 0;
                var bestDistance = double.MaxValue;

                for (var paletteIndex = 0; paletteIndex < palette.Count; paletteIndex++)
                {
                    var distance = ColorMath.SquaredDistance(pixelLab, palette[paletteIndex].Lab);
                    if (distance >= bestDistance)
                    {
                        continue;
                    }

                    bestDistance = distance;
                    bestIndex = paletteIndex;
                }

                grid[row, column] = bestIndex;
            }
        }

        return grid;
    }

    private static List<int> PickFinalPalette(
        IReadOnlyList<PaletteColor> palette,
        int[,] initialGrid,
        int maxColors,
        ISet<int> includeColorIds
    )
    {
        var forcedIndices = new List<int>();
        for (var index = 0; index < palette.Count; index++)
        {
            if (includeColorIds.Contains(palette[index].Id))
            {
                forcedIndices.Add(index);
            }
        }

        if (maxColors < forcedIndices.Count)
        {
            throw new MosaicGenerationException("Число max_colors меньше количества принудительно включенных цветов.");
        }

        if (maxColors >= palette.Count)
        {
            return Enumerable.Range(0, palette.Count).ToList();
        }

        var counts = new int[palette.Count];
        var rows = initialGrid.GetLength(0);
        var columns = initialGrid.GetLength(1);
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                counts[initialGrid[row, column]]++;
            }
        }

        var ranked = Enumerable.Range(0, palette.Count)
            .OrderByDescending(index => counts[index])
            .ToList();

        var selected = new List<int>();
        foreach (var index in forcedIndices)
        {
            if (!selected.Contains(index))
            {
                selected.Add(index);
            }
        }

        foreach (var index in ranked)
        {
            if (selected.Contains(index))
            {
                continue;
            }

            selected.Add(index);
            if (selected.Count == maxColors)
            {
                break;
            }
        }

        if (selected.Count < maxColors)
        {
            for (var index = 0; index < palette.Count; index++)
            {
                if (selected.Contains(index))
                {
                    continue;
                }

                selected.Add(index);
                if (selected.Count == maxColors)
                {
                    break;
                }
            }
        }

        return selected;
    }

    private static int[,] EnforceIncludedColorsPresence(
        int[,] finalGrid,
        ColorMath.Lab[,] pixelLabs,
        IReadOnlyList<PaletteColor> finalPalette,
        ISet<int> includeColorIds
    )
    {
        if (includeColorIds.Count == 0)
        {
            return finalGrid;
        }

        var rows = finalGrid.GetLength(0);
        var columns = finalGrid.GetLength(1);

        var paletteIndexById = new Dictionary<int, int>();
        for (var index = 0; index < finalPalette.Count; index++)
        {
            paletteIndexById[finalPalette[index].Id] = index;
        }

        var includeIndices = includeColorIds
            .Where(colorId => paletteIndexById.ContainsKey(colorId))
            .Select(colorId => paletteIndexById[colorId])
            .ToList();

        if (includeIndices.Count == 0)
        {
            return finalGrid;
        }

        if (includeIndices.Count > rows * columns)
        {
            throw new MosaicGenerationException("Слишком много принудительно включенных цветов для текущей сетки.");
        }

        var counts = new int[finalPalette.Count];
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                counts[finalGrid[row, column]]++;
            }
        }

        var missingIndices = includeIndices.Where(index => counts[index] == 0).ToList();
        if (missingIndices.Count == 0)
        {
            return finalGrid;
        }

        var nextGrid = (int[,])finalGrid.Clone();
        var reserved = new HashSet<int>();

        foreach (var paletteIndex in missingIndices)
        {
            var bestDistance = double.MaxValue;
            var bestRow = 0;
            var bestColumn = 0;
            var bestFlatIndex = -1;

            for (var row = 0; row < rows; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    var flatIndex = (row * columns) + column;
                    if (reserved.Contains(flatIndex))
                    {
                        continue;
                    }

                    var distance = ColorMath.SquaredDistance(pixelLabs[row, column], finalPalette[paletteIndex].Lab);
                    if (distance >= bestDistance)
                    {
                        continue;
                    }

                    bestDistance = distance;
                    bestRow = row;
                    bestColumn = column;
                    bestFlatIndex = flatIndex;
                }
            }

            if (bestFlatIndex >= 0)
            {
                reserved.Add(bestFlatIndex);
                nextGrid[bestRow, bestColumn] = paletteIndex;
            }
        }

        return nextGrid;
    }

    private static string RenderPreview(
        int[,] gridIndices,
        IReadOnlyList<PaletteColor> palette,
        double fieldWidthMm,
        double fieldHeightMm,
        double cellSizeMm,
        double gapMm,
        double offsetXMm,
        double offsetYMm,
        string groutColorHex
    )
    {
        const int maxPreviewSidePx = 1400;
        var scale = maxPreviewSidePx / Math.Max(fieldWidthMm, fieldHeightMm);
        scale = Math.Clamp(scale, 0.4, 8.0);

        var fieldWidthPx = Math.Max(1, (int)Math.Round(fieldWidthMm * scale));
        var fieldHeightPx = Math.Max(1, (int)Math.Round(fieldHeightMm * scale));
        var cellPx = Math.Max(1, (int)Math.Round(cellSizeMm * scale));
        var gapPx = Math.Max(0, (int)Math.Round(gapMm * scale));
        var stepPx = cellPx + gapPx;

        var groutRgb = ColorMath.HexToRgb(groutColorHex);
        using var image = new Image<Rgb24>(fieldWidthPx, fieldHeightPx, new Rgb24(groutRgb.R, groutRgb.G, groutRgb.B));

        var originXPx = (int)Math.Round(offsetXMm * scale);
        var originYPx = (int)Math.Round(offsetYMm * scale);

        var rows = gridIndices.GetLength(0);
        var columns = gridIndices.GetLength(1);

        for (var row = 0; row < rows; row++)
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

            for (var column = 0; column < columns; column++)
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

                var color = palette[gridIndices[row, column]];
                var tileColor = new Rgb24(color.R, color.G, color.B);

                var clippedY0 = Math.Max(y0, 0);
                var clippedX0 = Math.Max(x0, 0);
                for (var y = clippedY0; y < y1; y++)
                {
                    for (var x = clippedX0; x < x1; x++)
                    {
                        image[x, y] = tileColor;
                    }
                }
            }
        }

        using var memoryStream = new MemoryStream();
        image.SaveAsPng(memoryStream);
        return Convert.ToBase64String(memoryStream.ToArray());
    }
}
