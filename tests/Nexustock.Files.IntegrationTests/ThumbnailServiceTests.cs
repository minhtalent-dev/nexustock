using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nexustock.Modules.Files.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;

namespace Nexustock.Files.IntegrationTests;

[Trait("Category", "Phase46B")]
public sealed class ThumbnailServiceTests
{
    private readonly ThumbnailService _service = new(
        Options.Create(new ThumbnailOptions
        {
            Enabled = true,
            MaxEdge = 256,
            JpegQuality = 82,
            MaxPixels = 40_000_000,
            MaxDimension = 12_000,
            TimeoutSeconds = 10
        }),
        NullLogger<ThumbnailService>.Instance);

    [Theory]
    [MemberData(nameof(SupportedHeaders))]
    public void CanGenerate_AcceptsSupportedMagicBytes(string contentType, byte[] header)
        => Assert.True(_service.CanGenerate(contentType, header));

    [Fact]
    public void CanGenerate_RejectsInvalidRiffAndMimeMismatch()
    {
        Assert.False(_service.CanGenerate("image/webp", [0x52, 0x49, 0x46, 0x47, 0, 0, 0, 0, 0x57, 0x45, 0x42, 0x50]));
        Assert.False(_service.CanGenerate("application/octet-stream", SupportedHeaders().Last()[1] as byte[] ?? []));
    }

    [Fact]
    public async Task GenerateAsync_ProducesBoundedJpegWithoutUpscale()
    {
        await using var source = new MemoryStream();
        using (var image = new Image<Rgba32>(400, 200))
            await image.SaveAsPngAsync(source);
        source.Position = 0;

        await using var output = await _service.GenerateAsync(source, CancellationToken.None);
        using var thumbnail = await Image.LoadAsync(output);

        Assert.Equal(256, thumbnail.Width);
        Assert.Equal(128, thumbnail.Height);
        Assert.IsType<JpegFormat>(thumbnail.Metadata.DecodedImageFormat);
    }

    [Fact]
    public async Task GenerateAsync_CallerCancellationPropagates()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _service.GenerateAsync(new MemoryStream(new byte[32]), cts.Token));
    }

    public static IEnumerable<object[]> SupportedHeaders()
    {
        yield return ["image/jpeg", new byte[] { 0xFF, 0xD8, 0xFF, 0, 0, 0, 0, 0, 0, 0, 0, 0 }];
        yield return ["image/png", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0 }];
        yield return ["image/webp", new byte[] { 0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x57, 0x45, 0x42, 0x50 }];
    }
}
