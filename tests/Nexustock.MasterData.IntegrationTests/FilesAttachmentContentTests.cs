using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Nexustock.Modules.Files.Contexts;
using Nexustock.Modules.Files.Dtos;
using Nexustock.Modules.Files.Entities;
using Nexustock.Modules.Files.Providers;
using Nexustock.Modules.MasterData.Contexts;
using Nexustock.Modules.MasterData.Entities;
using Nexustock.Modules.Inventory.Contexts;
using Nexustock.Modules.Inventory.Entities;
using Xunit;

namespace Nexustock.MasterData.IntegrationTests;

[Trait("Category", "Phase46A")]
public class FilesAttachmentContentTests : IntegrationTestBase
{
    private static readonly Guid TestTenantId = Guid.Parse(TestAuthConstants.TenantId);

    [Fact]
    public async Task GetContent_ProviderFailure_Returns503()
    {
        PermissionService.AllowedPermissions.Add("files.read");
        var attachmentId = Guid.NewGuid();
        var key = "test503.png";

        var fakeStorage = Services.GetRequiredService<FakeObjectStorageProvider>();
        var fileBytes = new byte[] { 1, 2, 3 };
        using var ms = new MemoryStream(fileBytes);
        await fakeStorage.PutAsync(key, ms, "image/png", default);

        var filesDb = Services.GetRequiredService<FilesDbContext>();
        filesDb.FileAttachments.Add(new FileAttachment
        {
            Id = attachmentId,
            TenantId = TestTenantId,
            EntityType = "PRODUCT",
            EntityId = Guid.NewGuid(),
            FileName = "test503.png",
            ContentType = "image/png",
            SizeBytes = fileBytes.Length,
            Kind = "IMAGE",
            Provider = "FAKE",
            StorageKey = key,
            PublicUrl = "/fake/test503.png"
        });
        await filesDb.SaveChangesAsync();

        try
        {
            TestStorageFailureControl.ShouldFailRead = true;

            var response = await Client.GetAsync($"/api/files/attachments/{attachmentId}/content?disposition=inline");
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

            var err = await response.Content.ReadFromJsonAsync<ErrorResponse>();
            Assert.Equal("STORAGE_PROVIDER_ERROR", err?.Error);
        }
        finally
        {
            TestStorageFailureControl.ShouldFailRead = false;
        }
    }

