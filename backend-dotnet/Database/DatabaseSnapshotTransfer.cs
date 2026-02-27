using Microsoft.EntityFrameworkCore;
using Mozaika.Api.Database.Entities;

namespace Mozaika.Api.Database;

public sealed class DatabaseSnapshot
{
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

public sealed class AppSettingSnapshot
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

public sealed class ColorSnapshot
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string RalCode { get; set; } = string.Empty;
    public string RgbHex { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class GroutColorSnapshot
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string RgbHex { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class UserSnapshot
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

public sealed class AuthSessionSnapshot
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public bool IsRevoked { get; set; }
}

public sealed class ProjectSnapshot
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? OwnerUsername { get; set; }
    public string SourceImageMimeType { get; set; } = string.Empty;
    public byte[] SourceImageBytes { get; set; } = [];
    public int? ActiveGenerationId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class ProjectGenerationSnapshot
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public int Version { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public string SnapshotJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
}

public sealed class ProjectShareSnapshot
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

public sealed class ProjectOrderSnapshot
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

public sealed class ProjectOrderStatusHistorySnapshot
{
    public long Id { get; set; }
    public int OrderId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string StatusComment { get; set; } = string.Empty;
    public string ChangedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public static class DatabaseSnapshotTransfer
{
    public static async Task<DatabaseSnapshot> CaptureAsync(
        MozaikaDbContext dbContext,
        CancellationToken cancellationToken = default
    )
    {
        return new DatabaseSnapshot
        {
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
    }

    public static async Task ApplyAsync(
        MozaikaDbContext dbContext,
        DatabaseSnapshot snapshot,
        CancellationToken cancellationToken = default
    )
    {
        await ClearAsync(dbContext, cancellationToken);

        if (snapshot.AppSettings.Count > 0)
        {
            dbContext.AppSettings.AddRange(snapshot.AppSettings.Select(item => new AppSettingEntity
            {
                Id = item.Id,
                DefaultFieldWidthMm = item.DefaultFieldWidthMm,
                DefaultFieldHeightMm = item.DefaultFieldHeightMm,
                DefaultCellSizeMm = item.DefaultCellSizeMm,
                DefaultGapMm = item.DefaultGapMm,
                CorsOriginsJson = item.CorsOriginsJson,
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt,
            }));
        }

        if (snapshot.Colors.Count > 0)
        {
            dbContext.Colors.AddRange(snapshot.Colors.Select(item => new ColorEntity
            {
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
                Name = item.Name,
                RgbHex = item.RgbHex,
                IsActive = item.IsActive,
                CreatedAt = item.CreatedAt,
            }));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        dbContext.ChangeTracker.Clear();

        var userRows = snapshot.Users
            .OrderBy(item => item.Id)
            .Select(item => new
            {
                SourceId = item.Id,
                Entity = new UserEntity
                {
                    Username = item.Username,
                    DisplayName = item.DisplayName,
                    PasswordHash = item.PasswordHash,
                    PasswordSalt = item.PasswordSalt,
                    PasswordIterations = item.PasswordIterations,
                    Role = item.Role,
                    IsActive = item.IsActive,
                    CreatedAt = item.CreatedAt,
                    UpdatedAt = item.UpdatedAt,
                },
            })
            .ToList();

        if (userRows.Count > 0)
        {
            dbContext.Users.AddRange(userRows.Select(item => item.Entity));
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var userIdMap = userRows.ToDictionary(item => item.SourceId, item => item.Entity.Id);

        var authSessionRows = snapshot.AuthSessions
            .OrderBy(item => item.Id)
            .Select(item => new AuthSessionEntity
            {
                UserId = MapRequired(userIdMap, item.UserId, nameof(AuthSessionEntity)),
                TokenHash = item.TokenHash,
                CreatedAt = item.CreatedAt,
                ExpiresAt = item.ExpiresAt,
                LastSeenAt = item.LastSeenAt,
                IsRevoked = item.IsRevoked,
            })
            .ToList();

        if (authSessionRows.Count > 0)
        {
            dbContext.AuthSessions.AddRange(authSessionRows);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var projectRows = snapshot.Projects
            .OrderBy(item => item.Id)
            .Select(item => new
            {
                SourceId = item.Id,
                SourceActiveGenerationId = item.ActiveGenerationId,
                Entity = new ProjectEntity
                {
                    Name = item.Name,
                    Description = item.Description,
                    OwnerUsername = item.OwnerUsername,
                    SourceImageMimeType = item.SourceImageMimeType,
                    SourceImageBytes = item.SourceImageBytes?.ToArray() ?? [],
                    ActiveGenerationId = null,
                    CreatedAt = item.CreatedAt,
                    UpdatedAt = item.UpdatedAt,
                },
            })
            .ToList();

        if (projectRows.Count > 0)
        {
            dbContext.Projects.AddRange(projectRows.Select(item => item.Entity));
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var projectIdMap = projectRows.ToDictionary(item => item.SourceId, item => item.Entity.Id);

        var generationRows = snapshot.ProjectGenerations
            .OrderBy(item => item.Id)
            .Select(item => new
            {
                SourceId = item.Id,
                Entity = new ProjectGenerationEntity
                {
                    ProjectId = MapRequired(projectIdMap, item.ProjectId, nameof(ProjectGenerationEntity)),
                    Version = item.Version,
                    Name = item.Name,
                    Note = item.Note,
                    SnapshotJson = item.SnapshotJson,
                    CreatedAt = item.CreatedAt,
                },
            })
            .ToList();

        if (generationRows.Count > 0)
        {
            dbContext.ProjectGenerations.AddRange(generationRows.Select(item => item.Entity));
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var generationIdMap = generationRows.ToDictionary(item => item.SourceId, item => item.Entity.Id);

        foreach (var project in projectRows)
        {
            if (!project.SourceActiveGenerationId.HasValue)
            {
                continue;
            }

            if (!generationIdMap.TryGetValue(project.SourceActiveGenerationId.Value, out var mappedGenerationId))
            {
                continue;
            }

            project.Entity.ActiveGenerationId = mappedGenerationId;
        }

        if (projectRows.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var shareRows = snapshot.ProjectShares
            .OrderBy(item => item.Id)
            .Select(item => new ProjectShareEntity
            {
                ProjectId = MapRequired(projectIdMap, item.ProjectId, nameof(ProjectShareEntity)),
                GenerationId = MapRequired(generationIdMap, item.GenerationId, nameof(ProjectShareEntity)),
                GenerationVersion = item.GenerationVersion,
                GenerationName = item.GenerationName,
                Token = item.Token,
                CreatedAt = item.CreatedAt,
                ExpiresAt = item.ExpiresAt,
                IsRevoked = item.IsRevoked,
                CreatedBy = item.CreatedBy,
            })
            .ToList();

        if (shareRows.Count > 0)
        {
            dbContext.ProjectShares.AddRange(shareRows);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var orderRows = snapshot.ProjectOrders
            .OrderBy(item => item.Id)
            .Select(item => new
            {
                SourceId = item.Id,
                Entity = new ProjectOrderEntity
                {
                    ProjectId = MapRequired(projectIdMap, item.ProjectId, nameof(ProjectOrderEntity)),
                    GenerationId = MapRequired(generationIdMap, item.GenerationId, nameof(ProjectOrderEntity)),
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
                },
            })
            .ToList();

        if (orderRows.Count > 0)
        {
            dbContext.ProjectOrders.AddRange(orderRows.Select(item => item.Entity));
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var orderIdMap = orderRows.ToDictionary(item => item.SourceId, item => item.Entity.Id);

        var statusRows = snapshot.ProjectOrderStatusHistory
            .OrderBy(item => item.Id)
            .Select(item => new ProjectOrderStatusHistoryEntity
            {
                OrderId = MapRequired(orderIdMap, item.OrderId, nameof(ProjectOrderStatusHistoryEntity)),
                Status = item.Status,
                StatusComment = item.StatusComment,
                ChangedBy = item.ChangedBy,
                CreatedAt = item.CreatedAt,
            })
            .ToList();

        if (statusRows.Count > 0)
        {
            dbContext.ProjectOrderStatusHistory.AddRange(statusRows);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static int MapRequired(
        IReadOnlyDictionary<int, int> idMap,
        int sourceId,
        string entityName
    )
    {
        if (idMap.TryGetValue(sourceId, out var mapped))
        {
            return mapped;
        }

        throw new InvalidOperationException($"Cannot map {entityName} dependency with source id '{sourceId}'.");
    }

    private static async Task ClearAsync(MozaikaDbContext dbContext, CancellationToken cancellationToken)
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
        dbContext.ChangeTracker.Clear();
    }
}
