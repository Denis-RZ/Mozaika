using Microsoft.EntityFrameworkCore;
using Mozaika.Api.Options;

namespace Mozaika.Api.Database.Providers;

public interface IDatabaseProviderRegistry
{
    void Configure(DbContextOptionsBuilder optionsBuilder, DatabaseOptions options);
}

public sealed class DatabaseProviderRegistry : IDatabaseProviderRegistry
{
    private readonly Dictionary<string, IDatabaseProvider> _providers;

    public DatabaseProviderRegistry()
    {
        IDatabaseProvider[] providers = [
            new SqliteDatabaseProvider(),
            new PostgresDatabaseProvider(),
            new SqlServerDatabaseProvider(),
        ];
        _providers = providers.ToDictionary(
            provider => provider.Name,
            provider => provider,
            StringComparer.OrdinalIgnoreCase
        );
    }

    public void Configure(DbContextOptionsBuilder optionsBuilder, DatabaseOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Provider))
        {
            throw new InvalidOperationException("Не задан провайдер базы данных.");
        }

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new InvalidOperationException("Не задана строка подключения к базе данных.");
        }

        if (!_providers.TryGetValue(options.Provider, out var provider))
        {
            throw new InvalidOperationException($"Неподдерживаемый провайдер БД: {options.Provider}");
        }

        provider.Configure(optionsBuilder, options.ConnectionString);
    }
}
