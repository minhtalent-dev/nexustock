using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nexustock.Modules.Files.Contexts;
using Nexustock.Modules.Files.Entities;
using Nexustock.Modules.Files.Providers;
using Nexustock.Modules.Files.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;

namespace Nexustock.Files.IntegrationTests;

[Trait("Category", "Phase46B")]
public sealed class FileStorageServiceTests
{
    [Fact]
    public async Task UploadAsync_ValidWebP_PersistsOriginalAndThumbnail()
    {
        await using var db = CreateDb();
        var provider = new TestObjectStorageProvider(StorageProviderIds.Fake);
        var thumbnail = CreateThumbnailService();
        var service = new FileStorageService(db, new TestObjectStorageResolver(provider), thumbnail, NullLogger<FileStorageService>.Instance);
        await SeedSettingsAsync(db);
        await using var imageStream = await CreateWebPAsync();
        var file = CreateFormFile(imageStream, "sample.webp");

        var result = await service.UploadAsync(file, "phase46b-test", CancellationToken.None);
        var pending = await db.FilePendingUploads.SingleAsync();

        Assert.Equal("image/webp", result.ContentType);
        Assert.NotNull(pending.ThumbnailKey);
        Assert.True(await provider.ExistsAsync(pending.StorageKey, CancellationToken.None));
        Assert.True(await provider.ExistsAsync(pending.ThumbnailKey!, CancellationToken.None));
    }

    [Fact]
    public async Task UploadAsync_ThumbnailFailure_DoesNotLogStorageKey()
    {
        await using var db = CreateDb();
        var provider = new TestObjectStorageProvider(StorageProviderIds.Fake);
        var logger = new CapturingLogger<FileStorageService>();
        var service = new FileStorageService(db, new TestObjectStorageResolver(provider), new FailingThumbnailService(), logger);
        await SeedSettingsAsync(db);
        await using var imageStream = await CreateWebPAsync();
        var file = CreateFormFile(imageStream, "sample.webp");

        await service.UploadAsync(file, null, CancellationToken.None);
        var pending = await db.FilePendingUploads.SingleAsync();
        var logs = string.Join("\n", logger.Messages);

        Assert.DoesNotContain(pending.StorageKey, logs, StringComparison.Ordinal);
        Assert.DoesNotContain(pending.ThumbnailKey ?? "never-present", logs, StringComparison.Ordinal);
        Assert.Contains(StorageProviderIds.Fake, logs, StringComparison.Ordinal);
    }

    private static FilesDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<FilesDbContext>()
            .UseInMemoryDatabase($"p46b-files-{Guid.NewGuid():N}")
            .Options;
        return new FilesDbContext(options);
    }

    private static async Task SeedSettingsAsync(FilesDbContext db)
    {
        db.FileStorageSettings.Add(new FileStorageSettings
        {
            Id = Guid.NewGuid(),
            TenantId = db.CurrentTenantId,
            ActiveProvider = StorageProviderIds.Fake,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private static ThumbnailService CreateThumbnailService() => new(
        Options.Create(new ThumbnailOptions { Enabled = true }),
        NullLogger<ThumbnailService>.Instance);

    private static FormFile CreateFormFile(Stream stream, string fileName)
    {
        return new FormFile(stream, 0, stream.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/webp"
        };
    }

    private static async Task<MemoryStream> CreateWebPAsync()
    {
        var stream = new MemoryStream();
        using var image = new Image<Rgba32>(32, 16);
        await image.SaveAsync(stream, new WebpEncoder());
        stream.Position = 0;
        return stream;
    }

    private sealed class FailingThumbnailService : IThumbnailService
    {
        public bool CanGenerate(string contentType, byte[] headerBytes) => true;
        public Task<Stream> GenerateAsync(Stream originalStream, CancellationToken ct)
            => throw new InvalidDataException("thumbnail failed");
        public string BuildKey(string originalKey) => $"{originalKey}.thumb.jpg";
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }
}
