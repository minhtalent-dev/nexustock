using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexustock.Modules.Allocation.Services;
using Nexustock.Modules.Allocation.Jobs;

namespace Nexustock.Modules.Allocation;

public static class DependencyInjection
{
    public static IServiceCollection AddAllocationModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IAllocationService, AllocationService>();
        
        // Đăng ký Background Worker xử lý giải phóng giữ hàng hết hạn
        services.AddHostedService<ReservationExpiryWorker>();

        return services;
    }
}
