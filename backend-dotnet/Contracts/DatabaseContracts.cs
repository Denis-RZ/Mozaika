namespace Mozaika.Api.Contracts;

public sealed class DatabaseConfigReadResponse
{
    public string Provider { get; set; } = "sqlite";
    public string ConnectionString { get; set; } = string.Empty;
    public bool Echo { get; set; }
    public List<string> SupportedProviders { get; set; } = [];
}

public sealed class DatabaseConfigUpdateRequest
{
    public string Provider { get; set; } = string.Empty;
    public string ConnectionString { get; set; } = string.Empty;
    public bool? Echo { get; set; }
    public bool? CreateSchema { get; set; }
    public bool? SeedDefaults { get; set; }
}

public sealed class DatabaseConfigTestResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}
