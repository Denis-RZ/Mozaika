using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Mozaika.Api.Database.Entities;

[Table("auth_sessions")]
public sealed class AuthSessionEntity
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }

    [Required]
    [MaxLength(128)]
    [Column("token_hash")]
    public string TokenHash { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }

    [Column("last_seen_at")]
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;

    [Column("is_revoked")]
    public bool IsRevoked { get; set; }

    [ForeignKey(nameof(UserId))]
    public UserEntity User { get; set; } = null!;
}
