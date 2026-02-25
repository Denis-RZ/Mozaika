using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Mozaika.Api.Database.Json;

public sealed class JsonDatabaseSaveChangesInterceptor(
    IJsonDatabaseSynchronizationService jsonDatabaseSynchronizationService
) : SaveChangesInterceptor
{
    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (result <= 0 || eventData.Context is not MozaikaDbContext dbContext)
        {
            return result;
        }

        jsonDatabaseSynchronizationService
            .PersistToFileIfEnabledAsync(dbContext)
            .GetAwaiter()
            .GetResult();

        return result;
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default
    )
    {
        if (result <= 0 || eventData.Context is not MozaikaDbContext dbContext)
        {
            return result;
        }

        await jsonDatabaseSynchronizationService.PersistToFileIfEnabledAsync(dbContext, cancellationToken);
        return result;
    }
}
