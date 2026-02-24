using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mozaika.Api.Database.Entities;

[Table("project_shares")]
public sealed class ProjectShareEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("project_id")]
    public int ProjectId { get; set; }

    [Column("generation_id")]
    public int GenerationId { get; set; }

    [Column("generation_version")]
    public int GenerationVersion { get; set; }

    [Required]
    [MaxLength(140)]
    [Column("generation_name")]
    public string GenerationName { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    [Column("token")]
    public string Token { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("expires_at")]
    public DateTime? ExpiresAt { get; set; }

    [Column("is_revoked")]
    public bool IsRevoked { get; set; }

    [MaxLength(120)]
    [Column("created_by")]
    public string CreatedBy { get; set; } = string.Empty;

    [ForeignKey(nameof(ProjectId))]
    public ProjectEntity Project { get; set; } = null!;

    [ForeignKey(nameof(GenerationId))]
    public ProjectGenerationEntity Generation { get; set; } = null!;
}
