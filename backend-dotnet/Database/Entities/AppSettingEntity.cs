using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mozaika.Api.Database.Entities;

[Table("app_settings")]
public sealed class AppSettingEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; } = 1;

    [Column("default_field_width_mm")]
    public double DefaultFieldWidthMm { get; set; } = 1200.0;

    [Column("default_field_height_mm")]
    public double DefaultFieldHeightMm { get; set; } = 800.0;

    [Column("default_cell_size_mm")]
    public double DefaultCellSizeMm { get; set; } = 10.0;

    [Column("default_gap_mm")]
    public double DefaultGapMm { get; set; } = 2.0;

    [Column("cors_origins_json")]
    public string CorsOriginsJson { get; set; } = "[\"*\"]";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
