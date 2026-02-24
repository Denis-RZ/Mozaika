using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mozaika.Api.Contracts;
using Mozaika.Api.Database;
using Mozaika.Api.Database.Entities;

namespace Mozaika.Api.Services;

public sealed class ProjectStorageException : Exception
{
    public int StatusCode { get; }

    public ProjectStorageException(string message, int statusCode = StatusCodes.Status400BadRequest)
        : base(message)
    {
        StatusCode = statusCode;
    }
}

public sealed class ProjectStorageService(MozaikaDbContext dbContext)
{
    private const int MaxSourceImageBytes = 25 * 1024 * 1024;
    private const int MaxSnapshotJsonBytes = 40 * 1024 * 1024;
    private const int MaxPreviewPngBytes = 12 * 1024 * 1024;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public async Task<List<ProjectListItemResponse>> ListProjectsAsync()
    {
        var generationStats = dbContext.ProjectGenerations
            .AsNoTracking()
            .GroupBy(item => item.ProjectId)
            .Select(group => new
            {
                ProjectId = group.Key,
                GenerationsCount = group.Count(),
                LastGenerationAt = group.Max(item => item.CreatedAt),
            });

        return await (
            from project in dbContext.Projects.AsNoTracking()
            join stats in generationStats on project.Id equals stats.ProjectId into statsJoin
            from stats in statsJoin.DefaultIfEmpty()
            orderby project.UpdatedAt descending
            select new ProjectListItemResponse
            {
                Id = project.Id,
                Name = project.Name,
                Description = project.Description,
                CreatedAt = project.CreatedAt,
                UpdatedAt = project.UpdatedAt,
                GenerationsCount = stats == null ? 0 : stats.GenerationsCount,
                ActiveGenerationId = project.ActiveGenerationId,
                LastGenerationAt = stats == null ? null : stats.LastGenerationAt,
            }
        ).ToListAsync();
    }

    public async Task<ProjectReadResponse> CreateProjectAsync(ProjectCreateRequest request)
    {
        var name = request.Name.Trim();
        if (name.Length < 2)
        {
            throw new ProjectStorageException("Название проекта должно быть не короче 2 символов.");
        }

        var description = request.Description.Trim();
        var normalizedImage = NormalizeSourceImage(request.SourceImageBase64, request.SourceImageMimeType);
        var generation = NormalizeGenerationRequest(request.InitialGeneration, fallbackName: "Базовая генерация");
        var utcNow = DateTime.UtcNow;

        await using var tx = await dbContext.Database.BeginTransactionAsync();

        var project = new ProjectEntity
        {
            Name = name,
            Description = description,
            SourceImageMimeType = normalizedImage.MimeType,
            SourceImageBytes = normalizedImage.Bytes,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        };
        dbContext.Projects.Add(project);
        await dbContext.SaveChangesAsync();

        var generationEntity = new ProjectGenerationEntity
        {
            ProjectId = project.Id,
            Version = 1,
            Name = generation.Name,
            Note = generation.Note,
            SnapshotJson = SerializeSnapshot(generation.Snapshot),
            CreatedAt = utcNow,
        };
        dbContext.ProjectGenerations.Add(generationEntity);
        await dbContext.SaveChangesAsync();

        project.ActiveGenerationId = generationEntity.Id;
        project.UpdatedAt = utcNow;
        await dbContext.SaveChangesAsync();

        await tx.CommitAsync();
        return await GetProjectAsync(project.Id);
    }

    public async Task<ProjectReadResponse> GetProjectAsync(int projectId)
    {
        var project = await FindProjectEntityAsync(projectId, asTracking: false);
        var generationSummaries = await dbContext.ProjectGenerations
            .AsNoTracking()
            .Where(item => item.ProjectId == project.Id)
            .OrderByDescending(item => item.Version)
            .Select(item => new ProjectGenerationSummaryResponse
            {
                Id = item.Id,
                Version = item.Version,
                Name = item.Name,
                Note = item.Note,
                CreatedAt = item.CreatedAt,
            })
            .ToListAsync();

        ProjectGenerationReadResponse? activeGeneration = null;
        if (project.ActiveGenerationId is not null)
        {
            activeGeneration = await LoadGenerationAsync(project.Id, project.ActiveGenerationId.Value);
        }

        return BuildProjectReadResponse(project, generationSummaries, activeGeneration);
    }

