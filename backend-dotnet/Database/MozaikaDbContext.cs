using Microsoft.EntityFrameworkCore;
using Mozaika.Api.Database.Entities;

namespace Mozaika.Api.Database;

public sealed class MozaikaDbContext(DbContextOptions<MozaikaDbContext> options) : DbContext(options)
{
    public DbSet<ColorEntity> Colors => Set<ColorEntity>();
    public DbSet<GroutColorEntity> GroutColors => Set<GroutColorEntity>();
    public DbSet<AppSettingEntity> AppSettings => Set<AppSettingEntity>();
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<AuthSessionEntity> AuthSessions => Set<AuthSessionEntity>();
    public DbSet<ProjectEntity> Projects => Set<ProjectEntity>();
    public DbSet<ProjectGenerationEntity> ProjectGenerations => Set<ProjectGenerationEntity>();
    public DbSet<ProjectShareEntity> ProjectShares => Set<ProjectShareEntity>();
    public DbSet<ProjectOrderEntity> ProjectOrders => Set<ProjectOrderEntity>();
    public DbSet<ProjectOrderStatusHistoryEntity> ProjectOrderStatusHistory => Set<ProjectOrderStatusHistoryEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ColorEntity>(entity =>
        {
            entity.HasIndex(item => item.Name).IsUnique();
            entity.HasIndex(item => item.RalCode);
            entity.HasIndex(item => item.RgbHex);
            entity.Property(item => item.Id).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<GroutColorEntity>(entity =>
        {
            entity.HasIndex(item => item.Name).IsUnique();
            entity.HasIndex(item => item.RgbHex).IsUnique();
            entity.Property(item => item.Id).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<AppSettingEntity>(entity =>
        {
            entity.Property(item => item.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<UserEntity>(entity =>
        {
            entity.HasIndex(item => item.Username).IsUnique();
            entity.HasIndex(item => item.Role);
            entity.Property(item => item.Id).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<AuthSessionEntity>(entity =>
        {
            entity.HasIndex(item => item.TokenHash).IsUnique();
            entity.HasIndex(item => item.ExpiresAt);
            entity.Property(item => item.Id).ValueGeneratedOnAdd();
            entity.HasOne(item => item.User)
                .WithMany(item => item.Sessions)
                .HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProjectEntity>(entity =>
        {
            entity.HasIndex(item => item.UpdatedAt);
            entity.HasIndex(item => item.OwnerUsername);
            entity.Property(item => item.Id).ValueGeneratedOnAdd();
            entity.HasOne(item => item.ActiveGeneration)
                .WithMany()
                .HasForeignKey(item => item.ActiveGenerationId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ProjectGenerationEntity>(entity =>
        {
            entity.HasIndex(item => new { item.ProjectId, item.Version }).IsUnique();
            entity.HasIndex(item => item.CreatedAt);
            entity.Property(item => item.Id).ValueGeneratedOnAdd();
            entity.HasOne(item => item.Project)
                .WithMany(item => item.Generations)
                .HasForeignKey(item => item.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProjectShareEntity>(entity =>
        {
            entity.HasIndex(item => item.Token).IsUnique();
            entity.HasIndex(item => new { item.ProjectId, item.CreatedAt });
            entity.Property(item => item.Id).ValueGeneratedOnAdd();
            entity.HasOne(item => item.Project)
                .WithMany(item => item.Shares)
                .HasForeignKey(item => item.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.Generation)
                .WithMany(item => item.Shares)
                .HasForeignKey(item => item.GenerationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProjectOrderEntity>(entity =>
        {
            entity.HasIndex(item => new { item.ProjectId, item.CreatedAt });
            entity.HasIndex(item => item.Status);
            entity.Property(item => item.Id).ValueGeneratedOnAdd();
            entity.HasOne(item => item.Project)
                .WithMany(item => item.Orders)
                .HasForeignKey(item => item.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.Generation)
                .WithMany(item => item.Orders)
                .HasForeignKey(item => item.GenerationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProjectOrderStatusHistoryEntity>(entity =>
        {
            entity.HasIndex(item => new { item.OrderId, item.CreatedAt });
            entity.Property(item => item.Id).ValueGeneratedOnAdd();
            entity.HasOne(item => item.Order)
                .WithMany(item => item.StatusHistory)
                .HasForeignKey(item => item.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
