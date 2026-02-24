using System.ComponentModel.DataAnnotations;

namespace Mozaika.Api.Contracts;

public sealed class MosaicColorUsageResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string RalCode { get; set; } = string.Empty;
    public string RgbHex { get; set; } = string.Empty;
    public int Cells { get; set; }
    public double Ratio { get; set; }
}

public sealed class MosaicPriceBreakdownResponse
{
    public string Currency { get; set; } = "RUB";
    public int TotalChips { get; set; }
    public double AreaSqM { get; set; }
    public double SetupPrice { get; set; }
    public double ChipsPrice { get; set; }
    public double ColorsPrice { get; set; }
    public double ComplexityPrice { get; set; }
    public double GroutPrice { get; set; }
    public double SubtotalPrice { get; set; }
    public double MinOrderPrice { get; set; }
    public bool MinOrderApplied { get; set; }
    public double TotalPrice { get; set; }
}

public sealed class MosaicReplaceColorRequest
{
    [Required(ErrorMessage = "Нужна сетка мозаики для замены цвета.")]
    [MinLength(1, ErrorMessage = "Сетка мозаики не может быть пустой.")]
    public int[][] GridColorIds { get; set; } = [];
    public int FromColorId { get; set; }
    public int ToColorId { get; set; }
    public double FieldWidthMm { get; set; }
    public double FieldHeightMm { get; set; }
    public double CellSizeMm { get; set; }
    public double GapMm { get; set; }
    public double OffsetXMm { get; set; }
    public double OffsetYMm { get; set; }
    public string GroutColorHex { get; set; } = "#FFFFFF";
    public int? RequestedMaxColors { get; set; }
}

public sealed class MosaicGenerateResponse
{
    public int Rows { get; set; }
    public int Columns { get; set; }
    public double FieldWidthMm { get; set; }
    public double FieldHeightMm { get; set; }
    public double MosaicWidthMm { get; set; }
    public double MosaicHeightMm { get; set; }
    public double CellSizeMm { get; set; }
    public double GapMm { get; set; }
    public double OffsetXMm { get; set; }
    public double OffsetYMm { get; set; }
    public string GroutColorHex { get; set; } = string.Empty;
    public int RequestedMaxColors { get; set; }
    public int ActualColorsUsed { get; set; }
    public int TotalChips { get; set; }
    public MosaicPriceBreakdownResponse Price { get; set; } = new();
    public List<MosaicColorUsageResponse> UsedColors { get; set; } = [];
    public int[][] GridColorIds { get; set; } = [];
    public string PreviewPngBase64 { get; set; } = string.Empty;
}

public sealed class MosaicExportRequest
{
    [Required(ErrorMessage = "Нужна сетка мозаики для экспорта.")]
    [MinLength(1, ErrorMessage = "Сетка мозаики не может быть пустой.")]
    public int[][] GridColorIds { get; set; } = [];
    public double FieldWidthMm { get; set; }
    public double FieldHeightMm { get; set; }
    public double CellSizeMm { get; set; }
    public double GapMm { get; set; }
    public double OffsetXMm { get; set; }
    public double OffsetYMm { get; set; }
    public string GroutColorHex { get; set; } = "#FFFFFF";
    public int Dpi { get; set; } = 150;
    public bool MirrorHorizontal { get; set; }
    public bool IncludeLegend { get; set; } = true;
    public int ModuleChipColumns { get; set; } = 32;
    public int ModuleChipRows { get; set; } = 32;
    public int ModuleStartNumber { get; set; } = 1;
    public bool IncludeColorNumbers { get; set; } = true;
    [MaxLength(140, ErrorMessage = "Имя файла слишком длинное.")]
    public string FileName { get; set; } = "mozaika";
}
