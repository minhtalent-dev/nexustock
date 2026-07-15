using System;
using System.Collections.Generic;

namespace Nexustock.Modules.Rma.Entities;

public class RmaRequest
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string RmaNo { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public string? ReferenceNo { get; set; }
    public string Status { get; set; } = "OPEN"; // OPEN, RECEIVED, QC_COMPLETED, CLOSED, CANCELLED
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public int RowVersion { get; set; } = 1;
    public List<RmaItem> Items { get; set; } = new();
}
