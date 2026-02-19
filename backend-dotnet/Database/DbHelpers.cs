using Microsoft.EntityFrameworkCore;
using Mozaika.Api.Database.Entities;

namespace Mozaika.Api.Database;

public static class DbHelpers
{
    public static async Task<AppSettingEntity> GetOrCreateSettingsAsync(MozaikaDbContext dbContext)
    {
        var settings = await dbContext.AppSettings.FirstOrDefaultAsync(item => item.Id == 1);
        if (settings is not null)
        {
            return settings;
        }

        settings = new AppSettingEntity
        {
            Id = 1,
            DefaultFieldWidthMm = 1200,
            DefaultFieldHeightMm = 800,
            DefaultCellSizeMm = 10,
            DefaultGapMm = 2,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        dbContext.AppSettings.Add(settings);
        await dbContext.SaveChangesAsync();
        return settings;
    }
}
