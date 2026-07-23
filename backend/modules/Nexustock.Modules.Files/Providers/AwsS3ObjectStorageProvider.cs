using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using System.Text.Json;

namespace Nexustock.Modules.Files.Providers;

/// <summary>AWS S3 object storage.</summary>
public sealed class AwsS3ObjectStorageProvider : IObjectStorageProvider
{
    private readonly IAmazonS3 _client;
    private readonly string _bucket;

    public AwsS3ObjectStorageProvider(string configJson)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(configJson) ? "{}" : configJson);
        var root = doc.RootElement;
        _bucket = root.TryGetProperty("bucket", out var b) ? b.GetString() ?? "" : "";
        var region = root.TryGetProperty("region", out var r) ? r.GetString() ?? "ap-southeast-1" : "ap-southeast-1";
        var accessKey = root.TryGetProperty("accessKeyId", out var ak) ? ak.GetString() : null;
        var secret = root.TryGetProperty("secretAccessKey", out var sk) ? sk.GetString() : null;
        var forcePath = root.TryGetProperty("forcePathStyle", out var fp) && fp.ValueKind == JsonValueKind.True;

        if (string.IsNullOrWhiteSpace(_bucket))
            throw new InvalidOperationException("STORAGE_CONFIG_INVALID");

        var cfg = new AmazonS3Config
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(region),
            ForcePathStyle = forcePath
        };
        _client = string.IsNullOrWhiteSpace(accessKey)
            ? new AmazonS3Client(cfg)
            : new AmazonS3Client(accessKey, secret, cfg);
    }

    public string ProviderId => StorageProviderIds.AwsS3;

    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken ct)
    {
        var req = new PutObjectRequest
        {
            BucketName = _bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false
        };
        await _client.PutObjectAsync(req, ct);
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
        return $"https://{_bucket}.s3.amazonaws.com/{safe}";
    }
}
