using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Nexustock.Modules.Inventory.Dtos;

public class ScanValidateRequestDto
{
    [Required]
    public string Barcode { get; set; } = null!;
    [Required]
    public string Context { get; set; } = null!; // "LOCATION", "LOT", "ITEM"
}

public class OfflineSyncItemDto
{
    [Required]
    public string ClientOperationId { get; set; } = null!;
    [Required]
    public string StepType { get; set; } = null!; // "MOVE", "COUNT"
    [Required]
    public string Payload { get; set; } = null!; // JSON String
}

public class OfflineSyncRequestDto
{
    [Required]
    public List<OfflineSyncItemDto> Operations { get; set; } = new();
}

public class MobileTaskDto
{
    public Guid Id { get; set; }
    public string ReferenceType { get; set; } = null!;
    public Guid ReferenceId { get; set; }
    public string Step { get; set; } = null!;
    public Guid? LocationId { get; set; }
    public string? AssignedUser { get; set; }
    public string Status { get; set; } = null!;
}
