using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nexustock.Modules.Inventory.Entities;

[Table("inventory_transactions")]
public class InventoryTransaction
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("item_id")]
    public Guid ItemId { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("lot_no")]
    public string LotNo { get; set; } = null!;

    [Column("location_id")]
    public Guid LocationId { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("transaction_type")]
    public string TransactionType { get; set; } = null!; // 'RECEIVE', 'MOVE_OUT', 'MOVE_IN', 'ADJUST_ADD', 'ADJUST_SUB'

    [Column("qty")]
    public decimal Qty { get; set; }

    [MaxLength(100)]
    [Column("trace_id")]
    public string? TraceId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("created_by")]
    public string CreatedBy { get; set; } = null!;
}
