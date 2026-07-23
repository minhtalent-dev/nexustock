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
            await provider.PutAsync(key, stream, contentType, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Storage put failed for provider {Provider}", provider.ProviderId);
            throw new FileDomainException("STORAGE_PROVIDER_ERROR", "Storage provider error", 503);
        }

        var url = provider.BuildPublicUrl(key, settings.PublicBaseUrl);
        return new UploadResultDto(name, contentType, file.Length, kind, provider.ProviderId, key, url);
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
