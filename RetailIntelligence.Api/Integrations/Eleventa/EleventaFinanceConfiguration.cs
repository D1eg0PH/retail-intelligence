using Microsoft.EntityFrameworkCore;
using RetailIntelligence.Api.Integrations.Eleventa;

namespace RetailIntelligence.Api.Data;

public partial class AppDbContext
{
    public DbSet<EleventaFinanceEntry> EleventaFinanceEntries => Set<EleventaFinanceEntry>();
}

internal static class EleventaFinanceConfiguration
{
    internal static void Configure(ModelBuilder builder)
    {
        builder.Entity<EleventaFinanceEntry>(e =>
        {
            e.ToTable("eleventa_finance_entries");
            e.HasKey(x => new { x.SourceId, x.EntryId });
            e.Property(x => x.SourceId).HasColumnName("source_id");
            e.Property(x => x.EntryId).HasColumnName("entry_id");
            e.Property(x => x.Kind).HasColumnName("kind").HasMaxLength(30);
            e.Property(x => x.OccurredOn).HasColumnName("occurred_on");
            e.Property(x => x.Amount).HasColumnName("amount").HasPrecision(19, 2);
            e.Property(x => x.Category).HasColumnName("category").HasMaxLength(30);
            e.Property(x => x.Description).HasColumnName("description").HasMaxLength(250);
            e.Property(x => x.Counterparty).HasColumnName("counterparty").HasMaxLength(150);
            e.Property(x => x.Reference).HasColumnName("reference").HasMaxLength(100);
            e.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(1000);
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
            e.Property(x => x.VoidedAt).HasColumnName("voided_at").HasColumnType("timestamp with time zone");
            e.Property(x => x.VoidReason).HasColumnName("void_reason").HasMaxLength(300);
            e.HasIndex(x => new { x.SourceId, x.OccurredOn });
            e.HasOne<EleventaSource>().WithMany().HasForeignKey(x => x.SourceId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
