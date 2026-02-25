using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mozaika.Api.Database.Entities;

[Table("projects")]
public sealed class ProjectEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [MaxLength(120)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1200)]
    [Column("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>Username of the project owner. Null means legacy/admin-owned project.</summary>
    [MaxLength(80)]
    [Column("owner_username")]
    public string? OwnerUsername { get; set; }

    [MaxLength(64)]
    [Column("source_image_mime_type")]
    public string SourceImageMimeType { get; set; } = string.Empty;

    [Column("source_image_bytes")]
    public byte[] SourceImageBytes { get; set; } = [];

    [Column("active_generation_id")]
    public int? ActiveGenerationId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(ActiveGenerationId))]
    public ProjectGenerationEntity? ActiveGeneration { get; set; }

    public ICollection<ProjectGenerationEntity> Generations { get; set; } = new List<ProjectGenerationEntity>();
    public ICollection<ProjectShareEntity> Shares { get; set; } = new List<ProjectShareEntity>();
    public ICollection<ProjectOrderEntity> Orders { get; set; } = new List<ProjectOrderEntity>();
}
