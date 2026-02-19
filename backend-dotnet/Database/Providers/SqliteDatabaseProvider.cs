using Microsoft.EntityFrameworkCore;

namespace Mozaika.Api.Database.Providers;

public sealed class SqliteDatabaseProvider : IDatabaseProvider
{
    public string Name => "sqlite";

    public void Configure(DbContextOptionsBuilder optionsBuilder, string connectionString)
    {
        optionsBuilder.UseSqlite(connectionString);
    }
}
