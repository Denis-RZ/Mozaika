using System.ComponentModel.DataAnnotations;

namespace Mozaika.Api.Contracts;

public sealed class GroutColorReadResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string RgbHex { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class GroutColorCreateRequest
{
    [Required(ErrorMessage = "Название обязательно.")]
    [MinLength(1)]
    [MaxLength(80)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "HEX обязателен.")]
    public string RgbHex { get; set; } = string.Empty;

    public bool? IsActive { get; set; }
}

public sealed class GroutColorUpdateRequest
{
    [MinLength(1)]
    [MaxLength(80)]
    public string? Name { get; set; }

    public string? RgbHex { get; set; }

    public bool? IsActive { get; set; }

    public bool HasAnyValue() => Name is not null || RgbHex is not null || IsActive is not null;
}
