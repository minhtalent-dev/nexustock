using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using System.Text.Json;

namespace Nexustock.Modules.Files.Providers;

/// <summary>Google Cloud Storage.</summary>
public sealed class GcsObjectStorageProvider : IObjectStorageProvider
{
    private readonly StorageClient _client;
    private readonly string _bucket;

    public GcsObjectStorageProvider(string configJson)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(configJson) ? "{}" : configJson);
        var root = doc.RootElement;
        _bucket = root.TryGetProperty("bucket", out var b) ? b.GetString() ?? "" : "";
        var saJson = root.TryGetProperty("serviceAccountJson", out var sa) ? sa.GetString() : null;

        if (string.IsNullOrWhiteSpace(_bucket))
            throw new InvalidOperationException("STORAGE_CONFIG_INVALID");

        if (!string.IsNullOrWhiteSpace(saJson))
        {
            var credential = GoogleCredential.FromJson(saJson);
            _client = StorageClient.Create(credential);
        }
        else
        {
            _client = StorageClient.Create();
        }
    }

    public string ProviderId => StorageProviderIds.Gcs;

    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken ct)
        => await _client.UploadObjectAsync(_bucket, key, contentType, content, cancellationToken: ct);

    public async Task DeleteAsync(string key, CancellationToken ct)
        => await _client.DeleteObjectAsync(_bucket, key, cancellationToken: ct);

    public async Task<bool> ExistsAsync(string key, CancellationToken ct)
    {
        try
        {
            await _client.GetObjectAsync(_bucket, key, cancellationToken: ct);
            return true;
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task<Stream> OpenReadAsync(string key, CancellationToken ct)
    {
        var ms = new MemoryStream();
        await _client.DownloadObjectAsync(_bucket, key, ms, cancellationToken: ct);
        ms.Position = 0;
        return ms;
    }

    public string BuildPublicUrl(string key, string? publicBaseUrl)
    {
        var safe = key.TrimStart('/');
        if (!string.IsNullOrWhiteSpace(publicBaseUrl))
            return $"{publicBaseUrl.TrimEnd('/')}/{safe}";
        return $"https://storage.googleapis.com/{_bucket}/{safe}";
    }
}
