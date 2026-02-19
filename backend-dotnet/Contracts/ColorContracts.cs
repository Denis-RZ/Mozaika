using System.ComponentModel.DataAnnotations;

namespace Mozaika.Api.Contracts;

public sealed class ColorReadResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string RalCode { get; set; } = string.Empty;
    public string RgbHex { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class ColorCreateRequest
{
    [Required(ErrorMessage = "Название обязательно.")]
    [MinLength(1)]
    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "RAL обязателен.")]
    [MinLength(1)]
    [MaxLength(32)]
    public string RalCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "HEX обязателен.")]
    public string RgbHex { get; set; } = string.Empty;

    public bool? IsActive { get; set; }
}

public sealed class ColorUpdateRequest
{
    [MinLength(1)]
    [MaxLength(120)]
    public string? Name { get; set; }

    [MinLength(1)]
    [MaxLength(32)]
    public string? RalCode { get; set; }

    public string? RgbHex { get; set; }

    public bool? IsActive { get; set; }

    public bool HasAnyValue() => Name is not null || RalCode is not null || RgbHex is not null || IsActive is not null;
}

public sealed class ColorBulkCreateRequest
{
    [Required(ErrorMessage = "Список цветов обязателен.")]
    [MinLength(1, ErrorMessage = "Список цветов не может быть пустым.")]
    [MaxLength(500, ErrorMessage = "Можно загрузить не более 500 цветов за раз.")]
    public List<ColorCreateRequest> Colors { get; set; } = [];
}
