using System;

namespace Nexustock.Modules.Inventory.Entities;

public class MobileDevice
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string DeviceCode { get; set; } = null!;
    public string? Station { get; set; }
    public string Status { get; set; } = "Active";
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = null!;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

public class ScanEvent
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Context { get; set; } = null!;
    public string Barcode { get; set; } = null!;
    public string Result { get; set; } = null!;
    public int LatencyMs { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = null!;
}

public class OfflineOperation
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string ClientOperationId { get; set; } = null!;
    public string Payload { get; set; } = null!;
    public string SyncStatus { get; set; } = "Pending";
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = null!;
}

public class MobileTask
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string ReferenceType { get; set; } = null!;
    public Guid ReferenceId { get; set; }
    public string Step { get; set; } = null!;
    public Guid? LocationId { get; set; }
    public string? AssignedUser { get; set; }
    public string Status { get; set; } = "Open";
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = null!;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
