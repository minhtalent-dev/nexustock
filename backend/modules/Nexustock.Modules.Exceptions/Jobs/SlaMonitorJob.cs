using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nexustock.Modules.Exceptions.Contexts;
using Nexustock.Modules.Exceptions.Entities;

namespace Nexustock.Modules.Exceptions.Jobs;

public class SlaMonitorJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SlaMonitorJob> _logger;

    public SlaMonitorJob(IServiceScopeFactory scopeFactory, ILogger<SlaMonitorJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Exceptions SLA Monitor Job starting...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<ExceptionsDbContext>();
                    
                    // Tìm các Assignment Pending và quá hạn SLA
                    var overdueAssignments = await context.ExceptionAssignments
                        .IgnoreQueryFilters() // Job chạy nền quét toàn bộ Tenants
                        .Where(a => a.Status == "Pending" && a.SlaDeadline.HasValue && a.SlaDeadline < DateTime.UtcNow)
                        .ToListAsync(stoppingToken);

                    if (overdueAssignments.Any())
                    {
                        _logger.LogWarning("Phat hien {Count} assignments qua han SLA. Tien hanh cap nhat sang Overdue...", overdueAssignments.Count);

                        foreach (var assignment in overdueAssignments)
                        {
                            assignment.Status = "Overdue";

                            // Thêm ExceptionEvent cảnh báo
                            var @event = new ExceptionEvent
                            {
                                Id = Guid.NewGuid(),
                                TenantId = assignment.TenantId,
                                ExceptionId = assignment.ExceptionId,
                                Transition = "OVERDUE",
                                Actor = "SystemSlaMonitor",
                                Note = $"Qua han SLA xu ly cua {assignment.Owner}",
                                CreatedAt = DateTime.UtcNow
                            };
                            context.ExceptionEvents.Add(@event);
                        }

                        await context.SaveChangesAsync(stoppingToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Loi xay ra khi chay SLA Monitor Job");
            }

            // Chạy kiểm tra mỗi 5 giây để phục vụ test nhanh
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
