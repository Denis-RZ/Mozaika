using Microsoft.EntityFrameworkCore;

namespace Mozaika.Api.Database.Providers;

public sealed class MySqlDatabaseProvider : IDatabaseProvider
{
    public string Name => "mysql";

    public void Configure(DbContextOptionsBuilder optionsBuilder, string connectionString)
    {
        optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
    }
}
