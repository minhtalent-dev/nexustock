using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nexustock.Modules.MasterData.Contexts;

namespace Nexustock.MasterData.IntegrationTests;

/// <summary>
/// Custom WebApplicationFactory để tạo host dùng InMemory database
/// Tránh phụ thuộc vào PostgreSQL và .env khi test
/// </summary>
internal class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"NexustockTest_{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Tránh load .env và kết nối DB thật
        builder.UseEnvironment("Test");

        builder.ConfigureServices(services =>
        {
            // Xoá DbContextOptions<MasterDataDbContext> hiện có (Npgsql)
            services.RemoveAll<DbContextOptions<MasterDataDbContext>>();
            services.RemoveAll<MasterDataDbContext>();

            // Thay bằng InMemory
            services.AddDbContext<MasterDataDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));
        });
    }
}
