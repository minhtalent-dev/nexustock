using System.Collections.Concurrent;

namespace Nexustock.Modules.Files.Providers;

/// <summary>In-memory provider — chỉ dùng test/CI.</summary>
public sealed class FakeObjectStorageProvider : IObjectStorageProvider
{
    private readonly ConcurrentDictionary<string, byte[]> _store = new(StringComparer.Ordinal);

    public string ProviderId => StorageProviderIds.Fake;

    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        _store[key] = ms.ToArray();
    }

    public Task DeleteAsync(string key, CancellationToken ct)
    {
        _store.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken ct)
        => Task.FromResult(_store.ContainsKey(key));

    public Task<Stream> OpenReadAsync(string key, CancellationToken ct)
    {
        if (!_store.TryGetValue(key, out var bytes))
            throw new FileNotFoundException("Object not found", key);
        Stream stream = new MemoryStream(bytes, writable: false);
        return Task.FromResult(stream);
    }

    public string BuildPublicUrl(string key, string? publicBaseUrl)
    {
        var safe = key.TrimStart('/');
        if (!string.IsNullOrWhiteSpace(publicBaseUrl))
            return $"{publicBaseUrl.TrimEnd('/')}/{safe}";
        return $"/fake/{safe}";
    }
}
