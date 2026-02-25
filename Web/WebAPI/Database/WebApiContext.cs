using Database;
using Microsoft.EntityFrameworkCore;
using WebApiPlugin.Database.Entities;

namespace WebApiPlugin.Database;

/// <summary>
/// EF context for WebApi plugin: AuditLog only (same DB as DuckSoup/ProxyDb).
/// CORS is in the core database (API.Database.Context.DuckSoup).
/// Connection string must be set before use: DuckContext.ConnectionStrings[typeof(WebApiContext)] = "..."
/// </summary>
public partial class WebApiContext : DuckContext
{
    public virtual DbSet<AuditEntryEntity> AuditEntry { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditEntryEntity>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_dbo.AuditEntry");
            entity.ToTable("AuditEntry");
            entity.Property(e => e.Timestamp).IsRequired();
            entity.Property(e => e.Action).IsRequired();
            entity.Property(e => e.Username).IsRequired();
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
