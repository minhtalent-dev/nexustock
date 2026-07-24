using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexustock.Modules.Files.Contexts;
using Nexustock.Modules.Files.Dtos;
using Nexustock.Modules.Files.Entities;
using Nexustock.Modules.Files.Providers;

namespace Nexustock.Modules.Files.Services;

public interface IFileStorageService
{
    Task<UploadResultDto> UploadAsync(IFormFile file, string? user, CancellationToken ct);
}

public sealed class FileStorageService : IFileStorageService
{
    private static readonly HashSet<string> AllowedExt = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".pdf", ".csv", ".xlsx"
    };

    private static readonly Dictionary<string, HashSet<string>> ExtMime = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = new(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/jpg" },
        [".jpeg"] = new(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/jpg" },
        [".png"] = new(StringComparer.OrdinalIgnoreCase) { "image/png" },
        [".webp"] = new(StringComparer.OrdinalIgnoreCase) { "image/webp" },
        [".pdf"] = new(StringComparer.OrdinalIgnoreCase) { "application/pdf" },
        [".csv"] = new(StringComparer.OrdinalIgnoreCase) { "text/csv", "application/vnd.ms-excel", "application/csv", "text/plain" },
        [".xlsx"] = new(StringComparer.OrdinalIgnoreCase) { "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "application/octet-stream" },
    };

    private readonly FilesDbContext _db;
    private readonly IObjectStorageResolver _resolver;
    private readonly ILogger<FileStorageService> _logger;

    public FileStorageService(FilesDbContext db, IObjectStorageResolver resolver, ILogger<FileStorageService> logger)
    {
        _db = db;
        _resolver = resolver;
        _logger = logger;
    }

    public async Task<FileStorageSettings> GetOrCreateSettingsAsync(CancellationToken ct)
    {
        var settings = await _db.FileStorageSettings.FirstOrDefaultAsync(ct);
        if (settings != null) return settings;

        settings = new FileStorageSettings
        {
            Id = Guid.NewGuid(),
            TenantId = _db.CurrentTenantId,
            ActiveProvider = StorageProviderIds.Local,
            IsEnabled = true,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _db.FileStorageSettings.Add(settings);
        await _db.SaveChangesAsync(ct);
        return settings;
    }

    public async Task<UploadResultDto> UploadAsync(IFormFile file, string? user, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            throw new FileDomainException("FILE_EMPTY", "No file uploaded or file is empty");
        if (file.Length > 10 * 1024 * 1024)
            throw new FileDomainException("FILE_TOO_LARGE", "File exceeds 10 MB limit");

        var name = Path.GetFileName(file.FileName);
        var ext = Path.GetExtension(name).ToLowerInvariant();
        if (name.Split('.').Length > 2)
            throw new FileDomainException("FILE_TYPE_NOT_ALLOWED", "Double extension is not allowed");
        if (!AllowedExt.Contains(ext))
            throw new FileDomainException("FILE_TYPE_NOT_ALLOWED", $"Extension {ext} is not allowed");

        var contentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType;
        if (!ExtMime.TryGetValue(ext, out var mimes) || !mimes.Contains(contentType))
            throw new FileDomainException("FILE_TYPE_NOT_ALLOWED", "MIME type does not match extension");

        var kind = ext is ".pdf" or ".csv" or ".xlsx" ? "DOCUMENT" : "IMAGE";
        var key = $"{_db.CurrentTenantId:N}/{Guid.NewGuid():N}{ext}";

        var settings = await GetOrCreateSettingsAsync(ct);
        var provider = _resolver.Resolve(settings);

        try
        {
            await using var stream = file.OpenReadStream();
            
            // Magic byte validation tối thiểu cho an toàn upload
            var header = new byte[8];
            var bytesRead = await stream.ReadAsync(header.AsMemory(0, 8), ct);
            stream.Position = 0; // Rewind stream sau khi đọc magic-bytes
            
            if (bytesRead >= 3 && ext == ".jpg" && (header[0] != 0xFF || header[1] != 0xD8 || header[2] != 0xFF))
                throw new FileDomainException("FILE_CONTENT_MISMATCH", "JPG header mismatch");
            if (bytesRead >= 4 && ext == ".png" && (header[0] != 0x89 || header[1] != 0x50 || header[2] != 0x4E || header[3] != 0x47))
                throw new FileDomainException("FILE_CONTENT_MISMATCH", "PNG header mismatch");
            if (bytesRead >= 4 && ext == ".pdf" && (header[0] != 0x25 || header[1] != 0x50 || header[2] != 0x44 || header[3] != 0x46)) // %PDF
                throw new FileDomainException("FILE_CONTENT_MISMATCH", "PDF header mismatch");

            await provider.PutAsync(key, stream, contentType, ct);
        }
        catch (FileDomainException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Storage put failed for provider {Provider}", provider.ProviderId);
            throw new FileDomainException("STORAGE_PROVIDER_ERROR", "Storage provider error", 503);
        }

        var url = provider.BuildPublicUrl(key, settings.PublicBaseUrl);
        var uploadId = Guid.NewGuid();
        var expiresAt = DateTimeOffset.UtcNow.AddHours(24);

        var pending = new FilePendingUpload
        {
            Id = uploadId,
            TenantId = _db.CurrentTenantId,
            FileName = name,
            ContentType = contentType,
            SizeBytes = file.Length,
            Kind = kind,
            Provider = provider.ProviderId,
            StorageKey = key,
            LegacyUrl = url,
            Status = "PENDING",
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = user,
            ExpiresAt = expiresAt
        };

        _db.FilePendingUploads.Add(pending);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            var detail = ex.InnerException?.Message ?? ex.Message;
            _logger.LogError(ex, "Failed to persist pending upload record: {Detail}", detail);
            try
            {
                await provider.DeleteAsync(key, ct);
            }
            catch (Exception deleteEx)
            {
                _logger.LogWarning(deleteEx, "Storage delete cleanup failed after db error");
            }
            throw new FileDomainException("STORAGE_PROVIDER_ERROR", "Storage persistence error", 503);
        }

        return new UploadResultDto(uploadId, name, contentType, file.Length, kind, provider.ProviderId, url, expiresAt);
    }
}

public sealed class FileDomainException : Exception
{
    public string ErrorCode { get; }
    public int StatusCode { get; }

    public FileDomainException(string code, string message, int statusCode = 400) : base(message)
    {
        ErrorCode = code;
        StatusCode = statusCode;
    }
}