    public async Task<ProjectReadResponse> UpdateProjectAsync(int projectId, ProjectUpdateRequest request)
    {
        var project = await FindProjectEntityAsync(projectId, asTracking: true);
        if (request.Name is not null)
        {
            var name = request.Name.Trim();
            if (name.Length < 2)
            {
                throw new ProjectStorageException("Название проекта должно быть не короче 2 символов.");
            }
            project.Name = name;
        }

        if (request.Description is not null)
        {
            project.Description = request.Description.Trim();
        }

        project.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();
        return await GetProjectAsync(project.Id);
    }

    public async Task<ProjectGenerationReadResponse> SaveGenerationAsync(
        int projectId,
        ProjectSaveGenerationRequest request,
        bool setAsActive = true
    )
    {
        var project = await FindProjectEntityAsync(projectId, asTracking: true);
        var normalized = NormalizeGenerationRequest(request, fallbackName: "Новая версия");
        var utcNow = DateTime.UtcNow;

        var maxVersion = await dbContext.ProjectGenerations
            .AsNoTracking()
            .Where(item => item.ProjectId == project.Id)
            .Select(item => (int?)item.Version)
            .MaxAsync();

        var generationEntity = new ProjectGenerationEntity
        {
            ProjectId = project.Id,
            Version = (maxVersion ?? 0) + 1,
            Name = normalized.Name,
            Note = normalized.Note,
            SnapshotJson = SerializeSnapshot(normalized.Snapshot),
            CreatedAt = utcNow,
        };

        dbContext.ProjectGenerations.Add(generationEntity);
        try
        {
            await dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsDuplicateGenerationVersion(ex))
        {
            throw new ProjectStorageException(
                "Не удалось сохранить версию проекта: конкурентное изменение, повторите попытку.",
                StatusCodes.Status409Conflict
            );
        }

        if (setAsActive)
        {
            project.ActiveGenerationId = generationEntity.Id;
        }
        project.UpdatedAt = utcNow;
        await dbContext.SaveChangesAsync();

        return MapGenerationRead(generationEntity, normalized.Snapshot);
    }

    public async Task<ProjectGenerationReadResponse> GetGenerationAsync(int projectId, int generationId)
    {
        if (projectId <= 0)
        {
            throw new ProjectStorageException("Некорректный id проекта.");
        }
        if (generationId <= 0)
        {
            throw new ProjectStorageException("Некорректный id генерации.");
        }

        var generation = await dbContext.ProjectGenerations
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.ProjectId == projectId && item.Id == generationId);

        if (generation is null)
        {
            var projectExists = await dbContext.Projects.AnyAsync(item => item.Id == projectId);
            if (!projectExists)
            {
                throw new ProjectStorageException("Проект не найден.", StatusCodes.Status404NotFound);
            }
            throw new ProjectStorageException("Сохраненная генерация не найдена.", StatusCodes.Status404NotFound);
        }

