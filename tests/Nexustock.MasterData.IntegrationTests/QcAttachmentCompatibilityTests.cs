using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Nexustock.Modules.Files.Contexts;
using Nexustock.Modules.Files.Entities;
using Nexustock.Modules.Files.Services;
using Nexustock.Modules.Qc.Contexts;
using Nexustock.Modules.Qc.Entities;
using Nexustock.Modules.Qc.Services;
using Xunit;

namespace Nexustock.MasterData.IntegrationTests;

[Trait("Category", "Phase46A")]
public class QcAttachmentCompatibilityTests : IntegrationTestBase
{
    private static readonly Guid TestTenantId = Guid.Parse(TestAuthConstants.TenantId);

    [Fact]
    public async Task QcAttachment_OnBound_UpdatesAttachmentRefsSnapshot()
    {
        var qcDb = Services.GetRequiredService<QcDbContext>();
        var filesDb = Services.GetRequiredService<FilesDbContext>();

        var qcResultId = Guid.NewGuid();
        qcDb.QcResults.Add(new QcResult
        {
            Id = qcResultId,
            TenantId = TestTenantId,
            Inspector = "Inspector1",
            QcRequestId = Guid.NewGuid(),
            IsPassed = true
        });
        await qcDb.SaveChangesAsync();

        var attachmentId = Guid.NewGuid();
        var filesAttachment = new FileAttachment
        {
            Id = attachmentId,
            TenantId = TestTenantId,
            EntityType = "QC_RESULT",
            EntityId = qcResultId,
            FileName = "qc_report.pdf",
            ContentType = "application/pdf",
            SizeBytes = 500,
            Kind = "DOCUMENT",
            Provider = "FAKE",
            StorageKey = "qc_report.pdf",
            PublicUrl = "/fake/qc_report.pdf"
        };
        filesDb.FileAttachments.Add(filesAttachment);
        await filesDb.SaveChangesAsync();

        // Trigger observer manually to test logic compatibility
        var observer = Services.GetRequiredService<IAttachmentLifecycleObserver>();
        await observer.OnBoundAsync(TestTenantId, "QC_RESULT", qcResultId, attachmentId, default);

        var updatedQc = qcDb.QcResults.First(q => q.Id == qcResultId);
        Assert.NotNull(updatedQc.AttachmentRefs);
        Assert.Contains($"/api/files/attachments/{attachmentId}/content", updatedQc.AttachmentRefs);
    }

    [Fact]
    public async Task QcReadService_FallbackToLegacyRefs_WhenNoActiveRows()
    {
        var qcDb = Services.GetRequiredService<QcDbContext>();
        var readService = Services.GetRequiredService<IQcAttachmentReadService>();

        var legacyQcId = Guid.NewGuid();
        qcDb.QcResults.Add(new QcResult
        {
            Id = legacyQcId,
            TenantId = TestTenantId,
            Inspector = "Inspector1",
            QcRequestId = Guid.NewGuid(),
            IsPassed = true,
            AttachmentRefs = "http://legacy-server/uploads/old_ref.png,http://legacy-server/uploads/old_ref2.pdf"
        });
        await qcDb.SaveChangesAsync();


        // No active row in FilesDbContext. Read service should fallback to legacy refs
        var list = await readService.GetAttachmentsByEntityIdAsync(legacyQcId, default);
        
        Assert.Equal(2, list.Count);
        Assert.Equal("old_ref.png", list[0].FileName);
        Assert.Equal("image/png", list[0].ContentType);
        Assert.Contains("/api/files/attachments/", list[0].ContentUrl); // Authed endpoint fallback
    }

    [Fact]
    public async Task QcAttachment_OnDeleted_UpdatesAttachmentRefsSnapshot()
    {
        var qcDb = Services.GetRequiredService<QcDbContext>();
        var filesDb = Services.GetRequiredService<FilesDbContext>();

        var qcResultId = Guid.NewGuid();
        qcDb.QcResults.Add(new QcResult
        {
            Id = qcResultId,
            TenantId = TestTenantId,
            Inspector = "Inspector1",
            QcRequestId = Guid.NewGuid(),
            IsPassed = true,
            AttachmentRefs = "should-be-cleared"
        });
        await qcDb.SaveChangesAsync();

        var attachmentId = Guid.NewGuid();
        // Seed active first
        var filesAttachment = new FileAttachment
        {
            Id = attachmentId,
            TenantId = TestTenantId,
            EntityType = "QC_RESULT",
            EntityId = qcResultId,
            FileName = "qc_report.pdf",
            ContentType = "application/pdf",
            SizeBytes = 500,
            Kind = "DOCUMENT",
            Provider = "FAKE",
            StorageKey = "qc_report.pdf",
            PublicUrl = "/fake/qc_report.pdf"
        };
        filesDb.FileAttachments.Add(filesAttachment);
        await filesDb.SaveChangesAsync();

        filesAttachment.DeletedAt = DateTimeOffset.UtcNow;
        await filesDb.SaveChangesAsync();

        // manually trigger deleted observer
        var observer = Services.GetRequiredService<IAttachmentLifecycleObserver>();
        await observer.OnDeletedAsync(TestTenantId, "QC_RESULT", qcResultId, attachmentId, default);

        var updatedQc = qcDb.QcResults.First(q => q.Id == qcResultId);
        Assert.Null(updatedQc.AttachmentRefs);
    }

    [Fact]
    public async Task QcReadService_NoFallbackToLegacyRefs_WhenHasSoftDeletedRows()
    {
        var qcDb = Services.GetRequiredService<QcDbContext>();
        var filesDb = Services.GetRequiredService<FilesDbContext>();
        var readService = Services.GetRequiredService<IQcAttachmentReadService>();

        var qcId = Guid.NewGuid();
        qcDb.QcResults.Add(new QcResult
        {
            Id = qcId,
            TenantId = TestTenantId,
            Inspector = "Inspector1",
            QcRequestId = Guid.NewGuid(),
            IsPassed = true,
            AttachmentRefs = "http://legacy-server/uploads/old_ref.png"
        });
        await qcDb.SaveChangesAsync();

        // Seed soft-deleted row in FilesDbContext
        filesDb.FileAttachments.Add(new FileAttachment
        {
            Id = Guid.NewGuid(),
            TenantId = TestTenantId,
            EntityType = "QC_RESULT",
            EntityId = qcId,
            FileName = "migrated.png",
            ContentType = "image/png",
            SizeBytes = 100,
            Kind = "IMAGE",
            Provider = "FAKE",
            StorageKey = "migrated.png",
            PublicUrl = "/fake/migrated.png",
            DeletedAt = DateTimeOffset.UtcNow
        });
        await filesDb.SaveChangesAsync();

        // Should return empty list, not legacy fallback!
        var list = await readService.GetAttachmentsByEntityIdAsync(qcId, default);
        Assert.Empty(list);
    }
}
