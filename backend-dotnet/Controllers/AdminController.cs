using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Mozaika.Api.Contracts;
using Mozaika.Api.Database;
using Mozaika.Api.Database.Entities;
using Mozaika.Api.Database.Json;
using Mozaika.Api.Database.Providers;
using Mozaika.Api.Options;
using Mozaika.Api.Security;
using Mozaika.Api.Services;

namespace Mozaika.Api.Controllers;

[ApiController]
[Route("api/admin")]
public sealed class AdminController(
    MozaikaDbContext dbContext,
    RuntimeDatabaseSettingsStore runtimeDatabaseSettings,
    IDatabaseProviderRegistry databaseProviderRegistry,
    RuntimeCorsOriginsStore runtimeCorsOriginsStore,
    AppSettingsDatabaseConfigWriter appSettingsDatabaseConfigWriter,
    IOptions<AuthOptions> authOptionsAccessor
) : ApiControllerBase
{
    [HttpGet("settings")]
    public async Task<ActionResult<AdminSettingsReadResponse>> GetSettings()
    {
        var authError = RequireAnyRole(AppRoles.Admin);
        if (authError is not null)
        {
            return authError;
        }

        var settings = await DbHelpers.GetOrCreateSettingsAsync(dbContext);
        return Ok(settings.ToRead());
    }

    [HttpPut("settings")]
    public async Task<ActionResult<AdminSettingsReadResponse>> UpdateSettings([FromBody] AdminSettingsUpdateRequest payload)
    {
        var authError = RequireAnyRole(AppRoles.Admin);
        if (authError is not null)
        {
            return authError;
        }

        if (!payload.HasAnyValue())
        {
            return BadRequest(new ApiError("Нужно передать хотя бы одно поле для обновления."));
        }

        if (payload.DefaultFieldWidthMm is not null && payload.DefaultFieldWidthMm <= 0)
        {
            return BadRequest(new ApiError("Ширина поля должна быть больше нуля."));
        }

        if (payload.DefaultFieldHeightMm is not null && payload.DefaultFieldHeightMm <= 0)
        {
            return BadRequest(new ApiError("Высота поля должна быть больше нуля."));
        }

        if (payload.DefaultCellSizeMm is not null && payload.DefaultCellSizeMm <= 0)
        {
            return BadRequest(new ApiError("Размер ячейки должен быть больше нуля."));
        }

        if (payload.DefaultGapMm is not null && payload.DefaultGapMm < 0)
        {
            return BadRequest(new ApiError("Расстояние между ячейками не может быть отрицательным."));
        }

        string[]? nextCorsOrigins = null;
        if (payload.CorsOrigins is not null)
        {
            var parsedCors = ParseCorsOrigins(payload.CorsOrigins);
            if (!parsedCors.IsValid)
            {
                return BadRequest(new ApiError(parsedCors.Error!));
            }

            nextCorsOrigins = parsedCors.Origins!;
        }

        var settings = await DbHelpers.GetOrCreateSettingsAsync(dbContext);

        if (payload.DefaultFieldWidthMm is not null)
        {
            settings.DefaultFieldWidthMm = payload.DefaultFieldWidthMm.Value;
        }

        if (payload.DefaultFieldHeightMm is not null)
        {
            settings.DefaultFieldHeightMm = payload.DefaultFieldHeightMm.Value;
        }

        if (payload.DefaultCellSizeMm is not null)
        {
            settings.DefaultCellSizeMm = payload.DefaultCellSizeMm.Value;
        }

        if (payload.DefaultGapMm is not null)
        {
            settings.DefaultGapMm = payload.DefaultGapMm.Value;
        }

        if (nextCorsOrigins is not null)
        {
            settings.CorsOriginsJson = RuntimeCorsOriginsStore.SerializeOrigins(nextCorsOrigins);
            runtimeCorsOriginsStore.Set(nextCorsOrigins);
        }

        settings.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        return Ok(settings.ToRead());
    }

    [HttpGet("database")]
    public ActionResult<DatabaseConfigReadResponse> GetDatabaseConfig()
    {
        var authError = RequireAnyRole(AppRoles.Admin);
        if (authError is not null)
        {
            return authError;
        }

        var options = runtimeDatabaseSettings.GetSnapshot();
        return Ok(BuildDatabaseConfigResponse(options));
    }

    [HttpPut("database")]
    public async Task<ActionResult<DatabaseConfigReadResponse>> UpdateDatabaseConfig([FromBody] DatabaseConfigUpdateRequest payload)
    {
        var authError = RequireAnyRole(AppRoles.Admin);
        if (authError is not null)
        {
            return authError;
        }

        var parsed = ParseDatabasePayload(payload);
        if (!parsed.IsValid)
        {
            return BadRequest(new ApiError(parsed.Error!));
        }

        var currentOptions = runtimeDatabaseSettings.GetSnapshot();
        var nextOptions = parsed.Options!;
        var createSchema = payload.CreateSchema ?? true;
        var seedDefaults = payload.SeedDefaults ?? true;
        var shouldMigrateData = RequiresDataMigration(currentOptions, nextOptions);

        try
        {
            await VerifyDatabaseConnectionAsync(nextOptions, createSchema, seedDefaults);
            if (shouldMigrateData)
            {
                var snapshot = await DatabaseSnapshotTransfer.CaptureAsync(dbContext);
                await MigrateSnapshotToTargetAsync(snapshot, nextOptions);
            }

            await appSettingsDatabaseConfigWriter.PersistAsync(nextOptions);
        }
        catch (Exception ex)
        {
            return BadRequest(new ApiError($"Ошибка переключения базы данных: {ex.Message}"));
        }

        runtimeDatabaseSettings.Set(nextOptions);
        return Ok(BuildDatabaseConfigResponse(nextOptions));
    }

    [HttpPost("database/test")]
    public async Task<ActionResult<DatabaseConfigTestResponse>> TestDatabaseConfig([FromBody] DatabaseConfigUpdateRequest payload)
    {
        var authError = RequireAnyRole(AppRoles.Admin);
        if (authError is not null)
        {
            return authError;
        }

        var parsed = ParseDatabasePayload(payload);
        if (!parsed.IsValid)
        {
            return BadRequest(new ApiError(parsed.Error!));
        }

        var nextOptions = parsed.Options!;
        var createSchema = payload.CreateSchema ?? false;
        var seedDefaults = payload.SeedDefaults ?? false;

        try
        {
            await VerifyDatabaseConnectionAsync(nextOptions, createSchema, seedDefaults);
        }
        catch (Exception ex)
        {
            return BadRequest(new ApiError($"Ошибка подключения к базе данных: {ex.Message}"));
        }

        return Ok(new DatabaseConfigTestResponse
        {
            Success = true,
            Message = "Подключение успешно. Параметры корректны.",
        });
    }

    private async Task VerifyDatabaseConnectionAsync(DatabaseOptions options, bool createSchema, bool seedDefaults)
    {
        var isJsonProvider = JsonDatabaseFileStorage.IsJsonProvider(options);
        if (isJsonProvider &&
            !JsonDatabaseFileStorage.TryResolveFilePath(options, out _, out var jsonPathError))
        {
            throw new InvalidOperationException(jsonPathError);
        }

        var optionsBuilder = new DbContextOptionsBuilder<MozaikaDbContext>();
        databaseProviderRegistry.Configure(optionsBuilder, options);
        if (options.Echo)
        {
            optionsBuilder.EnableSensitiveDataLogging();
        }

        await using var testContext = new MozaikaDbContext(optionsBuilder.Options);
        if (createSchema)
        {
            if (isJsonProvider)
            {
                await JsonDatabaseFileStorage.ImportAsync(testContext, options);
            }

            await DbInitializer.SeedAsync(testContext, authOptionsAccessor.Value, seedDefaults: seedDefaults);

            if (isJsonProvider)
            {
                await JsonDatabaseFileStorage.ExportAsync(testContext, options);
            }

            return;
        }

        if (isJsonProvider)
        {
            // If file exists it must be readable and importable.
            await JsonDatabaseFileStorage.ImportAsync(testContext, options);
            return;
        }

        var connected = await testContext.Database.CanConnectAsync();
        if (!connected)
        {
            throw new InvalidOperationException("Не удалось подключиться к базе данных.");
        }
    }

    private (bool IsValid, DatabaseOptions? Options, string? Error) ParseDatabasePayload(DatabaseConfigUpdateRequest payload)
    {
        var provider = (payload.Provider ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(provider))
        {
            return (false, null, "Выберите провайдер базы данных.");
        }

        if (!databaseProviderRegistry.IsSupported(provider))
        {
            var available = string.Join(", ", databaseProviderRegistry.GetSupportedProviderNames());
            return (false, null, $"Провайдер '{provider}' не поддерживается. Доступно: {available}.");
        }

        var connectionString = (payload.ConnectionString ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return (false, null, "Строка подключения обязательна.");
        }

        return (true, new DatabaseOptions
        {
            Provider = provider,
            ConnectionString = connectionString,
            Echo = payload.Echo ?? false,
        }, null);
    }

    private async Task MigrateSnapshotToTargetAsync(DatabaseSnapshot snapshot, DatabaseOptions targetOptions)
    {
        var optionsBuilder = new DbContextOptionsBuilder<MozaikaDbContext>();
        databaseProviderRegistry.Configure(optionsBuilder, targetOptions);
        if (targetOptions.Echo)
        {
            optionsBuilder.EnableSensitiveDataLogging();
        }

        await using var targetContext = new MozaikaDbContext(optionsBuilder.Options);
        await DbInitializer.SeedAsync(targetContext, authOptionsAccessor.Value, seedDefaults: false);
        await DatabaseSnapshotTransfer.ApplyAsync(targetContext, snapshot);

        if (JsonDatabaseFileStorage.IsJsonProvider(targetOptions))
        {
            await JsonDatabaseFileStorage.ExportAsync(targetContext, targetOptions);
        }
    }

    private static bool RequiresDataMigration(DatabaseOptions current, DatabaseOptions next)
    {
        var providerChanged = !string.Equals(
            current.Provider?.Trim(),
            next.Provider?.Trim(),
            StringComparison.OrdinalIgnoreCase
        );

        var connectionChanged = !string.Equals(
            current.ConnectionString?.Trim(),
            next.ConnectionString?.Trim(),
            StringComparison.Ordinal
        );

        return providerChanged || connectionChanged;
    }

    private static (bool IsValid, string[]? Origins, string? Error) ParseCorsOrigins(IEnumerable<string> items)
    {
        var raw = (items ?? [])
            .Select(item => item.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToArray();

        if (raw.Length == 0)
        {
            return (false, null, "Укажите хотя бы один origin для CORS.");
        }

        if (raw.Any(item => item == "*"))
        {
            return (true, ["*"], null);
        }

        var normalized = new List<string>(raw.Length);
        foreach (var candidate in raw)
        {
            if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return (false, null, $"Некорректный origin '{candidate}'. Используйте формат https://domain.tld[:port].");
            }

            normalized.Add($"{uri.Scheme.ToLowerInvariant()}://{uri.Authority.ToLowerInvariant()}");
        }

        return (true, RuntimeCorsOriginsStore.NormalizeOrigins(normalized), null);
    }

    [HttpGet("grout-colors")]
    public async Task<ActionResult<List<GroutColorReadResponse>>> ListGroutColors(
        [FromQuery(Name = "include_inactive")] bool includeInactive = false
    )
    {
        var authError = RequireAnyRole(AppRoles.Admin);
        if (authError is not null)
        {
            return authError;
        }

        var query = dbContext.GroutColors
            .AsNoTracking()
            .OrderBy(item => item.Id)
            .AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(item => item.IsActive);
        }

        var payload = await query
            .Select(item => item.ToRead())
            .ToListAsync();

        return Ok(payload);
    }

    [HttpPost("grout-colors")]
    public async Task<ActionResult<GroutColorReadResponse>> CreateGroutColor([FromBody] GroutColorCreateRequest payload)
    {
        var authError = RequireAnyRole(AppRoles.Admin);
        if (authError is not null)
        {
            return authError;
        }

        var parsed = ValidateCreatePayload(payload);
        if (!parsed.IsValid)
        {
            return BadRequest(new ApiError(parsed.Error!));
        }

        var duplicateExists = await dbContext.GroutColors.AnyAsync(item =>
            item.Name == parsed.Name ||
            item.RgbHex == parsed.RgbHex
        );

        if (duplicateExists)
        {
            return Conflict(new ApiError("Цвет заполнения с таким названием или HEX уже существует."));
        }

        var entity = new GroutColorEntity
        {
            Name = parsed.Name!,
            RgbHex = parsed.RgbHex!,
            IsActive = parsed.IsActive,
            CreatedAt = DateTime.UtcNow,
        };

        dbContext.GroutColors.Add(entity);
        await dbContext.SaveChangesAsync();

        return StatusCode(StatusCodes.Status201Created, entity.ToRead());
    }

    [HttpPatch("grout-colors/{groutColorId:int}")]
    public async Task<ActionResult<GroutColorReadResponse>> UpdateGroutColor(
        int groutColorId,
        [FromBody] GroutColorUpdateRequest payload
    )
    {
        var authError = RequireAnyRole(AppRoles.Admin);
        if (authError is not null)
        {
            return authError;
        }

        if (groutColorId <= 0)
        {
            return BadRequest(new ApiError("Некорректный id цвета заполнения."));
        }

        if (!payload.HasAnyValue())
        {
            return BadRequest(new ApiError("Нужно передать хотя бы одно поле для обновления."));
        }

        var entity = await dbContext.GroutColors.FirstOrDefaultAsync(item => item.Id == groutColorId);
        if (entity is null)
        {
            return NotFound(new ApiError("Цвет заполнения не найден."));
        }

        var nextName = entity.Name;
        var nextHex = entity.RgbHex;
        var nextIsActive = entity.IsActive;

        if (payload.Name is not null)
        {
            nextName = payload.Name.Trim();
            if (string.IsNullOrWhiteSpace(nextName))
            {
                return BadRequest(new ApiError("Название цвета заполнения обязательно."));
            }
        }

        if (payload.RgbHex is not null)
        {
            try
            {
                nextHex = ColorMath.NormalizeHex(payload.RgbHex);
            }
            catch (ArgumentException ex)
            {
                return UnprocessableEntity(new ApiError(ex.Message));
            }
        }

        if (payload.IsActive is not null)
        {
            nextIsActive = payload.IsActive.Value;
        }

        var duplicateExists = await dbContext.GroutColors.AnyAsync(item =>
            item.Id != entity.Id &&
            (item.Name == nextName || item.RgbHex == nextHex)
        );

        if (duplicateExists)
        {
            return Conflict(new ApiError("Цвет заполнения с таким названием или HEX уже существует."));
        }

        entity.Name = nextName;
        entity.RgbHex = nextHex;
        entity.IsActive = nextIsActive;

        await dbContext.SaveChangesAsync();
        return Ok(entity.ToRead());
    }

    [HttpDelete("grout-colors/{groutColorId:int}")]
    public async Task<ActionResult<GroutColorReadResponse>> DeactivateGroutColor(int groutColorId)
    {
        var authError = RequireAnyRole(AppRoles.Admin);
        if (authError is not null)
        {
            return authError;
        }

        if (groutColorId <= 0)
        {
            return BadRequest(new ApiError("Некорректный id цвета заполнения."));
        }

        var entity = await dbContext.GroutColors.FirstOrDefaultAsync(item => item.Id == groutColorId);
        if (entity is null)
        {
            return NotFound(new ApiError("Цвет заполнения не найден."));
        }

        entity.IsActive = false;
        await dbContext.SaveChangesAsync();

        return Ok(entity.ToRead());
    }

    private DatabaseConfigReadResponse BuildDatabaseConfigResponse(DatabaseOptions options) => new()
    {
        Provider = options.Provider,
        ConnectionString = options.ConnectionString,
        Echo = options.Echo,
        SupportedProviders = databaseProviderRegistry.GetSupportedProviderNames().ToList(),
    };

    private static (bool IsValid, string? Name, string? RgbHex, bool IsActive, string? Error) ValidateCreatePayload(GroutColorCreateRequest payload)
    {
        var name = payload.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return (false, null, null, false, "Название цвета заполнения обязательно.");
        }

        string rgbHex;
        try
        {
            rgbHex = ColorMath.NormalizeHex(payload.RgbHex);
        }
        catch (ArgumentException ex)
        {
            return (false, null, null, false, ex.Message);
        }

        return (true, name, rgbHex, payload.IsActive ?? true, null);
    }
}
