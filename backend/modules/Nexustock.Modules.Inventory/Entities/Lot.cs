using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nexustock.Modules.Inventory.Entities;

[Table("Lots")]
public class Lot
{
    [Key]
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    [Required]
    [MaxLength(100)]
    public string LotNo { get; set; } = null!;

    public Guid ItemId { get; set; }

    [Required]
    [MaxLength(50)]
    public string QcStatus { get; set; } = null!; // 'Unspec', 'Release', 'Hold', 'Reject'
}
