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
using Nexustock.Modules.Identity.Services;
using Nexustock.Modules.Files.Services;

namespace Nexustock.MasterData.IntegrationTests;

internal class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
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


            // Đăng ký Fake Services
            services.RemoveAll<IUserPermissionService>();
            services.AddSingleton<IUserPermissionService>(PermissionService);

            services.RemoveAll<ISecretProtector>();
            services.AddScoped<ISecretProtector, FakeSecretProtector>();

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
            options.UseInMemoryDatabase(_dbName));
    }
}

