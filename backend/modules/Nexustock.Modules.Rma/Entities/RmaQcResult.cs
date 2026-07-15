using System;

namespace Nexustock.Modules.Rma.Entities;

public class RmaQcResult
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid RmaItemId { get; set; }
    public string QcStatus { get; set; } = string.Empty; // PASS, FAIL
    public string Disposition { get; set; } = string.Empty; // RESTOCK, QUARANTINE, SCRAP, REPAIR
    public decimal Qty { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}
