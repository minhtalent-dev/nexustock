using System;
using System.ComponentModel.DataAnnotations;

namespace Nexustock.Modules.Serial.DTOs;

public class ReceiveSerialDto
{
    [Required]
    public Guid ItemId { get; set; }

    [Required]
    [MaxLength(100)]
    public string SerialNo { get; set; } = string.Empty;

    [Required]
    public Guid LocationId { get; set; }
}
