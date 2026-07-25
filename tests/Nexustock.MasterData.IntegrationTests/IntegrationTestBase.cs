using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nexustock.Modules.MasterData.Contexts;
using Nexustock.Modules.Identity.Contexts;
using Nexustock.Modules.Files.Contexts;
using Nexustock.Modules.Qc.Contexts;
using Nexustock.Modules.Inventory.Contexts;

namespace Nexustock.MasterData.IntegrationTests;

/// <summary>
/// Test nền integration test với WebApplicationFactory.
/// Dùng in-memory database để không phụ thuộc môi trường thật.
/// </summary>
public class IntegrationTestBase : IDisposable
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly IServiceScope _scope;
    protected readonly IServiceProvider Services;
    protected readonly HttpClient Client;
    protected readonly FakeUserPermissionService PermissionService;

    protected IntegrationTestBase()
    {
        _factory = new CustomWebApplicationFactory();
        PermissionService = _factory.PermissionService;
        _scope = _factory.Services.CreateScope();
        Services = _scope.ServiceProvider;

        // Đảm bảo DB được tạo
        EnsureCreated<MasterDataDbContext>();
        EnsureCreated<IdentityDbContext>();
        EnsureCreated<FilesDbContext>();
        EnsureCreated<QcDbContext>();
        EnsureCreated<InventoryDbContext>();
        EnsureCreated<Nexustock.Modules.Inbound.Contexts.InboundDbContext>();
        EnsureCreated<Nexustock.Modules.Rma.Contexts.RmaDbContext>();

        Client = _factory.CreateClient();
    }



    private void EnsureCreated<TContext>() where TContext : DbContext
    {
        var db = Services.GetRequiredService<TContext>();
        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _scope.Dispose();
        Client.Dispose();
        _factory.Dispose();
    }
}

