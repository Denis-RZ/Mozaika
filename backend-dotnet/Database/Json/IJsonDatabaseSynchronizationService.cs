using Mozaika.Api.Options;

namespace Mozaika.Api.Database.Json;

public interface IJsonDatabaseSynchronizationService
{
    Task ImportFromFileIfEnabledAsync(MozaikaDbContext dbContext, CancellationToken cancellationToken = default);
    Task PersistToFileIfEnabledAsync(MozaikaDbContext dbContext, CancellationToken cancellationToken = default);
    IDisposable SuppressPersistence();
}

public sealed class JsonDatabaseSynchronizationService(
    RuntimeDatabaseSettingsStore runtimeDatabaseSettingsStore
) : IJsonDatabaseSynchronizationService
{
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private readonly AsyncLocal<int> _suppressDepth = new();

    public async Task ImportFromFileIfEnabledAsync(MozaikaDbContext dbContext, CancellationToken cancellationToken = default)
    {
        var options = runtimeDatabaseSettingsStore.GetSnapshot();
        if (!JsonDatabaseFileStorage.IsJsonProvider(options))
        {
            return;
        }

        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            using var _ = SuppressPersistence();
            await JsonDatabaseFileStorage.ImportAsync(dbContext, options, cancellationToken);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task PersistToFileIfEnabledAsync(MozaikaDbContext dbContext, CancellationToken cancellationToken = default)
    {
        if (_suppressDepth.Value > 0)
        {
            return;
        }

        var options = runtimeDatabaseSettingsStore.GetSnapshot();
        if (!JsonDatabaseFileStorage.IsJsonProvider(options))
        {
            return;
        }

        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            await JsonDatabaseFileStorage.ExportAsync(dbContext, options, cancellationToken);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public IDisposable SuppressPersistence()
    {
        _suppressDepth.Value = _suppressDepth.Value + 1;
        return new SuppressScope(_suppressDepth);
    }

    private sealed class SuppressScope(AsyncLocal<int> suppressDepth) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (suppressDepth.Value > 0)
            {
                suppressDepth.Value--;
            }
        }
    }
}
