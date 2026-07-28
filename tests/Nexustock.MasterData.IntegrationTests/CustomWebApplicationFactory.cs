using System.Linq;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nexustock.Modules.MasterData.Contexts;
using Nexustock.Modules.Identity.Contexts;
using Nexustock.Modules.Files.Contexts;
using Nexustock.Modules.Qc.Contexts;
using Nexustock.Modules.Inventory.Contexts;
using Nexustock.Modules.Inbound.Contexts;
using Nexustock.Modules.Rma.Contexts;
using Nexustock.Modules.Exceptions.Contexts;
using Nexustock.Modules.Lpn.Contexts;
using Nexustock.Modules.Wave.Contexts;
using Nexustock.Modules.Putaway.Contexts;
using Nexustock.Modules.CrossDocking.Contexts;
using Nexustock.Modules.Readiness.Contexts;
using Nexustock.Modules.Identity.Services;
using Nexustock.Modules.Files.Services;

namespace Nexustock.MasterData.IntegrationTests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private static readonly Microsoft.EntityFrameworkCore.Storage.InMemoryDatabaseRoot _dbRoot = new();
    private readonly string _dbName = $"TestDb_{Guid.NewGuid()}";
    public FakeUserPermissionService PermissionService { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureServices(services =>
        {
            // Thay thế DbContext sang InMemory DB cô lập
            ReplaceDbContext<MasterDataDbContext>(services);
            ReplaceDbContext<IdentityDbContext>(services);
            ReplaceDbContext<FilesDbContext>(services);
            ReplaceDbContext<QcDbContext>(services);
            ReplaceDbContext<InventoryDbContext>(services);
            ReplaceDbContext<InboundDbContext>(services);
            ReplaceDbContext<RmaDbContext>(services);
            ReplaceDbContext<ExceptionsDbContext>(services);
            ReplaceDbContext<LpnDbContext>(services);
            ReplaceDbContext<WaveDbContext>(services);
            ReplaceDbContext<PutawayDbContext>(services);
            ReplaceDbContext<CrossDockingDbContext>(services);
            ReplaceDbContext<ReadinessDbContext>(services);

            // Đăng ký Fake Services
            services.RemoveAll<IUserPermissionService>();
            services.AddSingleton<IUserPermissionService>(PermissionService);

            services.RemoveAll<ISecretProtector>();
            services.AddScoped<ISecretProtector, FakeSecretProtector>();

            // Bật Thumbnail và Backfill cho integration tests
            services.Configure<ThumbnailOptions>(options =>
            {
                options.Enabled = true;
                options.BackfillEnabled = true;
                options.JpegQuality = 82;
                options.MaxEdge = 256;
                options.MaxDimension = 5000;
                options.MaxPixels = 25000000;
                options.BatchSize = 50;
                options.MaxRetriesPerRun = 3;
            });

            // Loại bỏ background workers của Files module để tránh chạy ngầm làm sập Host
            var hostedServices = services.Where(d => d.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService)).ToList();
            foreach (var service in hostedServices)
            {
                var implType = service.ImplementationType?.FullName ?? "";
                if (implType.Contains("Nexustock.Modules.Files.Workers"))
                {
                    services.Remove(service);
                }
            }

            // Decorate ObjectStorageResolver cho việc giả lập lỗi storage provider
            var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IObjectStorageResolver));
            if (descriptor != null)
            {
                services.Remove(descriptor);
                var innerType = descriptor.ImplementationType ?? typeof(Nexustock.Modules.Files.Services.ObjectStorageResolver);
                services.AddScoped(innerType);
                services.AddScoped<IObjectStorageResolver>(sp => 
                    new TestObjectStorageResolver((IObjectStorageResolver)sp.GetRequiredService(innerType)));
            }

            // Override Authentication Handler thành TestAuth
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthConstants.Scheme;
                options.DefaultChallengeScheme = TestAuthConstants.Scheme;
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthConstants.Scheme, _ => { });
        });
    }

    private void ReplaceDbContext<TContext>(IServiceCollection services) where TContext : DbContext
    {
        services.RemoveAll<DbContextOptions<TContext>>();
        services.RemoveAll<TContext>();
        services.AddDbContext<TContext>(options =>
            options.UseInMemoryDatabase(_dbName, _dbRoot));
    }
}

