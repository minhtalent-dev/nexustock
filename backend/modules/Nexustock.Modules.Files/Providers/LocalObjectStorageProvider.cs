using Microsoft.Extensions.Configuration;

namespace Nexustock.Modules.Files.Providers;

/// <summary>Lưu object trên disk local (default P41).</summary>
public sealed class LocalObjectStorageProvider : IObjectStorageProvider
{
    private readonly string _rootPath;
    private readonly string _requestPath;

    public LocalObjectStorageProvider(IConfiguration configuration, string? pathOverride = null)
    {
        _rootPath = string.IsNullOrWhiteSpace(pathOverride)
            ? configuration["UploadSettings:UploadPath"] ?? @"D:\NexustockUploads"
            : pathOverride;
        _requestPath = (configuration["UploadSettings:RequestPath"] ?? "/uploads").TrimEnd('/');
        Directory.CreateDirectory(_rootPath);
    }

    public string ProviderId => StorageProviderIds.Local;

    private string ResolvePath(string key)
    {
        var safe = key.Replace('\\', '/').TrimStart('/');
        if (safe.Contains("..", StringComparison.Ordinal))
            throw new InvalidOperationException("Invalid storage key");
        var full = Path.GetFullPath(Path.Combine(_rootPath, safe.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(Path.GetFullPath(_rootPath), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid storage key");
        return full;
    }

    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken ct)
    {
        var path = ResolvePath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(fs, ct);
    }

    public Task DeleteAsync(string key, CancellationToken ct)
    {
        var path = ResolvePath(key);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken ct)
        => Task.FromResult(File.Exists(ResolvePath(key)));

    public Task<Stream> OpenReadAsync(string key, CancellationToken ct)
    {
        var path = ResolvePath(key);
        if (!File.Exists(path)) throw new FileNotFoundException("Object not found", key);
        Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult(stream);
    }

    public string BuildPublicUrl(string key, string? publicBaseUrl)
    {
        var safe = key.Replace('\\', '/').TrimStart('/');
        if (!string.IsNullOrWhiteSpace(publicBaseUrl))
            return $"{publicBaseUrl.TrimEnd('/')}/{safe}";
        return $"{_requestPath}/{safe}";
    }
}
