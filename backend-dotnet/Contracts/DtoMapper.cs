using Mozaika.Api.Database.Entities;
using Mozaika.Api.Security;

namespace Mozaika.Api.Contracts;

public static class DtoMapper
{
    public static ColorReadResponse ToRead(this ColorEntity entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        RalCode = entity.RalCode,
        RgbHex = entity.RgbHex,
        IsActive = entity.IsActive,
        CreatedAt = entity.CreatedAt,
    };

    public static GroutColorReadResponse ToRead(this GroutColorEntity entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        RgbHex = entity.RgbHex,
        IsActive = entity.IsActive,
        CreatedAt = entity.CreatedAt,
    };

    public static AdminSettingsReadResponse ToRead(this AppSettingEntity entity) => new()
    {
        Id = entity.Id,
        DefaultFieldWidthMm = entity.DefaultFieldWidthMm,
        DefaultFieldHeightMm = entity.DefaultFieldHeightMm,
        DefaultCellSizeMm = entity.DefaultCellSizeMm,
        DefaultGapMm = entity.DefaultGapMm,
        CorsOrigins = [.. RuntimeCorsOriginsStore.ParseOriginsJson(entity.CorsOriginsJson)],
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt,
    };
}
