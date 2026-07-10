using System;

namespace Nexustock.Modules.Inbound.Entities;

public enum LotQcStatus
{
    Unspec,
    Hold,
    Release,
    Reject
}

public class Lot
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string LotNo { get; set; } = null!;
    public Guid ItemId { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public DateTime? ProductionDate { get; set; }
    public LotQcStatus QcStatus { get; set; }
}
