namespace Mozaika.Api.Options;

public sealed class DatabaseOptions
{
    public string Provider { get; set; } = "sqlite";
    public string ConnectionString { get; set; } = "Data Source=../data/mozaika.local.db";
    public bool Echo { get; set; }
}
