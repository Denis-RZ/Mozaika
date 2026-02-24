using System.ComponentModel.DataAnnotations;

namespace Mozaika.Api.Contracts;

public sealed class ProjectSnapshotPayload
{
    [Required(ErrorMessage = "Для сохранения проекта нужна сгенерированная мозаика.")]
    public MosaicGenerateResponse Mosaic { get; set; } = new();
    public List<int> IncludeColorIds { get; set; } = [];
    public List<int> ExcludeColorIds { get; set; } = [];
    public int? GroutColorId { get; set; }
    public double PreviewZoom { get; set; } = 1;
    public double PreviewPanX { get; set; }
    public double PreviewPanY { get; set; }
}

public sealed class ProjectSaveGenerationRequest
{
    [MaxLength(140, ErrorMessage = "Название версии слишком длинное.")]
    public string Name { get; set; } = string.Empty;
    [MaxLength(1200, ErrorMessage = "Комментарий слишком длинный.")]
    public string Note { get; set; } = string.Empty;

    [Required(ErrorMessage = "Нужно передать данные генерации.")]
    public ProjectSnapshotPayload Snapshot { get; set; } = new();
}

public sealed class ProjectCreateRequest
{
    [Required(ErrorMessage = "Название проекта обязательно.")]
    [MinLength(2, ErrorMessage = "Название проекта должно быть не короче 2 символов.")]
    [MaxLength(120, ErrorMessage = "Название проекта должно быть не длиннее 120 символов.")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1200, ErrorMessage = "Описание проекта слишком длинное.")]
    public string Description { get; set; } = string.Empty;

    [MaxLength(64, ErrorMessage = "Некорректный mime-type изображения.")]
    public string SourceImageMimeType { get; set; } = string.Empty;
    public string SourceImageBase64 { get; set; } = string.Empty;

    [Required(ErrorMessage = "Нужно передать стартовую генерацию проекта.")]
    public ProjectSaveGenerationRequest InitialGeneration { get; set; } = new();
}

public sealed class ProjectUpdateRequest
{
    [MaxLength(120, ErrorMessage = "Название проекта должно быть не длиннее 120 символов.")]
    public string? Name { get; set; }

    [MaxLength(1200, ErrorMessage = "Описание проекта слишком длинное.")]
    public string? Description { get; set; }

    public bool HasAnyValue() => Name is not null || Description is not null;
}

public sealed class ProjectGenerationSummaryResponse
{
    public int Id { get; set; }
    public int Version { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public sealed class ProjectGenerationReadResponse
{
    public int Id { get; set; }
    public int Version { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public ProjectSnapshotPayload Snapshot { get; set; } = new();
}

public sealed class ProjectListItemResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int GenerationsCount { get; set; }
    public int? ActiveGenerationId { get; set; }
    public DateTime? LastGenerationAt { get; set; }
}

public sealed class ProjectReadResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SourceImageMimeType { get; set; } = string.Empty;
    public string SourceImageBase64 { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int GenerationsCount { get; set; }
    public int? ActiveGenerationId { get; set; }
    public DateTime? LastGenerationAt { get; set; }
    public ProjectGenerationReadResponse? ActiveGeneration { get; set; }
    public List<ProjectGenerationSummaryResponse> Generations { get; set; } = [];
}
