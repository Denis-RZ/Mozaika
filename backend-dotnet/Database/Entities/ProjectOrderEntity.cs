using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Mozaika.Api.Contracts;

namespace Mozaika.Api.Database.Entities;

[Table("project_orders")]
public sealed class ProjectOrderEntity
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

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    [MaxLength(24)]
    [Column("status")]
    public string Status { get; set; } = ProjectOrderStatuses.Submitted;

    [MaxLength(1200)]
    [Column("status_comment")]
    public string StatusComment { get; set; } = string.Empty;

    [Required]
    [MaxLength(180)]
    [Column("customer_name")]
    public string CustomerName { get; set; } = string.Empty;

    [MaxLength(180)]
    [Column("customer_email")]
    public string CustomerEmail { get; set; } = string.Empty;

    [MaxLength(64)]
    [Column("customer_phone")]
    public string CustomerPhone { get; set; } = string.Empty;

    [MaxLength(2400)]
    [Column("comment")]
    public string Comment { get; set; } = string.Empty;

    [MaxLength(120)]
    [Column("submitted_by")]
    public string SubmittedBy { get; set; } = string.Empty;

    [Column("total_price")]
    public double TotalPrice { get; set; }

    [MaxLength(8)]
    [Column("currency")]
    public string Currency { get; set; } = "RUB";

    [Column("total_chips")]
    public int TotalChips { get; set; }

    [Column("colors_used")]
    public int ColorsUsed { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public ProjectEntity Project { get; set; } = null!;

    [ForeignKey(nameof(GenerationId))]
    public ProjectGenerationEntity Generation { get; set; } = null!;

    public ICollection<ProjectOrderStatusHistoryEntity> StatusHistory { get; set; } = new List<ProjectOrderStatusHistoryEntity>();
}
