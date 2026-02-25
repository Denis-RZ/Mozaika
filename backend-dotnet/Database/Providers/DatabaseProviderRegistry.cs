using Microsoft.EntityFrameworkCore;
using Mozaika.Api.Options;

namespace Mozaika.Api.Database.Providers;

public interface IDatabaseProviderRegistry
{
    void Configure(DbContextOptionsBuilder optionsBuilder, DatabaseOptions options);
    IReadOnlyList<string> GetSupportedProviderNames();
    bool IsSupported(string providerName);
}

public sealed class DatabaseProviderRegistry : IDatabaseProviderRegistry
{
    private readonly Dictionary<string, IDatabaseProvider> _providers;

    public DatabaseProviderRegistry()
    {
        IDatabaseProvider[] providers = [
            new SqliteDatabaseProvider(),
            new JsonDatabaseProvider(),
            new PostgresDatabaseProvider(),
            new SqlServerDatabaseProvider(),
            new MySqlDatabaseProvider(),
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

    public IReadOnlyList<string> GetSupportedProviderNames() =>
        _providers.Keys
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public bool IsSupported(string providerName) =>
        !string.IsNullOrWhiteSpace(providerName) && _providers.ContainsKey(providerName.Trim());
}