        var snapshot = DeserializeSnapshot(generation.SnapshotJson);
        return MapGenerationRead(generation, snapshot);
    }

    public async Task<ProjectReadResponse> ActivateGenerationAsync(int projectId, int generationId)
    {
        var project = await FindProjectEntityAsync(projectId, asTracking: true);
        var generationExists = await dbContext.ProjectGenerations
            .AsNoTracking()
            .AnyAsync(item => item.ProjectId == project.Id && item.Id == generationId);

        if (!generationExists)
        {
            throw new ProjectStorageException("Сохраненная генерация не найдена.", StatusCodes.Status404NotFound);
        }

        project.ActiveGenerationId = generationId;
        project.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();
        return await GetProjectAsync(projectId);
    }

    private async Task<ProjectEntity> FindProjectEntityAsync(int projectId, bool asTracking)
    {
        if (projectId <= 0)
        {
            throw new ProjectStorageException("Некорректный id проекта.");
        }

        IQueryable<ProjectEntity> query = dbContext.Projects;
        if (!asTracking)
        {
            query = query.AsNoTracking();
        }

        var project = await query.FirstOrDefaultAsync(item => item.Id == projectId);
        if (project is null)
        {
            throw new ProjectStorageException("Проект не найден.", StatusCodes.Status404NotFound);
        }

        return project;
    }

    private async Task<ProjectGenerationReadResponse?> LoadGenerationAsync(int projectId, int generationId)
    {
        var generation = await dbContext.ProjectGenerations
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.ProjectId == projectId && item.Id == generationId);

        if (generation is null)
        {
            return null;
        }

        var snapshot = DeserializeSnapshot(generation.SnapshotJson);
        return MapGenerationRead(generation, snapshot);
    }

    private static ProjectReadResponse BuildProjectReadResponse(
        ProjectEntity project,
        IReadOnlyList<ProjectGenerationSummaryResponse> generationSummaries,
        ProjectGenerationReadResponse? activeGeneration
    )
    {
        var sourceImageBytes = project.SourceImageBytes ?? [];
        return new ProjectReadResponse
        {
            Id = project.Id,
            Name = project.Name,
            Description = project.Description,
            SourceImageMimeType = project.SourceImageMimeType,
            SourceImageBase64 = sourceImageBytes.Length == 0
                ? string.Empty
                : Convert.ToBase64String(sourceImageBytes),
            CreatedAt = project.CreatedAt,
            UpdatedAt = project.UpdatedAt,
            GenerationsCount = generationSummaries.Count,
            ActiveGenerationId = project.ActiveGenerationId,
            LastGenerationAt = generationSummaries.Count == 0 ? null : generationSummaries[0].CreatedAt,
            ActiveGeneration = activeGeneration,
            Generations = generationSummaries.ToList(),
        };
    }

    private static ProjectGenerationReadResponse MapGenerationRead(
        ProjectGenerationEntity generation,
        ProjectSnapshotPayload snapshot
    ) => new()
    {
        Id = generation.Id,
        Version = generation.Version,
        Name = generation.Name,
        Note = generation.Note,
        CreatedAt = generation.CreatedAt,
        Snapshot = snapshot,
    };

    private string SerializeSnapshot(ProjectSnapshotPayload snapshot)
    {
        try
        {
            var json = JsonSerializer.Serialize(snapshot, _jsonOptions);
            var size = Encoding.UTF8.GetByteCount(json);
            if (size > MaxSnapshotJsonBytes)
            {
                throw new ProjectStorageException("Snapshot проекта слишком большой для сохранения.");
            }

            return json;
        }
        catch (ProjectStorageException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ProjectStorageException(
                $"Ошибка сериализации snapshot проекта: {ex.Message}",
                StatusCodes.Status500InternalServerError
            );
        }
    }

    private ProjectSnapshotPayload DeserializeSnapshot(string json)
    {
        try
        {
            var snapshot = JsonSerializer.Deserialize<ProjectSnapshotPayload>(json, _jsonOptions);
            if (snapshot is null)
            {
                throw new ProjectStorageException(
                    "Не удалось прочитать сохраненный snapshot проекта.",
                    StatusCodes.Status500InternalServerError
                );
            }

            ValidateSnapshot(snapshot);
            return snapshot;
        }
        catch (ProjectStorageException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ProjectStorageException(
                $"Ошибка чтения snapshot проекта: {ex.Message}",
                StatusCodes.Status500InternalServerError
            );
        }
    }

    private static (byte[] Bytes, string MimeType) NormalizeSourceImage(string base64, string mimeType)
    {
        var normalizedBase64 = base64.Trim();
        var normalizedMime = mimeType.Trim();

        if (string.IsNullOrWhiteSpace(normalizedBase64))
        {
            return ([], string.Empty);
        }

        if (normalizedBase64.Length > (MaxSourceImageBytes * 2))
        {
            throw new ProjectStorageException("Исходное изображение слишком большое для сохранения в проекте.");
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(normalizedBase64);
        }
        catch (FormatException)
        {
            throw new ProjectStorageException("Исходное изображение передано не в формате base64.");
        }

        if (bytes.Length > MaxSourceImageBytes)
        {
            throw new ProjectStorageException("Исходное изображение слишком большое для сохранения в проекте.");
        }

        if (!string.IsNullOrWhiteSpace(normalizedMime) &&
            !normalizedMime.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            throw new ProjectStorageException("Mime-type исходного изображения должен начинаться с image/.");
        }

        return (bytes, normalizedMime);
    }

    private static (string Name, string Note, ProjectSnapshotPayload Snapshot) NormalizeGenerationRequest(
        ProjectSaveGenerationRequest request,
        string fallbackName
    )
    {
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            name = fallbackName;
        }

        var note = request.Note.Trim();
        var snapshot = request.Snapshot ?? throw new ProjectStorageException("Нужно передать данные генерации.");
        ValidateSnapshot(snapshot);
        return (name, note, snapshot);
    }

    private static void ValidateSnapshot(ProjectSnapshotPayload snapshot)
    {
        var mosaic = snapshot.Mosaic ?? throw new ProjectStorageException("Нужны данные мозаики для сохранения.");

        if (mosaic.Rows <= 0 || mosaic.Columns <= 0)
        {
            throw new ProjectStorageException("Некорректные размеры сетки мозаики.");
        }

        if (mosaic.GridColorIds.Length != mosaic.Rows)
        {
            throw new ProjectStorageException("Количество строк сетки не совпадает с параметром rows.");
        }

        for (var row = 0; row < mosaic.GridColorIds.Length; row++)
        {
            var rowItems = mosaic.GridColorIds[row];
            if (rowItems.Length != mosaic.Columns)
            {
                throw new ProjectStorageException("Сетка должна быть прямоугольной: все строки одинаковой длины.");
            }

            if (rowItems.Any(colorId => colorId <= 0))
            {
                throw new ProjectStorageException("Сетка содержит некорректные id цветов.");
            }
        }

        if (string.IsNullOrWhiteSpace(mosaic.PreviewPngBase64))
        {
            throw new ProjectStorageException("Для сохранения проекта нужен preview изображения.");
        }

        if (mosaic.PreviewPngBase64.Length > MaxPreviewPngBytes * 2)
        {
            throw new ProjectStorageException("Preview изображения слишком большой для сохранения.");
        }

        try
        {
            var previewBytes = Convert.FromBase64String(mosaic.PreviewPngBase64);
            if (previewBytes.Length > MaxPreviewPngBytes)
            {
                throw new ProjectStorageException("Preview изображения слишком большой для сохранения.");
            }
        }
        catch (ProjectStorageException)
        {
            throw;
        }
        catch (FormatException)
        {
            throw new ProjectStorageException("Preview изображения передан не в формате base64.");
        }

        snapshot.PreviewZoom = Math.Clamp(snapshot.PreviewZoom, 0.4, 8);
        var include = snapshot.IncludeColorIds
            .Where(id => id > 0)
            .Distinct()
            .OrderBy(id => id)
            .ToList();
        var exclude = snapshot.ExcludeColorIds
            .Where(id => id > 0)
            .Distinct()
            .OrderBy(id => id)
            .ToList();

        if (include.Intersect(exclude).Any())
        {
            throw new ProjectStorageException("Один и тот же цвет нельзя одновременно включить и исключить.");
        }

        snapshot.IncludeColorIds = include;
        snapshot.ExcludeColorIds = exclude;
    }

    private static bool IsDuplicateGenerationVersion(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains(
            "IX_project_generations_project_id_version",
            StringComparison.OrdinalIgnoreCase
        ) ?? false;
}
