using Microsoft.EntityFrameworkCore;

namespace Mozaika.Api.Database.Providers;

public sealed class JsonDatabaseProvider : IDatabaseProvider
{
    public string Name => "json";

    public void Configure(DbContextOptionsBuilder optionsBuilder, string connectionString)
    {
        // A single in-memory store is used as runtime cache, while full persistence
        // is handled by JSON import/export synchronization service.
        optionsBuilder.UseInMemoryDatabase("mozaika-json-provider");
    }
}
