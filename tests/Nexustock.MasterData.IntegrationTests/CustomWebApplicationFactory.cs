using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nexustock.Modules.MasterData.Contexts;

namespace Nexustock.MasterData.IntegrationTests;

/// <summary>
/// Custom WebApplicationFactory dùng database InMemory để test.
/// Must be internal because base type parameter 'Program' is internal.
/// </summary>
internal class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"TestDb_{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureServices(services =>
        {
            // Xoá DbContextOptions<MasterDataDbContext> hiện có (Npgsql)
            services.RemoveAll<DbContextOptions<MasterDataDbContext>>();
            services.RemoveAll<MasterDataDbContext>();

            // Xoá DbContextOptions<IdentityDbContext> hiện có (Npgsql)
            services.RemoveAll<DbContextOptions<Nexustock.Modules.Identity.Contexts.IdentityDbContext>>();
            services.RemoveAll<Nexustock.Modules.Identity.Contexts.IdentityDbContext>();

            // Thay bằng InMemory
            services.AddDbContext<MasterDataDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));

            services.AddDbContext<Nexustock.Modules.Identity.Contexts.IdentityDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));
        });
    }
}
