namespace Nexustock.Modules.Files.Providers;

public static class StorageProviderIds
{
    public const string Local = "LOCAL";
    public const string AwsS3 = "AWS_S3";
    public const string AzureBlob = "AZURE_BLOB";
    public const string Gcs = "GCS";
    public const string CloudflareR2 = "CLOUDFLARE_R2";
    public const string Fake = "FAKE";
}

public interface IObjectStorageProvider
{
    string ProviderId { get; }
    Task PutAsync(string key, Stream content, string contentType, CancellationToken ct);
    Task DeleteAsync(string key, CancellationToken ct);
    Task<bool> ExistsAsync(string key, CancellationToken ct);
    Task<Stream> OpenReadAsync(string key, CancellationToken ct);
    string BuildPublicUrl(string key, string? publicBaseUrl);
}
