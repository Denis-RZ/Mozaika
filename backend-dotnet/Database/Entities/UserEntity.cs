using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mozaika.Api.Database.Entities;

[Table("users")]
public sealed class UserEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [MaxLength(120)]
    [Column("username")]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MaxLength(180)]
    [Column("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    [Required]
    [MaxLength(260)]
    [Column("password_hash")]
    public string PasswordHash { get; set; } = string.Empty;

    [Required]
    [MaxLength(260)]
    [Column("password_salt")]
    public string PasswordSalt { get; set; } = string.Empty;

    [Column("password_iterations")]
    public int PasswordIterations { get; set; } = 120_000;

    [Required]
    [MaxLength(24)]
    [Column("role")]
    public string Role { get; set; } = "customer";

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<AuthSessionEntity> Sessions { get; set; } = new List<AuthSessionEntity>();
}
