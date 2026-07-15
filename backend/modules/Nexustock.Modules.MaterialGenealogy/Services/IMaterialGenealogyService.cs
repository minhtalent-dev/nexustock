using Nexustock.Modules.MaterialGenealogy.DTOs;

namespace Nexustock.Modules.MaterialGenealogy.Services;

public interface IMaterialGenealogyService
{
    Task CreateRelationAsync(Guid tenantId, string username, CreateLotRelationDto dto);
    Task<LotGenealogyNodeDto> GetLotTreeAsync(Guid tenantId, string lotNo);
    Task HoldBranchAsync(Guid tenantId, string username, HoldBranchDto dto);
}
