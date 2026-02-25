using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Mozaika.Api.Contracts;
using Mozaika.Api.Database;
using Mozaika.Api.Database.Entities;

namespace Mozaika.Api.Services;

public sealed class ProjectWorkflowService(
    MozaikaDbContext dbContext,
    ProjectStorageService projectStorageService
)
{
    private const int MaxShareTokenGenerationAttempts = 5;

    public async Task<List<ProjectShareReadResponse>> ListSharesAsync(
        int projectId,
        string callerUsername,
        string callerRole
    )
    {
        await projectStorageService.GetProjectAsync(projectId, callerUsername, callerRole);
        var shares = await dbContext.ProjectShares
            .AsNoTracking()
            .Where(item => item.ProjectId == projectId)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync();
        return shares.Select(MapShare).ToList();
    }

    public async Task<ProjectShareReadResponse> CreateShareAsync(
        int projectId,
        ProjectShareCreateRequest request,
        string createdBy,
        string callerUsername,
        string callerRole
    )
    {
        var project = await projectStorageService.GetProjectAsync(projectId, callerUsername, callerRole);
        var generation = await ResolveGenerationAsync(project, request.GenerationId, callerUsername, callerRole);
        var utcNow = DateTime.UtcNow;
        var createdByNormalized = string.IsNullOrWhiteSpace(createdBy) ? "unknown" : createdBy.Trim();

        for (var attempt = 1; attempt <= MaxShareTokenGenerationAttempts; attempt++)
        {
            var share = new ProjectShareEntity
            {
                ProjectId = projectId,
                GenerationId = generation.Id,
                GenerationVersion = generation.Version,
                GenerationName = generation.Name,
                Token = GenerateShareToken(),
                CreatedAt = utcNow,
                ExpiresAt = request.ExpiresInDays is null
                    ? null
                    : utcNow.AddDays(Math.Clamp(request.ExpiresInDays.Value, 1, 3650)),
                IsRevoked = false,
                CreatedBy = createdByNormalized,
            };

            dbContext.ProjectShares.Add(share);
            try
            {
                await dbContext.SaveChangesAsync();
                return MapShare(share);
            }
            catch (DbUpdateException ex) when (IsDuplicateShareToken(ex))
            {
                dbContext.Entry(share).State = EntityState.Detached;
                if (attempt == MaxShareTokenGenerationAttempts)
                {
                    throw new ProjectStorageException(
                        "Не удалось создать share-ссылку, повторите попытку.",
                        StatusCodes.Status409Conflict
                    );
                }
            }
        }

        throw new ProjectStorageException(
            "Не удалось создать share-ссылку, повторите попытку.",
            StatusCodes.Status409Conflict
        );
    }

    public async Task<ProjectShareReadResponse> RevokeShareAsync(
        int projectId,
        int shareId,
        string callerUsername,
        string callerRole
    )
    {
        await projectStorageService.GetProjectAsync(projectId, callerUsername, callerRole);
        var share = await dbContext.ProjectShares.FirstOrDefaultAsync(item =>
            item.ProjectId == projectId && item.Id == shareId);

        if (share is null)
        {
            throw new ProjectStorageException("Ссылка не найдена.", StatusCodes.Status404NotFound);
        }

        if (!share.IsRevoked)
        {
            share.IsRevoked = true;
            await dbContext.SaveChangesAsync();
        }

        return MapShare(share);
    }

    public async Task<ProjectShareResolveResponse> ResolveShareAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ProjectStorageException("Ссылка пустая или повреждена.");
        }

        var normalizedToken = token.Trim();
        var share = await dbContext.ProjectShares
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Token == normalizedToken);

        if (share is null)
        {
            throw new ProjectStorageException("Ссылка не найдена.", StatusCodes.Status404NotFound);
        }

        if (share.IsRevoked)
        {
            throw new ProjectStorageException("Ссылка была отозвана.", StatusCodes.Status410Gone);
        }

        if (share.ExpiresAt is not null && share.ExpiresAt <= DateTime.UtcNow)
        {
            throw new ProjectStorageException("Срок действия ссылки истек.", StatusCodes.Status410Gone);
        }

        var project = await projectStorageService.GetProjectAsync(share.ProjectId);
        var generation = await projectStorageService.GetGenerationAsync(share.ProjectId, share.GenerationId);
        return new ProjectShareResolveResponse
        {
            ProjectId = project.Id,
            ProjectName = project.Name,
            ProjectDescription = project.Description,
            SourceImageMimeType = project.SourceImageMimeType,
            SourceImageBase64 = project.SourceImageBase64,
            Generation = generation,
            Share = MapShare(share),
        };
    }

    public async Task<List<ProjectOrderReadResponse>> ListOrdersAsync(
        int projectId,
        string callerUsername,
        string callerRole
    )
    {
        await projectStorageService.GetProjectAsync(projectId, callerUsername, callerRole);
        var orders = await dbContext.ProjectOrders
            .AsNoTracking()
            .Where(item => item.ProjectId == projectId)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync();
        return orders.Select(MapOrder).ToList();
    }

    public async Task<ProjectOrderReadResponse> CreateOrderAsync(
        int projectId,
        ProjectOrderCreateRequest request,
        string submittedBy,
        string callerUsername,
        string callerRole
    )
    {
        var project = await projectStorageService.GetProjectAsync(projectId, callerUsername, callerRole);
        var generation = await ResolveGenerationAsync(project, request.GenerationId, callerUsername, callerRole);
        var customerName = request.CustomerName.Trim();
        if (string.IsNullOrWhiteSpace(customerName))
        {
            throw new ProjectStorageException("Введите имя контактного лица.");
        }

        var submittedByNormalized = string.IsNullOrWhiteSpace(submittedBy) ? "unknown" : submittedBy.Trim();
        var utcNow = DateTime.UtcNow;
        var order = new ProjectOrderEntity
        {
            ProjectId = projectId,
            GenerationId = generation.Id,
            GenerationVersion = generation.Version,
            GenerationName = generation.Name,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
            Status = ProjectOrderStatuses.Submitted,
            StatusComment = string.Empty,
            CustomerName = customerName,
            CustomerEmail = request.CustomerEmail.Trim(),
            CustomerPhone = request.CustomerPhone.Trim(),
            Comment = request.Comment.Trim(),
            SubmittedBy = submittedByNormalized,
            TotalPrice = generation.Snapshot.Mosaic.Price.TotalPrice,
            Currency = generation.Snapshot.Mosaic.Price.Currency,
            TotalChips = generation.Snapshot.Mosaic.TotalChips,
            ColorsUsed = generation.Snapshot.Mosaic.ActualColorsUsed,
        };

        dbContext.ProjectOrders.Add(order);
        await dbContext.SaveChangesAsync();

        dbContext.ProjectOrderStatusHistory.Add(new ProjectOrderStatusHistoryEntity
        {
            OrderId = order.Id,
            Status = order.Status,
            StatusComment = string.Empty,
            ChangedBy = submittedByNormalized,
            CreatedAt = utcNow,
        });

        await dbContext.SaveChangesAsync();
        return MapOrder(order);
    }

    public async Task<ProjectOrderReadResponse> UpdateOrderStatusAsync(
        int projectId,
        int orderId,
        ProjectOrderStatusUpdateRequest request,
        string changedBy,
        string callerUsername,
        string callerRole
    )
    {
        await projectStorageService.GetProjectAsync(projectId, callerUsername, callerRole);
        var status = request.Status.Trim().ToLowerInvariant();
        if (!ProjectOrderStatuses.IsValid(status))
        {
            var allowed = string.Join(", ", ProjectOrderStatuses.All);
            throw new ProjectStorageException($"Некорректный статус заказа. Доступно: {allowed}.");
        }

        var order = await dbContext.ProjectOrders.FirstOrDefaultAsync(item =>
            item.ProjectId == projectId && item.Id == orderId);

        if (order is null)
        {
            throw new ProjectStorageException("Предварительный заказ не найден.", StatusCodes.Status404NotFound);
        }

        var statusComment = request.StatusComment.Trim();
        var changedByNormalized = string.IsNullOrWhiteSpace(changedBy) ? "admin" : changedBy.Trim();
        if (string.Equals(order.Status, status, StringComparison.Ordinal) &&
            string.Equals(order.StatusComment, statusComment, StringComparison.Ordinal))
        {
            return MapOrder(order);
        }

        order.Status = status;
        order.StatusComment = statusComment;
        order.UpdatedAt = DateTime.UtcNow;
        dbContext.ProjectOrderStatusHistory.Add(new ProjectOrderStatusHistoryEntity
        {
            OrderId = order.Id,
            Status = order.Status,
            StatusComment = order.StatusComment,
            ChangedBy = changedByNormalized,
            CreatedAt = order.UpdatedAt,
        });

        await dbContext.SaveChangesAsync();
        return MapOrder(order);
    }

    private async Task<ProjectGenerationReadResponse> ResolveGenerationAsync(
        ProjectReadResponse project,
        int? generationId,
        string callerUsername,
        string callerRole
    )
    {
        if (generationId is not null)
        {
            return await projectStorageService.GetGenerationAsync(
                project.Id,
                generationId.Value,
                callerUsername,
                callerRole
            );
        }

        if (project.ActiveGeneration is not null)
        {
            return project.ActiveGeneration;
        }

        var firstGeneration = project.Generations.OrderByDescending(item => item.Version).FirstOrDefault();
        if (firstGeneration is null)
        {
            throw new ProjectStorageException("В проекте нет сохраненных генераций.");
        }

        return await projectStorageService.GetGenerationAsync(
            project.Id,
            firstGeneration.Id,
            callerUsername,
            callerRole
        );
    }

    private static ProjectShareReadResponse MapShare(ProjectShareEntity share) => new()
    {
        Id = share.Id,
        ProjectId = share.ProjectId,
        GenerationId = share.GenerationId,
        GenerationVersion = share.GenerationVersion,
        GenerationName = share.GenerationName,
        Token = share.Token,
        CreatedAt = share.CreatedAt,
        ExpiresAt = share.ExpiresAt,
        IsRevoked = share.IsRevoked,
        CreatedBy = share.CreatedBy,
    };

    private static ProjectOrderReadResponse MapOrder(ProjectOrderEntity order) => new()
    {
        Id = order.Id,
        ProjectId = order.ProjectId,
        GenerationId = order.GenerationId,
        GenerationVersion = order.GenerationVersion,
        GenerationName = order.GenerationName,
        CreatedAt = order.CreatedAt,
        UpdatedAt = order.UpdatedAt,
        Status = order.Status,
        StatusComment = order.StatusComment,
        CustomerName = order.CustomerName,
        CustomerEmail = order.CustomerEmail,
        CustomerPhone = order.CustomerPhone,
        Comment = order.Comment,
        SubmittedBy = order.SubmittedBy,
        TotalPrice = order.TotalPrice,
        Currency = order.Currency,
        TotalChips = order.TotalChips,
        ColorsUsed = order.ColorsUsed,
    };

    private static string GenerateShareToken()
    {
        Span<byte> bytes = stackalloc byte[24];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static bool IsDuplicateShareToken(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("IX_project_shares_token", StringComparison.OrdinalIgnoreCase) ?? false;
}
