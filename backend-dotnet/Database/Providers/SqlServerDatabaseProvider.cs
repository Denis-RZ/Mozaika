using Microsoft.EntityFrameworkCore;

namespace Mozaika.Api.Database.Providers;

public sealed class SqlServerDatabaseProvider : IDatabaseProvider
{
    public string Name => "sqlserver";

    public void Configure(DbContextOptionsBuilder optionsBuilder, string connectionString)
    {
        optionsBuilder.UseSqlServer(connectionString);
    }
}
