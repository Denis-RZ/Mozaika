using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mozaika.Api.Database.Entities;

[Table("project_generations")]
public sealed class ProjectGenerationEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("project_id")]
    public int ProjectId { get; set; }

    [Column("version")]
    public int Version { get; set; }

    [Required]
    [MaxLength(140)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1200)]
    [Column("note")]
    public string Note { get; set; } = string.Empty;

    [Required]
    [Column("snapshot_json")]
    public string SnapshotJson { get; set; } = "{}";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(ProjectId))]
    public ProjectEntity Project { get; set; } = null!;

    public ICollection<ProjectShareEntity> Shares { get; set; } = new List<ProjectShareEntity>();
    public ICollection<ProjectOrderEntity> Orders { get; set; } = new List<ProjectOrderEntity>();
}
