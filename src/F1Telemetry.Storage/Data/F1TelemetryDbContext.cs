using Microsoft.EntityFrameworkCore;

namespace F1Telemetry.Storage.Data;

internal sealed class F1TelemetryDbContext(DbContextOptions<F1TelemetryDbContext> options) : DbContext(options)
{
    public DbSet<SessionEntity> Sessions => Set<SessionEntity>();
    public DbSet<LapEntity> Laps => Set<LapEntity>();
    public DbSet<LapFeedbackEntity> LapFeedback => Set<LapFeedbackEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SessionEntity>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasMany(s => s.Laps).WithOne(l => l.Session).HasForeignKey(l => l.SessionId);
        });

        modelBuilder.Entity<LapEntity>(e =>
        {
            e.HasKey(l => l.Id);
            e.Property(l => l.Id).ValueGeneratedOnAdd();
            e.HasMany(l => l.Feedback).WithOne(f => f.Lap).HasForeignKey(f => f.LapId);
        });

        modelBuilder.Entity<LapFeedbackEntity>(e =>
        {
            e.HasKey(f => f.Id);
            e.Property(f => f.Id).ValueGeneratedOnAdd();
        });
    }
}
