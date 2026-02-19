using Microsoft.EntityFrameworkCore;
using Mozaika.Api.Options;

namespace Mozaika.Api.Database.Providers;

public interface IDatabaseProvider
{
    string Name { get; }
    void Configure(DbContextOptionsBuilder optionsBuilder, string connectionString);
}
