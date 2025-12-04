// backend/src/LogMonitor.Infrastructure/Data/LogMonitorDbContext.cs
using Microsoft.EntityFrameworkCore;
using LogMonitor.Core.Entities;

namespace LogMonitor.Infrastructure.Data;

public class LogMonitorDbContext : DbContext
{
    public LogMonitorDbContext(DbContextOptions<LogMonitorDbContext> options)
        : base(options) { }

    public DbSet<ErrorEntity> Errors => Set<ErrorEntity>();
    public DbSet<NotificationEntity> Notifications => Set<NotificationEntity>();
    public DbSet<FilePositionEntity> FilePositions => Set<FilePositionEntity>();
    public DbSet<TelegramSubscriberEntity> TelegramSubscribers => Set<TelegramSubscriberEntity>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ErrorEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.FileName, e.LinePosition }).IsUnique();
            entity.HasIndex(e => e.CreatedAt);
            entity.Property(e => e.ContentHash).IsRequired().HasMaxLength(64);
        });

        modelBuilder.Entity<NotificationEntity>(entity =>
        {
            entity.HasKey(n => n.Id);
            entity.HasOne(n => n.Error)
                  .WithMany()
                  .HasForeignKey(n => n.ErrorId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(n => n.ErrorId);
        });

        modelBuilder.Entity<FilePositionEntity>(entity =>
        {
            entity.HasKey(fp => fp.FilePath);
            entity.Property(fp => fp.FilePath).IsRequired();
            entity.Property(fp => fp.LastPosition).IsRequired();
        });
        modelBuilder.Entity<TelegramSubscriberEntity>(entity =>
        {
            entity.HasKey(t => t.ChatId);
            entity.Property(t => t.ChatId).ValueGeneratedNever();
        });
    }
}