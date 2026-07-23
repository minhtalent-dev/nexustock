using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System.Text.Json;

namespace Nexustock.Modules.Files.Providers;

/// <summary>Azure Blob Storage.</summary>
public sealed class AzureBlobObjectStorageProvider : IObjectStorageProvider
{
    private readonly BlobContainerClient _container;

    public AzureBlobObjectStorageProvider(string configJson)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(configJson) ? "{}" : configJson);
        var root = doc.RootElement;
        var connectionString = root.TryGetProperty("connectionString", out var cs) ? cs.GetString() : null;
        var container = root.TryGetProperty("container", out var c) ? c.GetString() ?? "nexustock" : "nexustock";
        var accountName = root.TryGetProperty("accountName", out var an) ? an.GetString() : null;
        var accountKey = root.TryGetProperty("accountKey", out var ak) ? ak.GetString() : null;

        BlobServiceClient service;
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            service = new BlobServiceClient(connectionString);
        }
        else if (!string.IsNullOrWhiteSpace(accountName) && !string.IsNullOrWhiteSpace(accountKey))
        {
            service = new BlobServiceClient(
                new Uri($"https://{accountName}.blob.core.windows.net"),
                new Azure.Storage.StorageSharedKeyCredential(accountName, accountKey));
        }
        else
        {
            throw new InvalidOperationException("STORAGE_CONFIG_INVALID");
        }

        _container = service.GetBlobContainerClient(container);
        _container.CreateIfNotExists(PublicAccessType.None);
    }

    public string ProviderId => StorageProviderIds.AzureBlob;

    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken ct)
    {
        var blob = _container.GetBlobClient(key);
        await blob.UploadAsync(content, new BlobHttpHeaders { ContentType = contentType }, cancellationToken: ct);
    }

    public async Task DeleteAsync(string key, CancellationToken ct)
        => await _container.GetBlobClient(key).DeleteIfExistsAsync(cancellationToken: ct);

    public async Task<bool> ExistsAsync(string key, CancellationToken ct)
        => await _container.GetBlobClient(key).ExistsAsync(ct);

    public async Task<Stream> OpenReadAsync(string key, CancellationToken ct)
        => await _container.GetBlobClient(key).OpenReadAsync(cancellationToken: ct);

    public string BuildPublicUrl(string key, string? publicBaseUrl)
    {
        var safe = key.TrimStart('/');
        if (!string.IsNullOrWhiteSpace(publicBaseUrl))
            return $"{publicBaseUrl.TrimEnd('/')}/{safe}";
        return _container.GetBlobClient(key).Uri.ToString();
    }
}
