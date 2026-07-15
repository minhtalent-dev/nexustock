using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nexustock.Modules.Wave.DTOs;

namespace Nexustock.Modules.Wave.Services;

public interface IWaveService
{
    Task<List<WaveListDto>> GetWavesAsync(Guid tenantId);
    Task<WaveDetailDto> GetWaveDetailsAsync(Guid tenantId, Guid waveId);
    Task<Guid> CreateWaveAsync(Guid tenantId, string username, CreateWaveDto dto);
    Task ReleaseWaveAsync(Guid tenantId, string username, Guid waveId);
    Task CompletePickTaskAsync(Guid tenantId, string username, CompleteWavePickDto dto);
    Task<SortResponseDto> SortItemAsync(Guid tenantId, Guid waveId, SortRequestDto dto);
    Task CompleteWaveAsync(Guid tenantId, string username, Guid waveId);
}
