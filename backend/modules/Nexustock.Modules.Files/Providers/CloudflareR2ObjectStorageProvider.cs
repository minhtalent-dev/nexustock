using Amazon.S3;
using Amazon.S3.Model;
using System.Text.Json;

namespace Nexustock.Modules.Files.Providers;

/// <summary>Cloudflare R2 — S3-compatible client + custom endpoint.</summary>
public sealed class CloudflareR2ObjectStorageProvider : IObjectStorageProvider
{
    private readonly IAmazonS3 _client;
    private readonly string _bucket;

    public CloudflareR2ObjectStorageProvider(string configJson)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(configJson) ? "{}" : configJson);
        var root = doc.RootElement;
        _bucket = root.TryGetProperty("bucket", out var b) ? b.GetString() ?? "" : "";
        var accountId = root.TryGetProperty("accountId", out var a) ? a.GetString() ?? "" : "";
        var endpoint = root.TryGetProperty("endpoint", out var e) ? e.GetString() : null;
        var accessKey = root.TryGetProperty("accessKeyId", out var ak) ? ak.GetString() ?? "" : "";
        var secret = root.TryGetProperty("secretAccessKey", out var sk) ? sk.GetString() ?? "" : "";

        if (string.IsNullOrWhiteSpace(_bucket) || string.IsNullOrWhiteSpace(accessKey))
            throw new InvalidOperationException("STORAGE_CONFIG_INVALID");

        var ep = string.IsNullOrWhiteSpace(endpoint)
            ? $"https://{accountId}.r2.cloudflarestorage.com"
            : endpoint;

        var cfg = new AmazonS3Config
        {
            ServiceURL = ep,
            ForcePathStyle = true
        };
        _client = new AmazonS3Client(accessKey, secret, cfg);
    }

    public string ProviderId => StorageProviderIds.CloudflareR2;

    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken ct)
    {
        await _client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false
        }, ct);
    }

    public async Task DeleteAsync(string key, CancellationToken ct)
        => await _client.DeleteObjectAsync(_bucket, key, ct);

    public async Task<bool> ExistsAsync(string key, CancellationToken ct)
    {
        try
        {
            await _client.GetObjectMetadataAsync(_bucket, key, ct);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task<Stream> OpenReadAsync(string key, CancellationToken ct)
    {
        var resp = await _client.GetObjectAsync(_bucket, key, ct);
        return resp.ResponseStream;
    }

    public string BuildPublicUrl(string key, string? publicBaseUrl)
    {
        var safe = key.TrimStart('/');
        if (!string.IsNullOrWhiteSpace(publicBaseUrl))
            return $"{publicBaseUrl.TrimEnd('/')}/{safe}";
        return $"r2://{_bucket}/{safe}";
    }
}
