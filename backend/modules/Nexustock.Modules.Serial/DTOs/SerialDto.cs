using System;

namespace Nexustock.Modules.Serial.DTOs;

public class SerialDto
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public string SerialNo { get; set; } = string.Empty;
    public Guid LocationId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}
