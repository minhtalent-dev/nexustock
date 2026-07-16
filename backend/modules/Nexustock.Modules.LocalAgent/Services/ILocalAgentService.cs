using System;
using System.Threading.Tasks;
using Nexustock.Modules.LocalAgent.DTOs;

namespace Nexustock.Modules.LocalAgent.Services;

public interface ILocalAgentService
{
    Task<PairingCodeResponseDto> GeneratePairingCodeAsync(Guid tenantId, string username, GeneratePairingCodeRequestDto dto);
    Task<ConfirmPairResponseDto> ConfirmPairAsync(ConfirmPairRequestDto dto);
    Task<HeartbeatResponseDto> HeartbeatAsync(Guid stationId, string token, HeartbeatRequestDto dto);
    Task<PaginatedListDto<StationResponseDto>> GetStationsAsync(Guid tenantId, int page, int pageSize, string? search);
    Task RevokeStationAsync(Guid tenantId, Guid stationId, RevokeStationRequestDto dto);
}
