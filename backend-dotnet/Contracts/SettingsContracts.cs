namespace Mozaika.Api.Contracts;

public sealed class AdminSettingsReadResponse
{
    public int Id { get; set; }
    public double DefaultFieldWidthMm { get; set; }
    public double DefaultFieldHeightMm { get; set; }
    public double DefaultCellSizeMm { get; set; }
    public double DefaultGapMm { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class AdminSettingsUpdateRequest
{
    public double? DefaultFieldWidthMm { get; set; }
    public double? DefaultFieldHeightMm { get; set; }
    public double? DefaultCellSizeMm { get; set; }
    public double? DefaultGapMm { get; set; }

    public bool HasAnyValue() =>
        DefaultFieldWidthMm is not null ||
        DefaultFieldHeightMm is not null ||
        DefaultCellSizeMm is not null ||
        DefaultGapMm is not null;
}
