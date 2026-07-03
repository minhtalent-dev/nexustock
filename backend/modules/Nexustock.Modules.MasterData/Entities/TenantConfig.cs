using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nexustock.Modules.MasterData.Entities;

[Table("tenant_configs")]
public class TenantConfig
{
    [Key]
    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("fifo_policy_level")]
    public int FifoPolicyLevel { get; set; } = 2; // 0: Tắt, 1: Cảnh báo, 2: Chặn cứng

    [Required]
    [MaxLength(100)]
    [Column("lot_no_pattern")]
    public string LotNoPattern { get; set; } = "{YYYY}{MM}{DD}-{SEQ}";

    [Column("allow_negative_stock")]
    public bool AllowNegativeStock { get; set; } = false;

    [Column("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }

    [MaxLength(100)]
    [Column("updated_by")]
    public string? UpdatedBy { get; set; }

    [ForeignKey("TenantId")]
    public virtual Tenant? Tenant { get; set; }
}
