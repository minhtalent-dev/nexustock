using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nexustock.Modules.Replenishment.Entities;

[Table("Lots")]
public class Lot
{
    [Key]
    [Column("Id")]
    public Guid Id { get; set; }

    [Column("TenantId")]
    public Guid TenantId { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("LotNo")]
    public string LotNo { get; set; } = null!;

    [Column("ItemId")]
    public Guid ItemId { get; set; }

    [Column("ExpiryDate")]
    public DateTime? ExpiryDate { get; set; }

    [Column("ProductionDate")]
    public DateTime? ProductionDate { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("QcStatus")]
    public string QcStatus { get; set; } = null!;
}
