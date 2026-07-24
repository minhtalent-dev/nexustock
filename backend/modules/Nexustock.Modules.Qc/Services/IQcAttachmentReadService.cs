using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nexustock.Modules.Files.Dtos;

namespace Nexustock.Modules.Qc.Services;

public interface IQcAttachmentReadService
{
    Task<Dictionary<Guid, List<AttachmentDto>>> GetAttachmentsByEntityIdsAsync(IEnumerable<Guid> entityIds, CancellationToken ct);
    Task<List<AttachmentDto>> GetAttachmentsByEntityIdAsync(Guid entityId, CancellationToken ct);
}