using Microsoft.EntityFrameworkCore;

namespace Mozaika.Api.Database.Providers;

public sealed class PostgresDatabaseProvider : IDatabaseProvider
{
    public string Name => "postgres";

    public void Configure(DbContextOptionsBuilder optionsBuilder, string connectionString)
    {
        optionsBuilder.UseNpgsql(connectionString);
    }
}
