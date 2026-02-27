using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mozaika.Api.Database.Entities;
using Mozaika.Api.Options;

namespace Mozaika.Api.Database.Json;

public static class JsonDatabaseFileStorage
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };

    public static bool IsJsonProvider(DatabaseOptions options) =>
        string.Equals(options.Provider?.Trim(), "json", StringComparison.OrdinalIgnoreCase);

    public static bool TryResolveFilePath(DatabaseOptions options, out string filePath, out string error)
    {
        filePath = string.Empty;
        error = string.Empty;

        if (!IsJsonProvider(options))
        {
            error = "The selected provider is not JSON.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            error = "Connection string for JSON provider is empty.";
            return false;
        }

        if (!TryExtractPath(options.ConnectionString, out var extractedPath))
        {
            error = "Unable to resolve JSON file path from connection string.";
            return false;
        }

        var normalized = extractedPath.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            error = "JSON file path is empty.";
            return false;
        }

        filePath = Path.IsPathRooted(normalized)
            ? Path.GetFullPath(normalized)
            : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), normalized));

        return true;
    }

    public static async Task ImportAsync(MozaikaDbContext dbContext, DatabaseOptions options, CancellationToken cancellationToken = default)
    {
        if (!IsJsonProvider(options))
        {
            return;
        }

        if (!TryResolveFilePath(options, out var filePath, out var error))
        {
            throw new InvalidOperationException(error);
        }

        if (!File.Exists(filePath))
        {
            return;
        }

        await using var stream = File.OpenRead(filePath);
        var snapshot = await JsonSerializer.DeserializeAsync<DatabaseSnapshot>(stream, SerializerOptions, cancellationToken);
        if (snapshot is null)
        {
            return;
        }

        await ReplaceDataAsync(dbContext, snapshot, cancellationToken);
    }

    public static async Task ExportAsync(MozaikaDbContext dbContext, DatabaseOptions options, CancellationToken cancellationToken = default)
    {
        if (!IsJsonProvider(options))
        {
            return;
        }

        if (!TryResolveFilePath(options, out var filePath, out var error))
        {
            throw new InvalidOperationException(error);
        }

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var snapshot = await BuildSnapshotAsync(dbContext, cancellationToken);
        var tempPath = $"{filePath}.{Guid.NewGuid():N}.tmp";

        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, snapshot, SerializerOptions, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        File.Move(tempPath, filePath, overwrite: true);
    }

    private static bool TryExtractPath(string connectionString, out string path)
    {
        path = string.Empty;
        var raw = connectionString.Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        if (!raw.Contains('='))
        {
            path = raw;
            return true;
        }

        var supportedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "data source",
            "datasource",
            "file",
            "filename",
            "path",
            "json",
            "jsonfile",
        };

        var segments = raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var segment in segments)
        {
            var index = segment.IndexOf('=');
            if (index <= 0)
            {
                continue;
            }

            var key = segment[..index].Trim();
            var value = segment[(index + 1)..].Trim();
            if (supportedKeys.Contains(key))
            {
                path = value;
                return true;
            }
        }

        return false;
    }

    private static async Task ReplaceDataAsync(MozaikaDbContext dbContext, DatabaseSnapshot snapshot, CancellationToken cancellationToken)
    {
        dbContext.ProjectOrderStatusHistory.RemoveRange(await dbContext.ProjectOrderStatusHistory.ToListAsync(cancellationToken));
        dbContext.ProjectOrders.RemoveRange(await dbContext.ProjectOrders.ToListAsync(cancellationToken));
        dbContext.ProjectShares.RemoveRange(await dbContext.ProjectShares.ToListAsync(cancellationToken));
        dbContext.ProjectGenerations.RemoveRange(await dbContext.ProjectGenerations.ToListAsync(cancellationToken));
        dbContext.Projects.RemoveRange(await dbContext.Projects.ToListAsync(cancellationToken));
        dbContext.AuthSessions.RemoveRange(await dbContext.AuthSessions.ToListAsync(cancellationToken));
        dbContext.Users.RemoveRange(await dbContext.Users.ToListAsync(cancellationToken));
        dbContext.Colors.RemoveRange(await dbContext.Colors.ToListAsync(cancellationToken));
        dbContext.GroutColors.RemoveRange(await dbContext.GroutColors.ToListAsync(cancellationToken));
        dbContext.AppSettings.RemoveRange(await dbContext.AppSettings.ToListAsync(cancellationToken));
        await dbContext.SaveChangesAsync(cancellationToken);

        if (snapshot.AppSettings.Count > 0)
        {
            dbContext.AppSettings.AddRange(snapshot.AppSettings.Select(item => new AppSettingEntity
            {
                Id = item.Id,
                DefaultFieldWidthMm = item.DefaultFieldWidthMm,
                DefaultFieldHeightMm = item.DefaultFieldHeightMm,
                DefaultCellSizeMm = item.DefaultCellSizeMm,
                DefaultGapMm = item.DefaultGapMm,
                CorsOriginsJson = string.IsNullOrWhiteSpace(item.CorsOriginsJson) ? "[\"*\"]" : item.CorsOriginsJson,
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt,
            }));
        }

        if (snapshot.Colors.Count > 0)
        {
            dbContext.Colors.AddRange(snapshot.Colors.Select(item => new ColorEntity
            {
                Id = item.Id,
                Name = item.Name,
                RalCode = item.RalCode,
                RgbHex = item.RgbHex,
                IsActive = item.IsActive,
                CreatedAt = item.CreatedAt,
            }));
        }

        if (snapshot.GroutColors.Count > 0)
        {
            dbContext.GroutColors.AddRange(snapshot.GroutColors.Select(item => new GroutColorEntity
            {
                Id = item.Id,
                Name = item.Name,
                RgbHex = item.RgbHex,
                IsActive = item.IsActive,
                CreatedAt = item.CreatedAt,
            }));
        }

        if (snapshot.Users.Count > 0)
        {
            dbContext.Users.AddRange(snapshot.Users.Select(item => new UserEntity
            {
                Id = item.Id,
                Username = item.Username,
                DisplayName = item.DisplayName,
                PasswordHash = item.PasswordHash,
                PasswordSalt = item.PasswordSalt,
                PasswordIterations = item.PasswordIterations,
                Role = item.Role,
                IsActive = item.IsActive,
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt,
            }));
        }

        if (snapshot.AuthSessions.Count > 0)
        {
            dbContext.AuthSessions.AddRange(snapshot.AuthSessions.Select(item => new AuthSessionEntity
            {
                Id = item.Id,
                UserId = item.UserId,
                TokenHash = item.TokenHash,
                CreatedAt = item.CreatedAt,
                ExpiresAt = item.ExpiresAt,
                LastSeenAt = item.LastSeenAt,
                IsRevoked = item.IsRevoked,
            }));
        }

        if (snapshot.Projects.Count > 0)
        {
            dbContext.Projects.AddRange(snapshot.Projects.Select(item => new ProjectEntity
            {
                Id = item.Id,
                Name = item.Name,
                Description = item.Description,
                OwnerUsername = item.OwnerUsername,
                SourceImageMimeType = item.SourceImageMimeType,
                SourceImageBytes = item.SourceImageBytes ?? [],
                ActiveGenerationId = item.ActiveGenerationId,
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt,
            }));
        }

        if (snapshot.ProjectGenerations.Count > 0)
        {
            dbContext.ProjectGenerations.AddRange(snapshot.ProjectGenerations.Select(item => new ProjectGenerationEntity
            {
                Id = item.Id,
                ProjectId = item.ProjectId,
                Version = item.Version,
                Name = item.Name,
                Note = item.Note,
                SnapshotJson = item.SnapshotJson,
                CreatedAt = item.CreatedAt,
            }));
        }

        if (snapshot.ProjectShares.Count > 0)
        {
            dbContext.ProjectShares.AddRange(snapshot.ProjectShares.Select(item => new ProjectShareEntity
            {
                Id = item.Id,
                ProjectId = item.ProjectId,
                GenerationId = item.GenerationId,
                GenerationVersion = item.GenerationVersion,
                GenerationName = item.GenerationName,
                Token = item.Token,
                CreatedAt = item.CreatedAt,
                ExpiresAt = item.ExpiresAt,
                IsRevoked = item.IsRevoked,
                CreatedBy = item.CreatedBy,
            }));
        }

        if (snapshot.ProjectOrders.Count > 0)
        {
            dbContext.ProjectOrders.AddRange(snapshot.ProjectOrders.Select(item => new ProjectOrderEntity
            {
                Id = item.Id,
                ProjectId = item.ProjectId,
                GenerationId = item.GenerationId,
                GenerationVersion = item.GenerationVersion,
                GenerationName = item.GenerationName,
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt,
                Status = item.Status,
                StatusComment = item.StatusComment,
                CustomerName = item.CustomerName,
                CustomerEmail = item.CustomerEmail,
                CustomerPhone = item.CustomerPhone,
                Comment = item.Comment,
                SubmittedBy = item.SubmittedBy,
                TotalPrice = item.TotalPrice,
                Currency = item.Currency,
                TotalChips = item.TotalChips,
                ColorsUsed = item.ColorsUsed,
            }));
        }

        if (snapshot.ProjectOrderStatusHistory.Count > 0)
        {
            dbContext.ProjectOrderStatusHistory.AddRange(snapshot.ProjectOrderStatusHistory.Select(item => new ProjectOrderStatusHistoryEntity
            {
                Id = item.Id,
                OrderId = item.OrderId,
                Status = item.Status,
                StatusComment = item.StatusComment,
                ChangedBy = item.ChangedBy,
                CreatedAt = item.CreatedAt,
            }));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task<DatabaseSnapshot> BuildSnapshotAsync(MozaikaDbContext dbContext, CancellationToken cancellationToken)
    {
        var snapshot = new DatabaseSnapshot
        {
            FormatVersion = 1,
            ExportedAtUtc = DateTime.UtcNow,
            AppSettings = await dbContext.AppSettings
                .AsNoTracking()
                .OrderBy(item => item.Id)
                .Select(item => new AppSettingSnapshot
                {
                    Id = item.Id,
                    DefaultFieldWidthMm = item.DefaultFieldWidthMm,
                    DefaultFieldHeightMm = item.DefaultFieldHeightMm,
                    DefaultCellSizeMm = item.DefaultCellSizeMm,
                    DefaultGapMm = item.DefaultGapMm,
                    CorsOriginsJson = item.CorsOriginsJson,
                    CreatedAt = item.CreatedAt,
                    UpdatedAt = item.UpdatedAt,
                })
                .ToListAsync(cancellationToken),
            Colors = await dbContext.Colors
                .AsNoTracking()
                .OrderBy(item => item.Id)
                .Select(item => new ColorSnapshot
                {
                    Id = item.Id,
                    Name = item.Name,
                    RalCode = item.RalCode,
                    RgbHex = item.RgbHex,
                    IsActive = item.IsActive,
                    CreatedAt = item.CreatedAt,
                })
                .ToListAsync(cancellationToken),
            GroutColors = await dbContext.GroutColors
                .AsNoTracking()
                .OrderBy(item => item.Id)
                .Select(item => new GroutColorSnapshot
                {
                    Id = item.Id,
                    Name = item.Name,
                    RgbHex = item.RgbHex,
                    IsActive = item.IsActive,
                    CreatedAt = item.CreatedAt,
                })
                .ToListAsync(cancellationToken),
            Users = await dbContext.Users
                .AsNoTracking()
                .OrderBy(item => item.Id)
                .Select(item => new UserSnapshot
                {
                    Id = item.Id,
                    Username = item.Username,
                    DisplayName = item.DisplayName,
                    PasswordHash = item.PasswordHash,
                    PasswordSalt = item.PasswordSalt,
                    PasswordIterations = item.PasswordIterations,
                    Role = item.Role,
                    IsActive = item.IsActive,
                    CreatedAt = item.CreatedAt,
                    UpdatedAt = item.UpdatedAt,
                })
                .ToListAsync(cancellationToken),
            AuthSessions = await dbContext.AuthSessions
                .AsNoTracking()
                .OrderBy(item => item.Id)
                .Select(item => new AuthSessionSnapshot
                {
                    Id = item.Id,
                    UserId = item.UserId,
                    TokenHash = item.TokenHash,
                    CreatedAt = item.CreatedAt,
                    ExpiresAt = item.ExpiresAt,
                    LastSeenAt = item.LastSeenAt,
                    IsRevoked = item.IsRevoked,
                })
                .ToListAsync(cancellationToken),
            Projects = await dbContext.Projects
                .AsNoTracking()
                .OrderBy(item => item.Id)
                .Select(item => new ProjectSnapshot
                {
                    Id = item.Id,
                    Name = item.Name,
                    Description = item.Description,
                    OwnerUsername = item.OwnerUsername,
                    SourceImageMimeType = item.SourceImageMimeType,
                    SourceImageBytes = item.SourceImageBytes,
                    ActiveGenerationId = item.ActiveGenerationId,
                    CreatedAt = item.CreatedAt,
                    UpdatedAt = item.UpdatedAt,
                })
                .ToListAsync(cancellationToken),
            ProjectGenerations = await dbContext.ProjectGenerations
                .AsNoTracking()
                .OrderBy(item => item.Id)
                .Select(item => new ProjectGenerationSnapshot
                {
                    Id = item.Id,
                    ProjectId = item.ProjectId,
                    Version = item.Version,
                    Name = item.Name,
                    Note = item.Note,
                    SnapshotJson = item.SnapshotJson,
                    CreatedAt = item.CreatedAt,
                })
                .ToListAsync(cancellationToken),
            ProjectShares = await dbContext.ProjectShares
                .AsNoTracking()
                .OrderBy(item => item.Id)
                .Select(item => new ProjectShareSnapshot
                {
                    Id = item.Id,
                    ProjectId = item.ProjectId,
                    GenerationId = item.GenerationId,
                    GenerationVersion = item.GenerationVersion,
                    GenerationName = item.GenerationName,
                    Token = item.Token,
                    CreatedAt = item.CreatedAt,
                    ExpiresAt = item.ExpiresAt,
                    IsRevoked = item.IsRevoked,
                    CreatedBy = item.CreatedBy,
                })
                .ToListAsync(cancellationToken),
            ProjectOrders = await dbContext.ProjectOrders
                .AsNoTracking()
                .OrderBy(item => item.Id)
                .Select(item => new ProjectOrderSnapshot
                {
                    Id = item.Id,
                    ProjectId = item.ProjectId,
                    GenerationId = item.GenerationId,
                    GenerationVersion = item.GenerationVersion,
                    GenerationName = item.GenerationName,
                    CreatedAt = item.CreatedAt,
                    UpdatedAt = item.UpdatedAt,
                    Status = item.Status,
                    StatusComment = item.StatusComment,
                    CustomerName = item.CustomerName,
                    CustomerEmail = item.CustomerEmail,
                    CustomerPhone = item.CustomerPhone,
                    Comment = item.Comment,
                    SubmittedBy = item.SubmittedBy,
                    TotalPrice = item.TotalPrice,
                    Currency = item.Currency,
                    TotalChips = item.TotalChips,
                    ColorsUsed = item.ColorsUsed,
                })
                .ToListAsync(cancellationToken),
            ProjectOrderStatusHistory = await dbContext.ProjectOrderStatusHistory
                .AsNoTracking()
                .OrderBy(item => item.Id)
                .Select(item => new ProjectOrderStatusHistorySnapshot
                {
                    Id = item.Id,
                    OrderId = item.OrderId,
                    Status = item.Status,
                    StatusComment = item.StatusComment,
                    ChangedBy = item.ChangedBy,
                    CreatedAt = item.CreatedAt,
                })
                .ToListAsync(cancellationToken),
        };

        return snapshot;
    }

    private sealed class DatabaseSnapshot
    {
        public int FormatVersion { get; set; }
        public DateTime ExportedAtUtc { get; set; }
        public List<AppSettingSnapshot> AppSettings { get; set; } = [];
        public List<ColorSnapshot> Colors { get; set; } = [];
        public List<GroutColorSnapshot> GroutColors { get; set; } = [];
        public List<UserSnapshot> Users { get; set; } = [];
        public List<AuthSessionSnapshot> AuthSessions { get; set; } = [];
        public List<ProjectSnapshot> Projects { get; set; } = [];
        public List<ProjectGenerationSnapshot> ProjectGenerations { get; set; } = [];
        public List<ProjectShareSnapshot> ProjectShares { get; set; } = [];
        public List<ProjectOrderSnapshot> ProjectOrders { get; set; } = [];
        public List<ProjectOrderStatusHistorySnapshot> ProjectOrderStatusHistory { get; set; } = [];
    }

    private sealed class AppSettingSnapshot
    {
        public int Id { get; set; }
        public double DefaultFieldWidthMm { get; set; }
        public double DefaultFieldHeightMm { get; set; }
        public double DefaultCellSizeMm { get; set; }
        public double DefaultGapMm { get; set; }
        public string CorsOriginsJson { get; set; } = "[\"*\"]";
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    private sealed class ColorSnapshot
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string RalCode { get; set; } = string.Empty;
        public string RgbHex { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    private sealed class GroutColorSnapshot
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string RgbHex { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    private sealed class UserSnapshot
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string PasswordSalt { get; set; } = string.Empty;
        public int PasswordIterations { get; set; }
        public string Role { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    private sealed class AuthSessionSnapshot
    {
        public long Id { get; set; }
        public int UserId { get; set; }
        public string TokenHash { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime LastSeenAt { get; set; }
        public bool IsRevoked { get; set; }
    }

    private sealed class ProjectSnapshot
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? OwnerUsername { get; set; }
        public string SourceImageMimeType { get; set; } = string.Empty;
        public byte[]? SourceImageBytes { get; set; }
        public int? ActiveGenerationId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    private sealed class ProjectGenerationSnapshot
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public int Version { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public string SnapshotJson { get; set; } = "{}";
        public DateTime CreatedAt { get; set; }
    }

    private sealed class ProjectShareSnapshot
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public int GenerationId { get; set; }
        public int GenerationVersion { get; set; }
        public string GenerationName { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public bool IsRevoked { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
    }

    private sealed class ProjectOrderSnapshot
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public int GenerationId { get; set; }
        public int GenerationVersion { get; set; }
        public string GenerationName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public string StatusComment { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
        public string SubmittedBy { get; set; } = string.Empty;
        public double TotalPrice { get; set; }
        public string Currency { get; set; } = "RUB";
        public int TotalChips { get; set; }
        public int ColorsUsed { get; set; }
    }

    private sealed class ProjectOrderStatusHistorySnapshot
    {
        public long Id { get; set; }
        public int OrderId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string StatusComment { get; set; } = string.Empty;
        public string ChangedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
