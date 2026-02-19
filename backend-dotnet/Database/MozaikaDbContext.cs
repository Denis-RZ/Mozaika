using Microsoft.EntityFrameworkCore;
using Mozaika.Api.Database.Entities;

namespace Mozaika.Api.Database;

public sealed class MozaikaDbContext(DbContextOptions<MozaikaDbContext> options) : DbContext(options)
{
    public DbSet<ColorEntity> Colors => Set<ColorEntity>();
    public DbSet<GroutColorEntity> GroutColors => Set<GroutColorEntity>();
    public DbSet<AppSettingEntity> AppSettings => Set<AppSettingEntity>();

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
    }
}
