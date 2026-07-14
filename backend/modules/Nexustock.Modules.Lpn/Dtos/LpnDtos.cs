using System;

namespace Nexustock.Modules.Lpn.Dtos;

public class CreateLpnDto
{
    public string LpnNo { get; set; } = null!;
    public Guid LocationId { get; set; }
}

public class AttachLpnDto
{
    public Guid ItemId { get; set; }
    public string LotNo { get; set; } = null!;
    public decimal Qty { get; set; }
}

public class DetachLpnDto
{
    public Guid ItemId { get; set; }
    public string LotNo { get; set; } = null!;
    public decimal Qty { get; set; }
}

public class MoveLpnDto
{
    public Guid TargetLocationId { get; set; }
}

public class LpnDto
{
    public Guid Id { get; set; }
    public string LpnNo { get; set; } = null!;
    public Guid LocationId { get; set; }
    public string Status { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}
