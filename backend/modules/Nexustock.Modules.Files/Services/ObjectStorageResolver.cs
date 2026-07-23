using Microsoft.Extensions.Configuration;
using Nexustock.Modules.Files.Entities;
using Nexustock.Modules.Files.Providers;
using System.Text.Json;

namespace Nexustock.Modules.Files.Services;

public interface IObjectStorageResolver
{
    IObjectStorageProvider Resolve(FileStorageSettings settings, string? providerOverride = null, string? configJsonPlain = null);
    IObjectStorageProvider ResolveByProviderId(string providerId, FileStorageSettings settings, string? configJsonPlain = null);
}

public sealed class ObjectStorageResolver : IObjectStorageResolver
{
    private readonly IConfiguration _configuration;
    private readonly ISecretProtector _protector;
    private readonly FakeObjectStorageProvider _fake;

    public ObjectStorageResolver(IConfiguration configuration, ISecretProtector protector, FakeObjectStorageProvider fake)
    {
        _configuration = configuration;
        _protector = protector;
        _fake = fake;
    }

    public IObjectStorageProvider Resolve(FileStorageSettings settings, string? providerOverride = null, string? configJsonPlain = null)
        => ResolveByProviderId(providerOverride ?? settings.ActiveProvider, settings, configJsonPlain);

    public IObjectStorageProvider ResolveByProviderId(string providerId, FileStorageSettings settings, string? configJsonPlain = null)
    {
        var id = (providerId ?? StorageProviderIds.Local).Trim().ToUpperInvariant();
        var plain = configJsonPlain;
        if (plain == null && !string.IsNullOrWhiteSpace(settings.ConfigJsonEncrypted))
        {
            try { plain = _protector.Unprotect(settings.ConfigJsonEncrypted); }
            catch { throw new InvalidOperationException("STORAGE_CONFIG_INVALID"); }
        }
        plain ??= "{}";

        return id switch
        {
            StorageProviderIds.Local => new LocalObjectStorageProvider(_configuration, settings.LocalPathOverride),
            StorageProviderIds.Fake => _fake,
            StorageProviderIds.AwsS3 => new AwsS3ObjectStorageProvider(plain),
            StorageProviderIds.AzureBlob => new AzureBlobObjectStorageProvider(plain),
            StorageProviderIds.Gcs => new GcsObjectStorageProvider(plain),
            StorageProviderIds.CloudflareR2 => new CloudflareR2ObjectStorageProvider(plain),
            _ => throw new InvalidOperationException("STORAGE_CONFIG_INVALID")
        };
    }
}
