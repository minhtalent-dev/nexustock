using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nexustock.Modules.MasterData.Contexts;

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

    protected IntegrationTestBase()
    {
        _factory = new CustomWebApplicationFactory();
        _scope = _factory.Services.CreateScope();
        Services = _scope.ServiceProvider;

        // Đảm bảo DB được tạo và seed data
        var db = Services.GetRequiredService<MasterDataDbContext>();
        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();

        var identityDb = Services.GetRequiredService<Nexustock.Modules.Identity.Contexts.IdentityDbContext>();
        identityDb.Database.EnsureCreated();

        Client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _scope.Dispose();
        Client.Dispose();
        _factory.Dispose();
    }
}
