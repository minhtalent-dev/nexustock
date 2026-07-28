using Nexustock.Modules.Files.Entities;
using Nexustock.Modules.Files.Providers;
using Nexustock.Modules.Files.Services;
using System.Collections.Concurrent;

namespace Nexustock.Files.IntegrationTests;

internal sealed class TestObjectStorageProvider(string providerId) : IObjectStorageProvider
{
    private readonly ConcurrentDictionary<string, byte[]> _objects = new(StringComparer.Ordinal);

    public string ProviderId { get; } = providerId;
    public Func<string, Task>? AfterPutAsync { get; set; }
    public Func<string, Exception?>? PutFailure { get; set; }
    public ConcurrentQueue<string> DeletedKeys { get; } = new();

    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken ct)
    {
        if (PutFailure?.Invoke(key) is { } failure)
            throw failure;

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        _objects[key] = buffer.ToArray();
        if (AfterPutAsync is not null)
            await AfterPutAsync(key);
    }

    public Task DeleteAsync(string key, CancellationToken ct)
    {
        DeletedKeys.Enqueue(key);
        _objects.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken ct)
        => Task.FromResult(_objects.ContainsKey(key));

    public Task<Stream> OpenReadAsync(string key, CancellationToken ct)
    {
        if (!_objects.TryGetValue(key, out var bytes))
            throw new FileNotFoundException("Object not found", key);
        return Task.FromResult<Stream>(new MemoryStream(bytes, writable: false));
    }

    public string BuildPublicUrl(string key, string? publicBaseUrl) => $"/test/{key}";
}

internal sealed class TestObjectStorageResolver(params TestObjectStorageProvider[] providers) : IObjectStorageResolver
{
    private readonly Dictionary<string, TestObjectStorageProvider> _providers = providers
        .ToDictionary(provider => provider.ProviderId, StringComparer.OrdinalIgnoreCase);

    public IObjectStorageProvider Resolve(FileStorageSettings settings, string? providerOverride = null, string? configJsonPlain = null)
        => ResolveByProviderId(providerOverride ?? settings.ActiveProvider, settings, configJsonPlain);

    public IObjectStorageProvider ResolveByProviderId(string providerId, FileStorageSettings settings, string? configJsonPlain = null)
        => _providers.TryGetValue(providerId, out var provider)
            ? provider
            : throw new InvalidOperationException("STORAGE_CONFIG_INVALID");
}

internal sealed class DeterministicThumbnailService : IThumbnailService
{
    public bool CanGenerate(string contentType, byte[] headerBytes) => true;
    public Task<Stream> GenerateAsync(Stream originalStream, CancellationToken ct)
        => Task.FromResult<Stream>(new MemoryStream([0xFF, 0xD8, 0xFF, 0xD9]));
    public string BuildKey(string originalKey) => $"{originalKey}.thumb.jpg";
}
