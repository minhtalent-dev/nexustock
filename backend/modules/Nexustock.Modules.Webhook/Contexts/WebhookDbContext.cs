using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Webhook.Entities;

namespace Nexustock.Modules.Webhook.Contexts;

public class WebhookDbContext : DbContext
{
    public WebhookDbContext(DbContextOptions<WebhookDbContext> options) : base(options) { }

    public DbSet<WebhookSubscription> WebhookSubscriptions => Set<WebhookSubscription>();
    public DbSet<WebhookDelivery> WebhookDeliveries => Set<WebhookDelivery>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<WebhookSubscription>(e =>
        {
            e.ToTable("WebhookSubscriptions");
            e.HasKey(x => x.Id);
            e.Property(x => x.TenantId).IsRequired();
            e.Property(x => x.TargetUrl).HasMaxLength(255).IsRequired();
            e.Property(x => x.SecretKey).HasMaxLength(255).IsRequired();
            e.Property(x => x.EventTypes).HasColumnType("text").IsRequired();
            e.Property(x => x.IsActive).HasDefaultValue(true);
            e.Property(x => x.CreatedAt).IsRequired();
            e.Property(x => x.UpdatedAt).IsRequired();
            e.HasIndex(x => x.TenantId);
            e.HasIndex(x => new { x.TenantId, x.IsActive });
        });

        modelBuilder.Entity<WebhookDelivery>(e =>
        {
            e.ToTable("WebhookDeliveries");
            e.HasKey(x => x.Id);
            e.Property(x => x.TenantId).IsRequired();
            e.Property(x => x.SubscriptionId).IsRequired();
            e.Property(x => x.EventType).HasMaxLength(50).IsRequired();
            e.Property(x => x.Payload).HasColumnType("text").IsRequired();
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.Property(x => x.RetryCount).HasDefaultValue(0);
            e.Property(x => x.NextAttemptAt).IsRequired();
            e.Property(x => x.TraceId).HasMaxLength(50).IsRequired();
            e.Property(x => x.LastResponseCode).IsRequired(false);
            e.Property(x => x.LastError).HasColumnType("text").IsRequired(false);
            e.Property(x => x.CreatedAt).IsRequired();
            e.Property(x => x.UpdatedAt).IsRequired();
            // Compound index cho worker query
            e.HasIndex(x => new { x.Status, x.NextAttemptAt });
            e.HasIndex(x => x.TenantId);
            e.HasIndex(x => x.SubscriptionId);
            e.HasIndex(x => x.TraceId);
            // Navigation
            e.HasOne(x => x.Subscription)
             .WithMany()
             .HasForeignKey(x => x.SubscriptionId)
             .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
