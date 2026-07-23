using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexustock.Modules.Files.Contexts;
using Nexustock.Modules.Files.Providers;
using Nexustock.Modules.Files.Services;
using Nexustock.Modules.Files.Workers;

namespace Nexustock.Modules.Files;

public static class DependencyInjection
{
    public static IServiceCollection AddFilesModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");
        services.AddDbContext<FilesDbContext>(options =>
            options.UseNpgsql(connectionString, b => b.MigrationsAssembly(typeof(FilesDbContext).Assembly.FullName)));

        services.AddDataProtection();
        services.AddSingleton<FakeObjectStorageProvider>();
        services.AddScoped<ISecretProtector, DataProtectionSecretProtector>();
        services.AddScoped<IObjectStorageResolver, ObjectStorageResolver>();
        services.AddScoped<FileStorageService>();
        services.AddScoped<IFileStorageService>(sp => sp.GetRequiredService<FileStorageService>());
        services.AddScoped<IAttachmentService, AttachmentService>();
        services.AddScoped<IFileStorageSettingsService, FileStorageSettingsService>();
        services.AddScoped<IStorageMigrateService, StorageMigrateService>();
        services.AddHostedService<StorageMigrateWorker>();

        services.Configure<FormOptions>(o =>
        {
            o.MultipartBodyLengthLimit = 12 * 1024 * 1024;
        });

        return services;
    }
}
