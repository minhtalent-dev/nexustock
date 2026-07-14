using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nexustock.Modules.Replenishment.Entities;

namespace Nexustock.Modules.Replenishment.Services;

public interface IReplenishmentService
{
    Task<List<ReplenishmentTask>> GenerateTasksAsync(Guid tenantId, string strategy = "FEFO");
    Task<ReplenishmentTask> CompleteTaskAsync(Guid taskId, decimal actualQty, string operatorName);
    Task<ReplenishmentTask> CancelTaskAsync(Guid taskId, string operatorName);
}
