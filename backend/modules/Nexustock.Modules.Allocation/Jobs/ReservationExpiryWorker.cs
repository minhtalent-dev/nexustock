using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nexustock.Modules.Inventory.Contexts;
using Nexustock.Modules.Inventory.Entities;

namespace Nexustock.Modules.Allocation.Jobs;

public class ReservationExpiryWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ReservationExpiryWorker> _logger;

    public ReservationExpiryWorker(IServiceProvider serviceProvider, ILogger<ReservationExpiryWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();

                // Quét tối đa 100 bản ghi hết hạn cùng lúc
                var expiredReservations = await dbContext.AllocationReservations
                    .Where(r => r.Status == "ACTIVE" && r.ExpiresAt < DateTime.UtcNow)
                    .Take(100)
                    .ToListAsync(stoppingToken);

                if (expiredReservations.Count > 0)
                {
                    using var transaction = await dbContext.Database.BeginTransactionAsync(stoppingToken);

                    var balanceIds = expiredReservations.Select(r => r.InventoryBalanceId).Distinct().ToList();
                    var balances = await dbContext.Inventories
                        .Where(i => balanceIds.Contains(i.Id))
                        .ToDictionaryAsync(i => i.Id, i => i, stoppingToken);

                    foreach (var res in expiredReservations)
                    {
                        res.Status = "EXPIRED";
                        res.UpdatedAt = DateTime.UtcNow;
                        res.UpdatedBy = "ExpiryWorker";

                        if (balances.TryGetValue(res.InventoryBalanceId, out var balance))
                        {
                            balance.QtyReserved = Math.Max(0, balance.QtyReserved - res.Qty);
                            balance.UpdatedAt = DateTime.UtcNow;
                            balance.UpdatedBy = "ExpiryWorker";
                            balance.RowVersion += 1;
                        }
                    }

                    await dbContext.SaveChangesAsync(stoppingToken);
                    await transaction.CommitAsync(stoppingToken);
                    _logger.LogInformation("Đã tự động giải phóng {Count} bản ghi giữ hàng hết hạn.", expiredReservations.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xảy ra trong quá trình xử lý giải phóng giữ hàng hết hạn.");
            }

            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}
