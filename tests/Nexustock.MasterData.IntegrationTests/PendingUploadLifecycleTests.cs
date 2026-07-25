using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Nexustock.Modules.Files.Contexts;
using Nexustock.Modules.Files.Entities;
using Nexustock.Modules.Files.Providers;
using Nexustock.Modules.Files.Services;
using Nexustock.Modules.Files.Dtos;
using Nexustock.Modules.MasterData.Contexts;
using Nexustock.Modules.MasterData.Entities;
using Xunit;

namespace Nexustock.MasterData.IntegrationTests;

[Trait("Category", "Phase46A")]
public class PendingUploadLifecycleTests : IntegrationTestBase
{
    private static readonly Guid TestTenantId = Guid.Parse(TestAuthConstants.TenantId);

    [Fact]
    public async Task PendingUpload_CleanedAfterExpiry_OrphanPurged()
    {
        var filesDb = Services.GetRequiredService<FilesDbContext>();
        var fakeStorage = Services.GetRequiredService<FakeObjectStorageProvider>();

        var expiredUploadId = Guid.NewGuid();
        var validUploadId = Guid.NewGuid();

        // 1. Seed fake files to storage
        var fileData = new byte[] { 1, 2, 3 };
        using (var ms1 = new MemoryStream(fileData))
            await fakeStorage.PutAsync("expired.png", ms1, "image/png", default);
        using (var ms2 = new MemoryStream(fileData))
            await fakeStorage.PutAsync("valid.png", ms2, "image/png", default);

        // 2. Add pending upload records
        filesDb.FilePendingUploads.Add(new FilePendingUpload
        {
            Id = expiredUploadId,
            TenantId = TestTenantId,
            FileName = "expired.png",
            ContentType = "image/png",
            SizeBytes = fileData.Length,
            Kind = "IMAGE",
            Provider = "FAKE",
            StorageKey = "expired.png",
            LegacyUrl = "/fake/expired.png",
            Status = "PENDING",
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-2),
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1)
        });

        filesDb.FilePendingUploads.Add(new FilePendingUpload
        {
            Id = validUploadId,
            TenantId = TestTenantId,
            FileName = "valid.png",
            ContentType = "image/png",
            SizeBytes = fileData.Length,
            Kind = "IMAGE",
            Provider = "FAKE",
            StorageKey = "valid.png",
            LegacyUrl = "/fake/valid.png",
            Status = "PENDING",
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(23)
        });
        await filesDb.SaveChangesAsync();

        // 3. Trigger cleanup service
        var cleanupService = Services.GetRequiredService<IPendingUploadCleanupService>();
        var cleanedCount = await cleanupService.CleanupExpiredPendingUploadsAsync(default);
        Assert.Equal(1, cleanedCount);

        // 4. Verify DB Status
        var rows = filesDb.FilePendingUploads.ToList();
        var expiredRow = rows.First(r => r.Id == expiredUploadId);
        var validRow = rows.First(r => r.Id == validUploadId);

        Assert.Equal("PURGED", expiredRow.Status);
        Assert.NotNull(expiredRow.PurgedAt);
        Assert.Equal("PENDING", validRow.Status);

        // 5. Verify Storage Provider Object Status
        Assert.False(await fakeStorage.ExistsAsync("expired.png", default));
        Assert.True(await fakeStorage.ExistsAsync("valid.png", default));
    }

    [Fact]
    public async Task PendingUpload_Bound_NotPurged()
    {
        var filesDb = Services.GetRequiredService<FilesDbContext>();
        var fakeStorage = Services.GetRequiredService<FakeObjectStorageProvider>();

        var boundUploadId = Guid.NewGuid();
        var fileData = new byte[] { 1, 2, 3 };
        using (var ms = new MemoryStream(fileData))
            await fakeStorage.PutAsync("bound.png", ms, "image/png", default);

        filesDb.FilePendingUploads.Add(new FilePendingUpload
        {
            Id = boundUploadId,
            TenantId = TestTenantId,
            FileName = "bound.png",
            ContentType = "image/png",
            SizeBytes = fileData.Length,
            Kind = "IMAGE",
            Provider = "FAKE",
            StorageKey = "bound.png",
            LegacyUrl = "/fake/bound.png",
            Status = "BOUND",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1), // Expired but already BOUND
            AttachmentId = Guid.NewGuid()
        });
        await filesDb.SaveChangesAsync();

        var cleanupService = Services.GetRequiredService<IPendingUploadCleanupService>();
        var cleanedCount = await cleanupService.CleanupExpiredPendingUploadsAsync(default);
        
        Assert.Equal(0, cleanedCount);
        Assert.True(await fakeStorage.ExistsAsync("bound.png", default));
    }

    [Fact]
    public async Task PendingUpload_BindBatch_PartialSuccessAndRetry()
    {
        var filesDb = Services.GetRequiredService<FilesDbContext>();
        var fakeStorage = Services.GetRequiredService<FakeObjectStorageProvider>();

        var validUploadId1 = Guid.NewGuid();
        var validUploadId2 = Guid.NewGuid();
        var invalidUploadId = Guid.NewGuid();

        // Seed products and pending uploads
        var masterData = Services.GetRequiredService<MasterDataDbContext>();
        var productId = Guid.NewGuid();
        masterData.Products.Add(new Product { Id = productId, TenantId = TestTenantId, Code = "PROD_BATCH", Name = "PB", BaseUomId = Guid.NewGuid() });
        await masterData.SaveChangesAsync();

        filesDb.FilePendingUploads.Add(new FilePendingUpload
        {
            Id = validUploadId1,
            TenantId = TestTenantId,
            FileName = "valid1.png",
            ContentType = "image/png",
            SizeBytes = 100,
            Kind = "IMAGE",
            Provider = "FAKE",
            StorageKey = "valid1.png",
            LegacyUrl = "/fake/valid1.png",
            Status = "PENDING",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(2)
        });
        await filesDb.SaveChangesAsync();

        // 1. Simulating client call: Bind valid1 (success)
        var service = Services.GetRequiredService<IAttachmentService>();
        var bound1 = await service.BindAsync(new BindAttachmentRequest(validUploadId1, "PRODUCT", productId), "TestUser", default);
        Assert.NotNull(bound1);

        // 2. Simulating client call: Bind invalid (fails with UPLOAD_NOT_FOUND)
        var ex = await Assert.ThrowsAsync<FileDomainException>(() => 
            service.BindAsync(new BindAttachmentRequest(invalidUploadId, "PRODUCT", productId), "TestUser", default));
        Assert.Equal("UPLOAD_NOT_FOUND", ex.ErrorCode);

        // 3. Seed valid2 now (to simulate it becoming valid/available for retry)
        filesDb.FilePendingUploads.Add(new FilePendingUpload
        {
            Id = validUploadId2,
            TenantId = TestTenantId,
            FileName = "valid2.png",
            ContentType = "image/png",
            SizeBytes = 200,
            Kind = "IMAGE",
            Provider = "FAKE",
            StorageKey = "valid2.png",
            LegacyUrl = "/fake/valid2.png",
            Status = "PENDING",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(2)
        });
        await filesDb.SaveChangesAsync();

        // 4. Retry both uploads:
        // - valid1 should return the existing attachment idempotently
        var bound1Retry = await service.BindAsync(new BindAttachmentRequest(validUploadId1, "PRODUCT", productId), "TestUser", default);
        Assert.Equal(bound1.Id, bound1Retry.Id);

        // - valid2 should succeed
        var bound2 = await service.BindAsync(new BindAttachmentRequest(validUploadId2, "PRODUCT", productId), "TestUser", default);
        Assert.NotNull(bound2);

        // Verify attachment count in DB is exactly 2
        var count = filesDb.FileAttachments.Count(a => a.EntityId == productId && a.DeletedAt == null);
        Assert.Equal(2, count);
    }
}
