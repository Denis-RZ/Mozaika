using System.ComponentModel.DataAnnotations;

namespace Mozaika.Api.Contracts;

public sealed class ProjectShareCreateRequest
{
    public int? GenerationId { get; set; }
    [Range(1, 3650, ErrorMessage = "Срок действия ссылки должен быть от 1 до 3650 дней.")]
    public int? ExpiresInDays { get; set; }
}

public sealed class ProjectShareReadResponse
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

public sealed class ProjectShareResolveResponse
{
    public int ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string ProjectDescription { get; set; } = string.Empty;
    public string SourceImageMimeType { get; set; } = string.Empty;
    public string SourceImageBase64 { get; set; } = string.Empty;
    public ProjectGenerationReadResponse Generation { get; set; } = new();
    public ProjectShareReadResponse Share { get; set; } = new();
}

public static class ProjectOrderStatuses
{
    public const string Submitted = "submitted";
    public const string InReview = "in_review";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string Cancelled = "cancelled";

    public static readonly IReadOnlyList<string> All =
    [
        Submitted,
        InReview,
        Approved,
        Rejected,
        Cancelled,
    ];

    public static bool IsValid(string? status) =>
        !string.IsNullOrWhiteSpace(status) &&
        All.Contains(status.Trim().ToLowerInvariant());
}

public sealed class ProjectOrderCreateRequest
{
    public int? GenerationId { get; set; }
    [Required(ErrorMessage = "Введите имя контактного лица.")]
    [MaxLength(180, ErrorMessage = "Имя контактного лица слишком длинное.")]
    public string CustomerName { get; set; } = string.Empty;

    [MaxLength(180, ErrorMessage = "E-mail слишком длинный.")]
    public string CustomerEmail { get; set; } = string.Empty;

    [MaxLength(64, ErrorMessage = "Телефон слишком длинный.")]
    public string CustomerPhone { get; set; } = string.Empty;

    [MaxLength(2400, ErrorMessage = "Комментарий слишком длинный.")]
    public string Comment { get; set; } = string.Empty;
}

public sealed class ProjectOrderStatusUpdateRequest
{
    [Required(ErrorMessage = "Нужно передать новый статус заказа.")]
    public string Status { get; set; } = ProjectOrderStatuses.InReview;

    [MaxLength(1200, ErrorMessage = "Комментарий к статусу слишком длинный.")]
    public string StatusComment { get; set; } = string.Empty;
}

public sealed class ProjectOrderReadResponse
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public int GenerationId { get; set; }
    public int GenerationVersion { get; set; }
    public string GenerationName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string Status { get; set; } = ProjectOrderStatuses.Submitted;
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
