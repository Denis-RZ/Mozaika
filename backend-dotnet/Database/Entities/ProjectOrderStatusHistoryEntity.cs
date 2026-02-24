using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mozaika.Api.Database.Entities;

[Table("project_order_status_history")]
public sealed class ProjectOrderStatusHistoryEntity
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("order_id")]
    public int OrderId { get; set; }

    [Required]
    [MaxLength(24)]
    [Column("status")]
    public string Status { get; set; } = string.Empty;

    [MaxLength(1200)]
    [Column("status_comment")]
    public string StatusComment { get; set; } = string.Empty;

    [MaxLength(120)]
    [Column("changed_by")]
    public string ChangedBy { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(OrderId))]
    public ProjectOrderEntity Order { get; set; } = null!;
}
