using Microsoft.EntityFrameworkCore;

namespace EmojiEstimator.Web.Data;

public sealed class EmojiEstimatorDbContext(DbContextOptions<EmojiEstimatorDbContext> options) : DbContext(options)
{
    public DbSet<RepositoryScan> RepositoryScans => Set<RepositoryScan>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<RepositoryScan>(entity =>
        {
            entity.ToTable("RepositoryScans");

            entity.HasKey(scan => scan.Id);

            entity.Property(scan => scan.RepositoryOwner)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(scan => scan.RepositoryName)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(scan => scan.NormalizedKey)
                .HasMaxLength(320)
                .IsRequired();

            entity.Property(scan => scan.Status)
                .HasMaxLength(32)
                .IsRequired();

            entity.Property(scan => scan.FailureMessage)
                .HasMaxLength(2048);

            entity.HasIndex(scan => scan.NormalizedKey)
                .IsUnique();

            entity.HasIndex(scan => scan.ExpiresAtUtc);
        });
    }
}