    [Fact]
    public async Task GetContent_CrossTenant_Returns404()
    {
        PermissionService.AllowedPermissions.Add("files.read");
        var attachmentId = Guid.NewGuid();
        var key = "crosstenant.png";

        var fakeStorage = Services.GetRequiredService<FakeObjectStorageProvider>();
        var fileBytes = new byte[] { 1, 2, 3 };
        using var ms = new MemoryStream(fileBytes);
        await fakeStorage.PutAsync(key, ms, "image/png", default);

        var filesDb = Services.GetRequiredService<FilesDbContext>();
        filesDb.FileAttachments.Add(new FileAttachment
        {
            Id = attachmentId,
            TenantId = Guid.NewGuid(), // Different tenant
            EntityType = "PRODUCT",
            EntityId = Guid.NewGuid(),
            FileName = "crosstenant.png",
            ContentType = "image/png",
            SizeBytes = fileBytes.Length,
            Kind = "IMAGE",
            Provider = "FAKE",
            StorageKey = key,
            PublicUrl = "/fake/crosstenant.png"
        });
        await filesDb.SaveChangesAsync();

        var response = await Client.GetAsync($"/api/files/attachments/{attachmentId}/content?disposition=inline");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetContent_NoPermission_Returns403()
    {
        PermissionService.AllowedPermissions.Clear();
        var response = await Client.GetAsync($"/api/files/attachments/{Guid.NewGuid()}/content");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetContent_MissingOrDeleted_Returns404()
    {
        PermissionService.AllowedPermissions.Add("files.read");
        
        // Missing Guid
        var response = await Client.GetAsync($"/api/files/attachments/{Guid.NewGuid()}/content");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // Soft deleted
        var attachmentId = Guid.NewGuid();
        var filesDb = Services.GetRequiredService<FilesDbContext>();
        filesDb.FileAttachments.Add(new FileAttachment
        {
            Id = attachmentId,
            TenantId = TestTenantId,
            EntityType = "PRODUCT",
            EntityId = Guid.NewGuid(),
            FileName = "test.png",
            ContentType = "image/png",
            SizeBytes = 100,
            Kind = "IMAGE",
            Provider = "FAKE",
            StorageKey = "test.png",
            PublicUrl = "/fake/test.png",
            DeletedAt = DateTimeOffset.UtcNow
        });
        await filesDb.SaveChangesAsync();

        response = await Client.GetAsync($"/api/files/attachments/{attachmentId}/content");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetContent_InvalidDisposition_Returns400()
    {
        PermissionService.AllowedPermissions.Add("files.read");
        var response = await Client.GetAsync($"/api/files/attachments/{Guid.NewGuid()}/content?disposition=invalid");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        
        var err = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("ATTACHMENT_DISPOSITION_INVALID", err?.Error);
    }

    [Fact]
    public async Task GetContent_ValidImage_ReturnsInlineAndSecurityHeaders()
    {
        PermissionService.AllowedPermissions.Add("files.read");
        var attachmentId = Guid.NewGuid();
        var key = "test.png";

        // Store fake object data in FakeObjectStorageProvider
        var fakeStorage = Services.GetRequiredService<FakeObjectStorageProvider>();
        var fileBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }; // PNG Magic
        using var ms = new MemoryStream(fileBytes);
        await fakeStorage.PutAsync(key, ms, "image/png", default);

        var filesDb = Services.GetRequiredService<FilesDbContext>();
        filesDb.FileAttachments.Add(new FileAttachment
        {
            Id = attachmentId,
            TenantId = TestTenantId,
            EntityType = "PRODUCT",
            EntityId = Guid.NewGuid(),
            FileName = "test.png",
            ContentType = "image/png",
            SizeBytes = fileBytes.Length,
            Kind = "IMAGE",
            Provider = "FAKE",
            StorageKey = key,
            PublicUrl = "/fake/test.png"
        });
        await filesDb.SaveChangesAsync();

        var response = await Client.GetAsync($"/api/files/attachments/{attachmentId}/content?disposition=inline");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.ToString());
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Contains("private", Assert.Single(response.Headers.GetValues("Cache-Control")), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("inline; filename=test.png", response.Content.Headers.ContentDisposition?.ToString());
    }

    [Fact]
    public async Task GetContent_CsvInline_ForcesAttachmentMode()
    {
        PermissionService.AllowedPermissions.Add("files.read");
        var attachmentId = Guid.NewGuid();
        var key = "data.csv";

        var fakeStorage = Services.GetRequiredService<FakeObjectStorageProvider>();
        var fileBytes = System.Text.Encoding.UTF8.GetBytes("a,b,c");
        using var ms = new MemoryStream(fileBytes);
        await fakeStorage.PutAsync(key, ms, "text/csv", default);

        var filesDb = Services.GetRequiredService<FilesDbContext>();
        filesDb.FileAttachments.Add(new FileAttachment
        {
            Id = attachmentId,
            TenantId = TestTenantId,
            EntityType = "PRODUCT",
            EntityId = Guid.NewGuid(),
            FileName = "data.csv",
            ContentType = "text/csv",
            SizeBytes = fileBytes.Length,
            Kind = "DOCUMENT",
            Provider = "FAKE",
            StorageKey = key,
            PublicUrl = "/fake/data.csv"
        });
        await filesDb.SaveChangesAsync();

        var response = await Client.GetAsync($"/api/files/attachments/{attachmentId}/content?disposition=inline");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("attachment; filename=data.csv", response.Content.Headers.ContentDisposition?.ToString());
    }

    [Fact]
    public async Task GetContent_HeaderInjectionSafe_SanitizesFileName()
    {
        PermissionService.AllowedPermissions.Add("files.read");
        var attachmentId = Guid.NewGuid();
        var key = "unsafe.png";

        var fakeStorage = Services.GetRequiredService<FakeObjectStorageProvider>();
        var fileBytes = new byte[] { 1, 2, 3 };
        using var ms = new MemoryStream(fileBytes);
        await fakeStorage.PutAsync(key, ms, "image/png", default);

        var filesDb = Services.GetRequiredService<FilesDbContext>();
        filesDb.FileAttachments.Add(new FileAttachment
        {
            Id = attachmentId,
            TenantId = TestTenantId,
            EntityType = "PRODUCT",
            EntityId = Guid.NewGuid(),
            FileName = "test\r\nunsafe\"name'.png",
            ContentType = "image/png",
            SizeBytes = fileBytes.Length,
            Kind = "IMAGE",
            Provider = "FAKE",
            StorageKey = key,
            PublicUrl = "/fake/unsafe.png"
        });
        await filesDb.SaveChangesAsync();

        var response = await Client.GetAsync($"/api/files/attachments/{attachmentId}/content?disposition=inline");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("testunsafename.png", response.Content.Headers.ContentDisposition?.ToString());
    }


    [Theory]
    [InlineData("PRODUCT")]
    [InlineData("QC_RESULT")]
    [InlineData("INBOUND_ORDER")]
    [InlineData("SHIPMENT")]
    [InlineData("STOCKTAKE")]
    [InlineData("RMA_REQUEST")]
    public async Task Bind_AllSixEntityTypes_ShouldSucceed(string entityType)
    {
        // 1. Seed entity
        var entityId = Guid.NewGuid();
        await SeedEntityAsync(entityType, entityId);

        var filesDb = Services.GetRequiredService<FilesDbContext>();
        var uploadId = Guid.NewGuid();
        var key = $"upload_{entityType}_{Guid.NewGuid()}.png";

        // Seed fake storage object
        var fakeStorage = Services.GetRequiredService<FakeObjectStorageProvider>();
        var fileBytes = new byte[] { 1, 2, 3, 4 };
        using (var ms = new MemoryStream(fileBytes))
        {
            await fakeStorage.PutAsync(key, ms, "image/png", default);
        }

        filesDb.FilePendingUploads.Add(new FilePendingUpload
        {
            Id = uploadId,
            TenantId = TestTenantId,
            FileName = "test.png",
            ContentType = "image/png",
            SizeBytes = fileBytes.Length,
            Kind = "IMAGE",
            Provider = "FAKE",
            StorageKey = key,
            LegacyUrl = $"/fake/{key}",
            Status = "PENDING",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(2)
        });
        await filesDb.SaveChangesAsync();

        // 2. Bind with files.upload permission
        PermissionService.AllowedPermissions.Clear();
        var response = await Client.PostAsJsonAsync("/api/files/attachments", new BindAttachmentRequest(uploadId, entityType, entityId));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        PermissionService.AllowedPermissions.Add("files.upload");
        response = await Client.PostAsJsonAsync("/api/files/attachments", new BindAttachmentRequest(uploadId, entityType, entityId));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var bound = await response.Content.ReadFromJsonAsync<AttachmentDto>();
        Assert.NotNull(bound);
        Assert.Equal(entityType, bound!.EntityType);
        Assert.Equal(entityId, bound.EntityId);
        
        // Ensure storageKey/uploads not present in returned DTO
        var jsonText = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("storageKey", jsonText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("uploads", jsonText, StringComparison.OrdinalIgnoreCase);

        var attachmentId = bound.Id;

        // 3. List attachments
        PermissionService.AllowedPermissions.Clear();
        response = await Client.GetAsync($"/api/files/attachments?entityType={entityType}&entityId={entityId}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        PermissionService.AllowedPermissions.Add("files.read");
        response = await Client.GetAsync($"/api/files/attachments?entityType={entityType}&entityId={entityId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var listResult = await response.Content.ReadFromJsonAsync<AttachmentListResponse>();
        Assert.NotNull(listResult);
        Assert.Single(listResult!.Items);
        Assert.Equal(attachmentId, listResult.Items[0].Id);

        // 4. Get content (inline)
        PermissionService.AllowedPermissions.Clear();
        response = await Client.GetAsync($"/api/files/attachments/{attachmentId}/content?disposition=inline");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        PermissionService.AllowedPermissions.Add("files.read");
        response = await Client.GetAsync($"/api/files/attachments/{attachmentId}/content?disposition=inline");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.ToString());
        Assert.Equal("inline; filename=test.png", response.Content.Headers.ContentDisposition?.ToString());
        Assert.Equal(fileBytes, await response.Content.ReadAsByteArrayAsync());

        response = await Client.GetAsync($"/api/files/attachments/{attachmentId}/content?disposition=attachment");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("attachment; filename=test.png", response.Content.Headers.ContentDisposition?.ToString());
        Assert.Equal(fileBytes, await response.Content.ReadAsByteArrayAsync());

        // 5. Delete attachment
        PermissionService.AllowedPermissions.Clear();
        response = await Client.DeleteAsync($"/api/files/attachments/{attachmentId}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        PermissionService.AllowedPermissions.Add("files.delete");
        response = await Client.DeleteAsync($"/api/files/attachments/{attachmentId}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // 6. Verify 404 and empty list after delete
        PermissionService.AllowedPermissions.Add("files.read");
        response = await Client.GetAsync($"/api/files/attachments?entityType={entityType}&entityId={entityId}");
        listResult = await response.Content.ReadFromJsonAsync<AttachmentListResponse>();
        Assert.Empty(listResult!.Items);

        response = await Client.GetAsync($"/api/files/attachments/{attachmentId}/content?disposition=inline");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private record AttachmentListResponse(System.Collections.Generic.List<AttachmentDto> Items);

    private async Task SeedEntityAsync(string entityType, Guid entityId)
    {
        switch (entityType)
        {
            case "PRODUCT":
                var masterData = Services.GetRequiredService<MasterDataDbContext>();
                masterData.Products.Add(new Product { Id = entityId, TenantId = TestTenantId, Code = "SKU1", Name = "P1", BaseUomId = Guid.NewGuid() });
                await masterData.SaveChangesAsync();
                break;
            case "SHIPMENT":
                var inventory = Services.GetRequiredService<InventoryDbContext>();
                inventory.Shipments.Add(new Shipment { Id = entityId, TenantId = TestTenantId, ShipmentNo = "SH1", Status = "DRAFT", CreatedBy = "System" });
                await inventory.SaveChangesAsync();
                break;
            case "STOCKTAKE":
                var inv = Services.GetRequiredService<InventoryDbContext>();
                inv.Stocktakes.Add(new Stocktake { Id = entityId, TenantId = TestTenantId, StocktakeNo = "ST1", Status = "Draft", CreatedBy = "System" });
                await inv.SaveChangesAsync();
                break;
            case "INBOUND_ORDER":
                var inbound = Services.GetRequiredService<Nexustock.Modules.Inbound.Contexts.InboundDbContext>();
                inbound.InboundOrders.Add(new Nexustock.Modules.Inbound.Entities.InboundOrder { Id = entityId, TenantId = TestTenantId, OrderNo = "IO1", Status = Nexustock.Modules.Inbound.Entities.InboundOrderStatus.Draft });
                await inbound.SaveChangesAsync();
                break;
            case "RMA_REQUEST":
                var rma = Services.GetRequiredService<Nexustock.Modules.Rma.Contexts.RmaDbContext>();
                rma.RmaRequests.Add(new Nexustock.Modules.Rma.Entities.RmaRequest { Id = entityId, TenantId = TestTenantId, RmaNo = "RMA1", Status = "OPEN" });
                await rma.SaveChangesAsync();
                break;
            case "QC_RESULT":
                var qc = Services.GetRequiredService<Nexustock.Modules.Qc.Contexts.QcDbContext>();
                qc.QcResults.Add(new Nexustock.Modules.Qc.Entities.QcResult { Id = entityId, TenantId = TestTenantId, Inspector = "System", QcRequestId = Guid.NewGuid(), IsPassed = true });
                await qc.SaveChangesAsync();
                break;

            default:
                break;
        }
    }



    private record ErrorResponse(string Error, string Message);
}
