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
    public List<MosaicColorUsageResponse> UsedColors { get; set; } = [];
    public int[][] GridColorIds { get; set; } = [];
    public string PreviewPngBase64 { get; set; } = string.Empty;
}
