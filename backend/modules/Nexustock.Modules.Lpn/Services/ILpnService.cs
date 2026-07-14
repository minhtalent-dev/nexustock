using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nexustock.Modules.Lpn.Dtos;

namespace Nexustock.Modules.Lpn.Services;

public interface ILpnService
{
    Task<LpnDto> CreateLpnAsync(Guid tenantId, CreateLpnDto dto, string username);
    Task<bool> AttachToLpnAsync(Guid tenantId, Guid lpnId, AttachLpnDto dto, string username);
    Task<bool> DetachFromLpnAsync(Guid tenantId, Guid lpnId, DetachLpnDto dto, string username);
    Task<bool> MoveLpnAsync(Guid tenantId, Guid lpnId, MoveLpnDto dto, string username);
    Task<List<LpnDto>> GetLpnsAsync(Guid tenantId);
    Task<List<LpnEventDto>> GetLpnEventsAsync(Guid tenantId, Guid lpnId);
}

public class LpnEventDto
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = null!;
    public Guid? ItemId { get; set; }
    public string? ItemCode { get; set; }
    public string? ItemName { get; set; }
    public string? LotNo { get; set; }
    public decimal? Qty { get; set; }
    public string? FromLocationCode { get; set; }
    public string? ToLocationCode { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = null!;
}
