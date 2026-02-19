namespace Mozaika.Api.Contracts;

public sealed class BootstrapResponse
{
    public AdminSettingsReadResponse Settings { get; set; } = new();
    public List<ColorReadResponse> Colors { get; set; } = [];
    public List<GroutColorReadResponse> GroutColors { get; set; } = [];
}
