using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Nexustock.Modules.Files.Services;

public interface IThumbnailService
{
    bool CanGenerate(string contentType, byte[] headerBytes);
    Task<Stream> GenerateAsync(Stream originalStream, CancellationToken ct);
    string BuildKey(string originalKey);
}

public sealed class ThumbnailService : IThumbnailService
{
    private readonly ThumbnailOptions _options;
    private readonly ILogger<ThumbnailService> _logger;

    public ThumbnailService(
        IOptions<ThumbnailOptions> options,
        ILogger<ThumbnailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool CanGenerate(string contentType, byte[] headerBytes)
    {
        if (string.IsNullOrWhiteSpace(contentType)) return false;

        // Bỏ qua định dạng không phải hình ảnh nhanh chóng bằng MIME
        if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return false;

        // Magic bytes checks
        if (headerBytes == null || headerBytes.Length < 12) return false;

        // JPEG: FF D8 FF
        if (headerBytes[0] == 0xFF && headerBytes[1] == 0xD8 && headerBytes[2] == 0xFF)
            return true;

        // PNG: 89 50 4E 47 0D 0A 1A 0A
        if (headerBytes[0] == 0x89 && headerBytes[1] == 0x50 && headerBytes[2] == 0x4E && headerBytes[3] == 0x47 &&
            headerBytes[4] == 0x0D && headerBytes[5] == 0x0A && headerBytes[6] == 0x1A && headerBytes[7] == 0x0A)
            return true;

        // WebP: RIFF....WEBP (52 49 46 46 at 0, 57 45 42 50 at 8)
        if (headerBytes[0] == 0x52 && headerBytes[1] == 0x49 && headerBytes[2] == 0x46 && headerBytes[3] == 0x46 &&
            headerBytes[8] == 0x57 && headerBytes[9] == 0x45 && headerBytes[10] == 0x42 && headerBytes[11] == 0x50)
            return true;

        return false;
    }

    public async Task<Stream> GenerateAsync(Stream originalStream, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (!_options.Enabled)
            throw new InvalidOperationException("Thumbnail generation is disabled via options");

        // Đảm bảo stream ở đầu
        if (originalStream.CanSeek)
        {
            originalStream.Position = 0;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));
        var operationToken = timeoutCts.Token;

        // Đọc metadata ảnh trước khi decode toàn bộ (Image decompression-bomb guard)
        ImageInfo imageInfo;
        try
        {
            imageInfo = await Image.IdentifyAsync(originalStream, operationToken);
            if (imageInfo == null)
                throw new InvalidDataException("Invalid or unsupported image stream");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to identify image metadata.");
            throw new InvalidDataException("Failed to identify image metadata.", ex);
        }

        // Guard dimensions/pixels
        if (imageInfo.Width <= 0 || imageInfo.Height <= 0)
            throw new InvalidDataException("Image width/height must be positive");

        long pixels = (long)imageInfo.Width * imageInfo.Height;
        if (pixels > _options.MaxPixels)
            throw new InvalidDataException($"Image exceeds max pixel limit of {_options.MaxPixels} pixels");

        if (imageInfo.Width > _options.MaxDimension || imageInfo.Height > _options.MaxDimension)
            throw new InvalidDataException($"Image dimension exceeds max limit of {_options.MaxDimension} px");

        // Reset stream để Load ảnh
        if (originalStream.CanSeek)
        {
            originalStream.Position = 0;
        }

        var memoryStream = new MemoryStream();
        using (var image = await Image.LoadAsync(originalStream, operationToken))
        {
            // Auto orient và resize Mode Max 256
            image.Mutate(x =>
            {
                x.AutoOrient();
                
                // Chỉ resize nếu kích thước lớn hơn MaxEdge (không upscale)
                if (image.Width > _options.MaxEdge || image.Height > _options.MaxEdge)
                {
                    x.Resize(new ResizeOptions
                    {
                        Size = new Size(_options.MaxEdge, _options.MaxEdge),
                        Mode = ResizeMode.Max
                    });
                }
            });

            // Thumbnail không giữ metadata từ ảnh nguồn.
            image.Metadata.ExifProfile = null;
            image.Metadata.IptcProfile = null;
            image.Metadata.XmpProfile = null;
            image.Metadata.IccProfile = null;

            var encoder = new JpegEncoder
            {
                Quality = _options.JpegQuality
            };

            await image.SaveAsJpegAsync(memoryStream, encoder, operationToken);
        }

        memoryStream.Position = 0;
        return memoryStream;
    }

    public string BuildKey(string originalKey)
    {
        if (string.IsNullOrWhiteSpace(originalKey))
            throw new ArgumentException("Original storage key cannot be empty", nameof(originalKey));
        return $"{originalKey}.thumb.jpg";
    }
}
