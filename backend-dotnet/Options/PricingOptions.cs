namespace Mozaika.Api.Options;

public sealed class PricingOptions
{
    public string Currency { get; set; } = "RUB";
    public double SetupPrice { get; set; } = 0;
    public double PricePerChip { get; set; } = 1.75;
    public double PricePerUsedColor { get; set; } = 120;
    public int ComplexityThresholdColors { get; set; } = 12;
    public double ExtraPricePerColorAboveThreshold { get; set; } = 70;
    public double GroutPricePerSquareMeter { get; set; } = 0;
    public double MinOrderPrice { get; set; } = 0;
}
