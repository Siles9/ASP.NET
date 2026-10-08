using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ZooStav.Web.Domain;

namespace ZooStav.Web.Data;

public class ZooDbContext : IdentityDbContext<ZooUser>
{
    public ZooDbContext(DbContextOptions<ZooDbContext> options) : base(options)
    {
    }

    public DbSet<Animal> Animals => Set<Animal>();
    public DbSet<MediaItem> MediaItems => Set<MediaItem>();
    public DbSet<DiaryEntry> DiaryEntries => Set<DiaryEntry>();
    public DbSet<Donation> Donations => Set<Donation>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    /// <summary>Поиск по дневнику: нормализуем текст записи при сохранении.</summary>
    private void NormalizeDiarySearchText()
    {
        foreach (var entry in ChangeTracker.Entries<DiaryEntry>()
                     .Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            var source = (entry.Entity.Title + " " + entry.Entity.Description).ToLowerInvariant().Trim();
            entry.Entity.SearchText = source.Length > 1200 ? source[..1200] : source;
        }
    }

    public override int SaveChanges()
    {
        NormalizeDiarySearchText();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        NormalizeDiarySearchText();
        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Animal>(e =>
        {
            e.HasIndex(a => a.Slug).IsUnique();
            e.Property(a => a.Slug).HasMaxLength(64).IsRequired();
            e.Property(a => a.Name).HasMaxLength(160).IsRequired();
            e.Property(a => a.Species).HasMaxLength(200).IsRequired();
        });

        builder.Entity<MediaItem>(e =>
        {
            e.HasOne(m => m.Animal)
                .WithMany(a => a.Media)
                .HasForeignKey(m => m.AnimalId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<DiaryEntry>(e =>
        {
            e.HasIndex(d => d.OccurredAtUtc);
            e.HasIndex(d => d.Type);
            e.HasOne(d => d.Animal)
                .WithMany(a => a.DiaryEntries)
                .HasForeignKey(d => d.AnimalId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(d => d.User)
                .WithMany(u => u.DiaryEntries)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Donation>(e =>
        {
            e.Property(d => d.Amount).HasPrecision(18, 2);
            e.HasIndex(d => d.CreatedAtUtc);
            e.HasOne(d => d.Animal)
                .WithMany(a => a.Donations)
                .HasForeignKey(d => d.AnimalId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AuditLog>(e => e.HasIndex(a => a.TimestampUtc));
    }
}
