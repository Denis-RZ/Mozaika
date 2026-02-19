using Mozaika.Api.Options;

namespace Mozaika.Api.Database;

public sealed class RuntimeDatabaseSettingsStore
{
    private readonly object _sync = new();
    private DatabaseOptions _current;

    public RuntimeDatabaseSettingsStore(DatabaseOptions initial)
    {
        _current = Clone(initial);
    }

    public DatabaseOptions GetSnapshot()
    {
        lock (_sync)
        {
            return Clone(_current);
        }
    }

    public void Set(DatabaseOptions next)
    {
        lock (_sync)
        {
            _current = Clone(next);
        }
    }

    private static DatabaseOptions Clone(DatabaseOptions source) => new()
    {
        Provider = source.Provider,
        ConnectionString = source.ConnectionString,
        Echo = source.Echo,
    };
}
