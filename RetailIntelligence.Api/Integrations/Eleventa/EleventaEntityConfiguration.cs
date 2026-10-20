using Microsoft.EntityFrameworkCore;

using RetailIntelligence.Api.Integrations.Eleventa;

namespace RetailIntelligence.Api.Data;

public partial class AppDbContext
{
    public DbSet<EleventaSource> EleventaSources => Set<EleventaSource>();
    public DbSet<EleventaSyncBatch> EleventaSyncBatches => Set<EleventaSyncBatch>();
    public DbSet<EleventaProduct> EleventaProducts => Set<EleventaProduct>();
    public DbSet<EleventaInventorySnapshot> EleventaInventorySnapshots => Set<EleventaInventorySnapshot>();
    public DbSet<EleventaInventoryRow> EleventaInventoryRows => Set<EleventaInventoryRow>();
    public DbSet<EleventaSale> EleventaSales => Set<EleventaSale>();
    public DbSet<EleventaSaleLine> EleventaSaleLines => Set<EleventaSaleLine>();
    public DbSet<EleventaReturn> EleventaReturns => Set<EleventaReturn>();
    public DbSet<EleventaReturnLine> EleventaReturnLines => Set<EleventaReturnLine>();
}

internal static class EleventaEntityConfiguration
{
    internal static void Configure(ModelBuilder b)
    {
        b.Entity<EleventaSource>(e =>
        {
            e.ToTable("eleventa_sources"); e.HasKey(x => x.SourceId);
            e.Property(x => x.SourceId).HasColumnName("source_id"); e.Property(x => x.Name).HasColumnName("name").HasMaxLength(120);
            e.Property(x => x.CredentialSha256).HasColumnName("credential_sha256").HasMaxLength(64).IsFixedLength();
            e.Property(x => x.IsActive).HasColumnName("is_active"); e.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
            e.Property(x => x.LastConfirmedAt).HasColumnName("last_confirmed_at").HasColumnType("timestamp with time zone"); e.HasIndex(x => x.CredentialSha256).IsUnique();
        });
        b.Entity<EleventaSyncBatch>(e =>
        {
            e.ToTable("eleventa_sync_batches"); e.HasKey(x => x.Id); e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.SourceId).HasColumnName("source_id"); e.Property(x => x.BatchId).HasColumnName("batch_id");
            e.Property(x => x.ContractVersion).HasColumnName("contract_version"); e.Property(x => x.BatchKind).HasColumnName("batch_kind").HasMaxLength(24);
            e.Property(x => x.PayloadSha256).HasColumnName("payload_sha256").HasMaxLength(64).IsFixedLength(); e.Property(x => x.RecordCount).HasColumnName("record_count");
            e.Property(x => x.AcceptedAt).HasColumnName("accepted_at").HasColumnType("timestamp with time zone"); e.HasIndex(x => new { x.SourceId, x.BatchId }).IsUnique();
        });
        b.Entity<EleventaProduct>(e =>
        {
            e.ToTable("eleventa_products"); e.HasKey(x => new { x.SourceId, x.SourceProductId }); e.Property(x => x.SourceId).HasColumnName("source_id");
            e.Property(x => x.SourceProductId).HasColumnName("source_product_id"); e.Property(x => x.CodeOriginal).HasColumnName("code_original").HasMaxLength(50);
            e.Property(x => x.DescriptionOriginal).HasColumnName("description_original").HasMaxLength(255);
            e.Property(x => x.Cost).HasColumnName("cost").HasPrecision(19,4); e.Property(x => x.SalePrice).HasColumnName("sale_price").HasPrecision(19,4);
            e.Property(x => x.WholesalePrice).HasColumnName("wholesale_price").HasPrecision(19,4); e.Property(x => x.SourceDepartmentId).HasColumnName("source_department_id");
            e.Property(x => x.SourceSupplierId).HasColumnName("source_supplier_id"); e.Property(x => x.CheckedAt).HasColumnName("checked_at").HasColumnType("timestamp without time zone"); e.Property(x => x.DeletedAt).HasColumnName("deleted_at").HasColumnType("timestamp without time zone");
            e.Property(x => x.IsKit).HasColumnName("is_kit"); e.Property(x => x.TracksInventory).HasColumnName("tracks_inventory"); e.Property(x => x.CreatedAtOrigin).HasColumnName("created_at_origin").HasColumnType("timestamp without time zone");
            e.Property(x => x.CreatedAtSource).HasColumnName("created_at_source").HasMaxLength(40); e.Property(x => x.CreatedAtConfidence).HasColumnName("created_at_confidence").HasMaxLength(20);
            e.Property(x => x.FirstSaleAt).HasColumnName("first_sale_at").HasColumnType("timestamp without time zone"); e.Property(x => x.FirstInventoryMovementAt).HasColumnName("first_inventory_movement_at").HasColumnType("timestamp without time zone");
            e.Property(x => x.ObservedAt).HasColumnName("observed_at").HasColumnType("timestamp with time zone");
        });
        b.Entity<EleventaInventorySnapshot>(e =>
        {
            e.ToTable("eleventa_inventory_snapshots"); e.HasKey(x => new { x.SourceId, x.SnapshotId }); e.Property(x => x.SourceId).HasColumnName("source_id");
            e.Property(x => x.SnapshotId).HasColumnName("snapshot_id"); e.Property(x => x.ExtractedAt).HasColumnName("extracted_at").HasColumnType("timestamp with time zone");
            e.Property(x => x.ExpectedRecordCount).HasColumnName("expected_record_count"); e.Property(x => x.ReceivedRecordCount).HasColumnName("received_record_count");
            e.Property(x => x.IsComplete).HasColumnName("is_complete"); e.Property(x => x.ValidationStatus).HasColumnName("validation_status").HasMaxLength(20);
            e.Property(x => x.CompletedAt).HasColumnName("completed_at").HasColumnType("timestamp with time zone");
        });
        b.Entity<EleventaInventoryRow>(e =>
        {
            e.ToTable("eleventa_inventory_rows"); e.HasKey(x => new { x.SourceId, x.SnapshotId, x.SourceProductId, x.SourceWarehouseId });
            e.Property(x => x.SourceId).HasColumnName("source_id"); e.Property(x => x.SnapshotId).HasColumnName("snapshot_id");
            e.Property(x => x.SourceProductId).HasColumnName("source_product_id"); e.Property(x => x.SourceWarehouseId).HasColumnName("source_warehouse_id");
            e.Property(x => x.Quantity).HasColumnName("quantity").HasPrecision(24,10);
            e.HasOne<EleventaInventorySnapshot>().WithMany().HasForeignKey(x => new { x.SourceId, x.SnapshotId });
        });
        b.Entity<EleventaSale>(e =>
        {
            e.ToTable("eleventa_sales"); e.HasKey(x => new { x.SourceId, x.SourceTicketId }); e.Property(x => x.SourceId).HasColumnName("source_id");
            e.Property(x => x.SourceTicketId).HasColumnName("source_ticket_id"); e.Property(x => x.Folio).HasColumnName("folio");
            e.Property(x => x.CreatedAtOrigin).HasColumnName("created_at_origin").HasColumnType("timestamp without time zone"); e.Property(x => x.SoldAt).HasColumnName("sold_at").HasColumnType("timestamp without time zone"); e.Property(x => x.PaidAt).HasColumnName("paid_at").HasColumnType("timestamp without time zone");
            e.Property(x => x.Total).HasColumnName("total").HasPrecision(19,4); e.Property(x => x.Profit).HasColumnName("profit").HasPrecision(19,4);
            e.Property(x => x.IsOpen).HasColumnName("is_open"); e.Property(x => x.IsCancelled).HasColumnName("is_cancelled"); e.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(3).IsFixedLength();
            e.Property(x => x.ObservedAt).HasColumnName("observed_at").HasColumnType("timestamp with time zone"); e.HasIndex(x => new { x.SourceId, x.SoldAt });
        });
        b.Entity<EleventaSaleLine>(e =>
        {
            e.ToTable("eleventa_sale_lines"); e.HasKey(x => new { x.SourceId, x.SourceLineId }); e.Property(x => x.SourceId).HasColumnName("source_id");
            e.Property(x => x.SourceLineId).HasColumnName("source_line_id"); e.Property(x => x.SourceTicketId).HasColumnName("source_ticket_id");
            e.Property(x => x.SourceProductId).HasColumnName("source_product_id"); e.Property(x => x.ProductCodeOriginal).HasColumnName("product_code_original").HasMaxLength(20);
            e.Property(x => x.ProductNameOriginal).HasColumnName("product_name_original").HasMaxLength(255); e.Property(x => x.Quantity).HasColumnName("quantity").HasPrecision(24,10);
            e.Property(x => x.ReturnedQuantity).HasColumnName("returned_quantity").HasPrecision(24,10); e.Property(x => x.UsedPrice).HasColumnName("used_price").HasPrecision(19,4);
            e.Property(x => x.FinalPrice).HasColumnName("final_price").HasPrecision(19,4); e.Property(x => x.LineTotal).HasColumnName("line_total").HasPrecision(19,4);
            e.Property(x => x.AddedAt).HasColumnName("added_at").HasColumnType("timestamp without time zone"); e.Property(x => x.IsReturned).HasColumnName("is_returned");
            e.Property(x => x.PresentInLatestSource).HasColumnName("present_in_latest_source"); e.Property(x => x.ObservedAt).HasColumnName("observed_at").HasColumnType("timestamp with time zone");
            e.HasOne<EleventaSale>().WithMany().HasForeignKey(x => new { x.SourceId, x.SourceTicketId }); e.HasIndex(x => new { x.SourceId, x.SourceTicketId });
        });
        b.Entity<EleventaReturn>(e =>
        {
            e.ToTable("eleventa_returns"); e.HasKey(x => new { x.SourceId, x.SourceReturnId }); e.Property(x => x.SourceId).HasColumnName("source_id");
            e.Property(x => x.SourceReturnId).HasColumnName("source_return_id"); e.Property(x => x.SourceTicketId).HasColumnName("source_ticket_id");
            e.Property(x => x.ReturnedAt).HasColumnName("returned_at").HasColumnType("timestamp without time zone"); e.Property(x => x.PaidAt).HasColumnName("paid_at").HasColumnType("timestamp without time zone");
            e.Property(x => x.ReturnType).HasColumnName("return_type").HasMaxLength(1); e.Property(x => x.Cashier).HasColumnName("cashier").HasMaxLength(50);
            e.Property(x => x.RegisterName).HasColumnName("register_name").HasMaxLength(50); e.Property(x => x.Amount).HasColumnName("amount").HasPrecision(19,4);
            e.Property(x => x.DetailCount).HasColumnName("detail_count"); e.Property(x => x.DetailComplete).HasColumnName("detail_complete");
            e.Property(x => x.ObservedAt).HasColumnName("observed_at").HasColumnType("timestamp with time zone"); e.HasIndex(x => new { x.SourceId, x.SourceTicketId });
        });
        b.Entity<EleventaReturnLine>(e =>
        {
            e.ToTable("eleventa_return_lines"); e.HasKey(x => new { x.SourceId, x.SourceReturnLineId }); e.Property(x => x.SourceId).HasColumnName("source_id");
            e.Property(x => x.SourceReturnLineId).HasColumnName("source_return_line_id"); e.Property(x => x.SourceReturnId).HasColumnName("source_return_id");
            e.Property(x => x.SourceTicketId).HasColumnName("source_ticket_id"); e.Property(x => x.ProductCodeOriginal).HasColumnName("product_code_original").HasMaxLength(20);
            e.Property(x => x.ProductDescriptionOriginal).HasColumnName("product_description_original").HasMaxLength(255);
            e.Property(x => x.ReturnedQuantity).HasColumnName("returned_quantity").HasPrecision(24,10); e.Property(x => x.Amount).HasColumnName("amount").HasPrecision(19,4);
            e.HasOne<EleventaReturn>().WithMany().HasForeignKey(x => new { x.SourceId, x.SourceReturnId });
        });
    }
}
