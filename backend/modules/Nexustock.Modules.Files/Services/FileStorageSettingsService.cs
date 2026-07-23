using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Nexustock.Modules.Files.Contexts;
using Nexustock.Modules.Files.Dtos;
using Nexustock.Modules.Files.Entities;
using Nexustock.Modules.Files.Providers;
using System.Text.Json;

namespace Nexustock.Modules.Files.Services;

public interface IFileStorageSettingsService
{
    Task<StorageSettingsDto> GetAsync(CancellationToken ct);
    Task<StorageSettingsDto> UpsertAsync(UpsertStorageSettingsRequest request, string? user, CancellationToken ct);
    Task<StorageTestResultDto> TestAsync(UpsertStorageSettingsRequest? draft, CancellationToken ct);
}

public sealed class FileStorageSettingsService : IFileStorageSettingsService
{
    private static readonly string[] ProviderOrder =
    {
        StorageProviderIds.Local, StorageProviderIds.AwsS3, StorageProviderIds.AzureBlob,
        StorageProviderIds.Gcs, StorageProviderIds.CloudflareR2
    };

    private static readonly HashSet<string> SecretKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "secretAccessKey", "accessKeyId", "connectionString", "accountKey", "serviceAccountJson"
    };

    private readonly FilesDbContext _db;
    private readonly FileStorageService _storage;
    private readonly IObjectStorageResolver _resolver;
    private readonly ISecretProtector _protector;
    private readonly IConfiguration _configuration;
    private readonly ILogger<FileStorageSettingsService> _logger;

    public FileStorageSettingsService(
        FilesDbContext db,
        FileStorageService storage,
        IObjectStorageResolver resolver,
        ISecretProtector protector,
        IConfiguration configuration,
        ILogger<FileStorageSettingsService> logger)
    {
        _db = db;
        _storage = storage;
        _resolver = resolver;
        _protector = protector;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<StorageSettingsDto> GetAsync(CancellationToken ct)
    {
        var settings = await _storage.GetOrCreateSettingsAsync(ct);
        return ToDto(settings);
    }

    public async Task<StorageSettingsDto> UpsertAsync(UpsertStorageSettingsRequest request, string? user, CancellationToken ct)
    {
        var settings = await _storage.GetOrCreateSettingsAsync(ct);
        var targetProvider = string.IsNullOrWhiteSpace(request.ActiveProvider)
            ? settings.ActiveProvider
            : request.ActiveProvider.Trim().ToUpperInvariant();

        if (!IsKnownProvider(targetProvider))
            throw new FileDomainException("STORAGE_CONFIG_INVALID", "Unknown provider");

        if (request.Activate && !string.Equals(targetProvider, StorageProviderIds.Local, StringComparison.OrdinalIgnoreCase))
        {
            if (settings.LastTestOk != true)
                throw new FileDomainException("STORAGE_TEST_REQUIRED", "Test connection must succeed before activating cloud provider");
        }

        if (request.PublicBaseUrl != null)
            settings.PublicBaseUrl = string.IsNullOrWhiteSpace(request.PublicBaseUrl) ? null : request.PublicBaseUrl.Trim().TrimEnd('/');

        if (request.LocalPathOverride != null)
            settings.LocalPathOverride = string.IsNullOrWhiteSpace(request.LocalPathOverride) ? null : request.LocalPathOverride;

        if (request.Config != null)
        {
            var merged = MergeConfig(settings, request.Config);
            settings.ConfigJsonEncrypted = _protector.Protect(JsonSerializer.Serialize(merged));
        }

        if (request.Activate)
            settings.ActiveProvider = targetProvider;

        settings.UpdatedAt = DateTimeOffset.UtcNow;
        settings.UpdatedBy = user;
        await _db.SaveChangesAsync(ct);
        return ToDto(settings);
    }

    public async Task<StorageTestResultDto> TestAsync(UpsertStorageSettingsRequest? draft, CancellationToken ct)
    {
        var settings = await _storage.GetOrCreateSettingsAsync(ct);
        var providerId = draft?.ActiveProvider?.Trim().ToUpperInvariant() ?? settings.ActiveProvider;
        string? plain = null;

        if (draft?.Config != null)
        {
            var merged = MergeConfig(settings, draft.Config);
            plain = JsonSerializer.Serialize(merged);
        }

        try
        {
            var provider = _resolver.ResolveByProviderId(providerId, settings, plain);
            var probeKey = $"{_db.CurrentTenantId:N}/_probe_{Guid.NewGuid():N}.txt";
            await using (var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("nexustock-probe")))
            {
                await provider.PutAsync(probeKey, ms, "text/plain", ct);
            }
            await provider.DeleteAsync(probeKey, ct);

            settings.LastTestAt = DateTimeOffset.UtcNow;
            settings.LastTestOk = true;
            settings.LastTestMessage = "Put+Delete probe object succeeded";
            await _db.SaveChangesAsync(ct);
            return new StorageTestResultDto(true, settings.LastTestMessage);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Storage test failed for {Provider}", providerId);
            settings.LastTestAt = DateTimeOffset.UtcNow;
            settings.LastTestOk = false;
            settings.LastTestMessage = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
            await _db.SaveChangesAsync(ct);
            throw new FileDomainException("STORAGE_TEST_FAILED", settings.LastTestMessage);
        }
    }

    private Dictionary<string, string?> MergeConfig(FileStorageSettings settings, Dictionary<string, string?> incoming)
    {
        var existing = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(settings.ConfigJsonEncrypted))
        {
            try
            {
                var plain = _protector.Unprotect(settings.ConfigJsonEncrypted);
                existing = JsonSerializer.Deserialize<Dictionary<string, string?>>(plain)
                    ?? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                existing = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            }
        }

        foreach (var (k, v) in incoming)
        {
            if (v == null) continue;
            if (SecretKeys.Contains(k) && (string.IsNullOrWhiteSpace(v) || v == "********"))
                continue;
            existing[k] = v;
        }
        return existing;
    }

    private StorageSettingsDto ToDto(FileStorageSettings settings)
    {
        var configured = !string.IsNullOrWhiteSpace(settings.ConfigJsonEncrypted);
        var localPath = !string.IsNullOrWhiteSpace(settings.LocalPathOverride)
            || !string.IsNullOrWhiteSpace(_configuration["UploadSettings:UploadPath"]);

        var providers = ProviderOrder.Select(id => new ProviderStatusDto(
            id,
            id switch
            {
                StorageProviderIds.Local => "Local disk",
                StorageProviderIds.AwsS3 => "Amazon S3",
                StorageProviderIds.AzureBlob => "Azure Blob",
                StorageProviderIds.Gcs => "Google Cloud Storage",
                StorageProviderIds.CloudflareR2 => "Cloudflare R2",
                _ => id
            },
            id == StorageProviderIds.Local ? localPath : configured
        )).ToList();

        return new StorageSettingsDto(
            settings.ActiveProvider,
            settings.PublicBaseUrl,
            localPath,
            providers,
            settings.LastTestAt,
            settings.LastTestOk,
            settings.LastTestMessage);
    }

    private static bool IsKnownProvider(string id) =>
        ProviderOrder.Contains(id, StringComparer.OrdinalIgnoreCase) ||
        string.Equals(id, StorageProviderIds.Fake, StringComparison.OrdinalIgnoreCase);
}
