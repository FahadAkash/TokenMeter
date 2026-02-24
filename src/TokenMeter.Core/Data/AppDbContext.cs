using Microsoft.EntityFrameworkCore;
using TokenMeter.Core.Models;

namespace TokenMeter.Core.Data;

public class AppDbContext : DbContext
{
    public DbSet<UsageEntry> UsageEntries => Set<UsageEntry>();
    public DbSet<CostHistorySnapshot> CostHistory => Set<CostHistorySnapshot>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UsageEntry>().HasKey(e => e.Id);

        modelBuilder.Entity<CostHistorySnapshot>().HasKey(e => e.Id);
        modelBuilder.Entity<CostHistorySnapshot>().HasIndex(e => e.Date).IsUnique();

        // Convert enum to string for better database readability
        modelBuilder.Entity<UsageEntry>(entity =>
        {
            entity.Property(e => e.Provider).HasConversion<string>();

            // Ensure we only have one entry per provider per day
            entity.HasIndex(e => new { e.Provider, e.Date }).IsUnique();
        });
    }
}
