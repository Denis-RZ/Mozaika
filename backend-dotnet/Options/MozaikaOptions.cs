namespace Mozaika.Api.Options;

public sealed class MozaikaOptions
{
    public string ApiPrefix { get; set; } = "/api";
    public int MaxUploadMb { get; set; } = 25;
    public string[] CorsOrigins { get; set; } = ["*"];
}
