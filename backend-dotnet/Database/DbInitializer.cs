using Microsoft.EntityFrameworkCore;
using Mozaika.Api.Database.Entities;

namespace Mozaika.Api.Database;

public static class DbInitializer
{
    public static async Task SeedAsync(MozaikaDbContext dbContext, bool seedDefaults = true)
    {
        await dbContext.Database.EnsureCreatedAsync();
        if (!seedDefaults)
        {
            return;
        }

        var settings = await dbContext.AppSettings.FindAsync(1);
        if (settings is null)
        {
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
        }

        if (!await dbContext.GroutColors.AnyAsync())
        {
            dbContext.GroutColors.AddRange(
                new GroutColorEntity
                {
                    Name = "Warm White",
                    RgbHex = "#F2EFEA",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                },
                new GroutColorEntity
                {
                    Name = "Graphite",
                    RgbHex = "#353535",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                },
                new GroutColorEntity
                {
                    Name = "Sand",
                    RgbHex = "#CDBB9C",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                }
            );
        }

        await dbContext.SaveChangesAsync();
    }
}
