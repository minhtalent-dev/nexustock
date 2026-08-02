using System;
using System.Net.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nexustock.Modules.Identity.Contexts;
using Nexustock.Modules.MasterData.Contexts;
using Nexustock.Modules.Files.Contexts;
using Nexustock.Modules.Qc.Contexts;
using Nexustock.Modules.Inventory.Contexts;
using Nexustock.Modules.Exceptions.Contexts;
using Nexustock.Modules.Lpn.Contexts;
using Nexustock.Modules.Wave.Contexts;
using Nexustock.Modules.Putaway.Contexts;
using Nexustock.Modules.CrossDocking.Contexts;
using Nexustock.Modules.Readiness.Contexts;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Nexustock.MasterData.IntegrationTests;

/// <summary>
/// Test nền integration test với WebApplicationFactory.
/// Dùng in-memory database để không phụ thuộc môi trường thật.
/// </summary>
public class IntegrationTestBase : IClassFixture<CustomWebApplicationFactory>, IDisposable
{
    protected readonly CustomWebApplicationFactory Factory;
    private readonly IServiceScope _scope;
    protected readonly IServiceProvider Services;
    protected readonly HttpClient Client;
    protected readonly FakeUserPermissionService PermissionService;

    // Mỗi test class nhận fixture riêng từ xUnit để cô lập host và InMemory DB.
    protected IntegrationTestBase(CustomWebApplicationFactory factory)
    {
        Factory = factory;
        PermissionService = factory.PermissionService;
        _scope = factory.Services.CreateScope();
        Services = _scope.ServiceProvider;

        // Đảm bảo DB được tạo
        EnsureCreated<MasterDataDbContext>();
        EnsureCreated<IdentityDbContext>();
        EnsureCreated<FilesDbContext>();
        EnsureCreated<QcDbContext>();
        EnsureCreated<InventoryDbContext>();
        EnsureCreated<Nexustock.Modules.Inbound.Contexts.InboundDbContext>();
        EnsureCreated<Nexustock.Modules.Rma.Contexts.RmaDbContext>();
        EnsureCreated<ExceptionsDbContext>();
        EnsureCreated<LpnDbContext>();
        EnsureCreated<WaveDbContext>();
        EnsureCreated<PutawayDbContext>();
        EnsureCreated<CrossDockingDbContext>();
        EnsureCreated<ReadinessDbContext>();
        EnsureCreated<Nexustock.Modules.Replenishment.Contexts.ReplenishmentDbContext>();

        Client = factory.CreateClient();
    }

    private void EnsureCreated<TContext>() where TContext : DbContext
    {
        var db = Services.GetRequiredService<TContext>();
        db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _scope.Dispose();
    }
}
