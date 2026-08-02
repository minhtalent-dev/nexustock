using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nexustock.Modules.MasterData.Contexts;
using Nexustock.Modules.Files.Contexts;
using Nexustock.Modules.Files.Dtos;
using Nexustock.Modules.Files.Entities;
using Nexustock.Modules.Files.Providers;
using Nexustock.Modules.Files.Services;
using Nexustock.Modules.Inbound.Contexts;
using Nexustock.Modules.Inbound.Entities;
using Nexustock.Modules.Exceptions.Contexts;
using Nexustock.Modules.Exceptions.Entities;
using Nexustock.Modules.Lpn.Contexts;
using Nexustock.Modules.Lpn.Entities;
using Nexustock.Modules.Wave.Contexts;
using Nexustock.Modules.Wave.Entities;
using Nexustock.Modules.Putaway.Contexts;
using Nexustock.Modules.Putaway.Entities;
using Nexustock.Modules.CrossDocking.Contexts;
using Nexustock.Modules.CrossDocking.Entities;
using Nexustock.Modules.Qc.Contexts;
using Nexustock.Modules.Qc.Entities;
using Nexustock.Modules.Rma.Contexts;
using Nexustock.Modules.Rma.Entities;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace Nexustock.MasterData.IntegrationTests;

public class AttachmentThumbnailTests : IntegrationTestBase
{
    public AttachmentThumbnailTests(CustomWebApplicationFactory factory) : base(factory)
    {
        _thumbnailService = Services.GetRequiredService<IThumbnailService>();
        _httpContextAccessor = Services.GetRequiredService<IHttpContextAccessor>();
        _handlers = Services.GetServices<IEntityExistenceHandler>();
    }

    private readonly IThumbnailService _thumbnailService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IEnumerable<IEntityExistenceHandler> _handlers;

    private async Task<byte[]> CreateTestImageBytesAsync(int width = 100, int height = 100)
    {
        using var img = new Image<Rgba32>(width, height);
        using var ms = new MemoryStream();
        await img.SaveAsJpegAsync(ms);
        return ms.ToArray();
    }

    private void SetMockUserTenant(Guid tenantId)
    {
        var claims = new[] { new Claim("tenantId", tenantId.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        _httpContextAccessor.HttpContext = new DefaultHttpContext { User = principal };
    }

    private IEntityExistenceHandler GetHandler(string entityType)
    {
        foreach (var h in _handlers)
        {
            if (h.CanHandle(entityType))
                return h;
        }
        throw new Exception($"Handler not found for {entityType}");
    }

    [Fact]
    public async Task Attachment_Thumbnail_And_ExistenceHandlers_AllFlows_Combined()
    {
        // =========================================================================
        // PART 1: THUMBNAIL SERVICE LOGIC
        // =========================================================================
        {
            // 1. CAN GENERATE VALID
            var jpegHeader = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01 };
            Assert.True(_thumbnailService.CanGenerate("image/jpeg", jpegHeader));
            Assert.True(_thumbnailService.CanGenerate("image/jpg", jpegHeader));

            var pngHeader = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D };
            Assert.True(_thumbnailService.CanGenerate("image/png", pngHeader));

            var webpHeader = new byte[] { 0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50 };
            Assert.True(_thumbnailService.CanGenerate("image/webp", webpHeader));

            // 2. CAN GENERATE INVALID / SHORT
            var pdfHeader = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34, 0x0A, 0x25, 0xE2, 0xE3 };
            Assert.False(_thumbnailService.CanGenerate("application/pdf", pdfHeader));
            Assert.False(_thumbnailService.CanGenerate("image/jpeg", new byte[] { 0xFF, 0xD8 }));

            // 3. BUILD KEY
            var originalKey = "tenant1/photos/item-abc.png";
            var expectedThumbKey = "tenant1/photos/item-abc.png.thumb.jpg";
            var actualThumbKey = _thumbnailService.BuildKey(originalKey);
            Assert.Equal(expectedThumbKey, actualThumbKey);

            var originalKey2 = "/tenant2/files/image.JPG";
            var expectedThumbKey2 = "/tenant2/files/image.JPG.thumb.jpg";
            var actualThumbKey2 = _thumbnailService.BuildKey(originalKey2);
            Assert.Equal(expectedThumbKey2, actualThumbKey2);

            // 4. GENERATE RESIZE ASPECT RATIO
            var originalBytes = await CreateTestImageBytesAsync(800, 600);
            using var origStream = new MemoryStream(originalBytes);

            using var thumbStream = await _thumbnailService.GenerateAsync(origStream, default);
            Assert.NotNull(thumbStream);
            Assert.True(thumbStream.Length > 0);

            thumbStream.Position = 0;
            using var thumbImg = await Image.LoadAsync<Rgba32>(thumbStream);
            Assert.True(thumbImg.Width <= 256);
            Assert.True(thumbImg.Height <= 256);
            Assert.Equal(256, thumbImg.Width);
            Assert.Equal(192, thumbImg.Height);

            // 5. GENERATE TOO LARGE DIMENSIONS
            var hugeBytes = await CreateTestImageBytesAsync(6000, 100);
            using var hugeStream = new MemoryStream(hugeBytes);

            await Assert.ThrowsAsync<InvalidDataException>(async () =>
            {
                await _thumbnailService.GenerateAsync(hugeStream, default);
            });
        }

        // =========================================================================
        // PART 2: ATTACHMENT LIFECYCLE (Upload -> Bind -> Get -> Delete)
        // =========================================================================
        {
            PermissionService.AllowedPermissions.Add("files.upload");
            PermissionService.AllowedPermissions.Add("files.read");
            PermissionService.AllowedPermissions.Add("files.delete");

            using (var scope = Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<FilesDbContext>();
                db.FileStorageSettings.RemoveRange(db.FileStorageSettings);

                var settings = new FileStorageSettings
                {
                    Id = Guid.NewGuid(),
                    TenantId = db.CurrentTenantId,
                    ActiveProvider = "FAKE",
                    IsEnabled = true,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                db.FileStorageSettings.Add(settings);
                await db.SaveChangesAsync();
            }

            var imageBytes = await CreateTestImageBytesAsync();
            using var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(imageBytes);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
            content.Add(fileContent, "file", "test_photo.jpg");

            var response = await Client.PostAsync("/api/files/upload", content);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var uploadResult = JsonSerializer.Deserialize<UploadResultDto>(
                await response.Content.ReadAsStringAsync(),
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
            );
            Assert.NotNull(uploadResult);

            using (var scope = Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<FilesDbContext>();
                var storageProvider = scope.ServiceProvider.GetRequiredService<FakeObjectStorageProvider>();

                var pending = await db.FilePendingUploads.FirstOrDefaultAsync(p => p.Id == uploadResult.UploadId);
                Assert.NotNull(pending);
                Assert.NotNull(pending.ThumbnailKey);
                Assert.True(await storageProvider.ExistsAsync(pending.StorageKey, default));
                Assert.True(await storageProvider.ExistsAsync(pending.ThumbnailKey, default));
            }

            var productId = Guid.NewGuid();
            using (var scope = Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<FilesDbContext>();
                var mdDb = scope.ServiceProvider.GetRequiredService<MasterDataDbContext>();
                
                var product = new Nexustock.Modules.MasterData.Entities.Product
                {
                    Id = productId,
                    TenantId = db.CurrentTenantId,
                    Code = "TEST_PROD_LIFE",
                    Name = "Test Product Lifecycle",
                    BaseUomId = Guid.NewGuid(),
                    IsActive = true
                };
                mdDb.Products.Add(product);
                await mdDb.SaveChangesAsync();
            }

            var bindPayload = new BindAttachmentRequest(uploadResult.UploadId, "PRODUCT", productId);
            var bindResponse = await Client.PostAsJsonAsync("/api/files/attachments", bindPayload);
            if (bindResponse.StatusCode != HttpStatusCode.OK)
            {
                var body = await bindResponse.Content.ReadAsStringAsync();
                throw new Exception($"Bind failed: {bindResponse.StatusCode}. Body: {body}");
            }

            var attachment = JsonSerializer.Deserialize<AttachmentDto>(
                await bindResponse.Content.ReadAsStringAsync(),
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
            );
            Assert.NotNull(attachment);
            Assert.NotNull(attachment.ThumbnailUrl);

            string storageKey;
            string thumbnailKey;
            using (var scope = Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<FilesDbContext>();
                var attInDb = await db.FileAttachments.FirstOrDefaultAsync(a => a.Id == attachment.Id);
                Assert.NotNull(attInDb);
                Assert.NotNull(attInDb.ThumbnailKey);
                storageKey = attInDb.StorageKey;
                thumbnailKey = attInDb.ThumbnailKey;
            }

            var thumbResponse = await Client.GetAsync(attachment.ThumbnailUrl);
            Assert.Equal(HttpStatusCode.OK, thumbResponse.StatusCode);
            Assert.Equal("image/jpeg", thumbResponse.Content.Headers.ContentType?.MediaType);
            Assert.Contains("private", thumbResponse.Headers.CacheControl?.ToString());
            Assert.True(thumbResponse.Headers.Contains("ETag"));

            var etag = thumbResponse.Headers.ETag?.Tag;
            Assert.NotNull(etag);

            var request304 = new HttpRequestMessage(HttpMethod.Get, attachment.ThumbnailUrl);
            request304.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(etag));
            var response304 = await Client.SendAsync(request304);
            Assert.Equal(HttpStatusCode.NotModified, response304.StatusCode);

            var deleteResponse = await Client.DeleteAsync($"/api/files/attachments/{attachment.Id}");
            if (deleteResponse.StatusCode != HttpStatusCode.NoContent)
            {
                var body = await deleteResponse.Content.ReadAsStringAsync();
                throw new Exception($"Delete failed: {deleteResponse.StatusCode}. Body: {body}");
            }

            using (var scope = Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<FilesDbContext>();
                var storageProvider = scope.ServiceProvider.GetRequiredService<FakeObjectStorageProvider>();

                var deletedAtt = await db.FileAttachments.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.Id == attachment.Id);
                Assert.NotNull(deletedAtt);
                Assert.NotNull(deletedAtt.ObjectsPurgedAt);
                Assert.False(await storageProvider.ExistsAsync(storageKey, default));
                Assert.False(await storageProvider.ExistsAsync(thumbnailKey, default));
            }

            // ==========================================
            // FLOW 2: EXPIRATION CLEANUP
            // ==========================================
            var keyExp = "";
            var thumbKeyExp = "";
            var expiredPendingId = Guid.NewGuid();

            using (var scope = Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<FilesDbContext>();
                var storageProvider = scope.ServiceProvider.GetRequiredService<FakeObjectStorageProvider>();

                keyExp = $"{db.CurrentTenantId:N}/expired.jpg";
                thumbKeyExp = $"{keyExp}.thumb.jpg";

                using var stream1 = new MemoryStream(imageBytes);
                using var stream2 = new MemoryStream(imageBytes);
                await storageProvider.PutAsync(keyExp, stream1, "image/jpeg", default);
                await storageProvider.PutAsync(thumbKeyExp, stream2, "image/jpeg", default);

                var expiredPending = new FilePendingUpload
                {
                    Id = expiredPendingId,
                    TenantId = db.CurrentTenantId,
                    FileName = "expired.jpg",
                    ContentType = "image/jpeg",
                    SizeBytes = imageBytes.Length,
                    Kind = "IMAGE",
                    Provider = "FAKE",
                    StorageKey = keyExp,
                    ThumbnailKey = thumbKeyExp,
                    Status = "PENDING",
                    CreatedAt = DateTimeOffset.UtcNow.AddDays(-2),
                    ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1)
                };

                db.FilePendingUploads.Add(expiredPending);
                await db.SaveChangesAsync();
            }

            using (var scope = Services.CreateScope())
            {
                var cleanupService = scope.ServiceProvider.GetRequiredService<IPendingUploadCleanupService>();
                int cleaned = await cleanupService.CleanupExpiredPendingUploadsAsync(default);
                Assert.True(cleaned > 0);
            }

            using (var scope = Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<FilesDbContext>();
                var storageProvider = scope.ServiceProvider.GetRequiredService<FakeObjectStorageProvider>();

                var freshPending = await db.FilePendingUploads.FirstOrDefaultAsync(p => p.Id == expiredPendingId);
                Assert.NotNull(freshPending);
                Assert.Equal("PURGED", freshPending.Status);
                Assert.False(await storageProvider.ExistsAsync(keyExp, default));
                Assert.False(await storageProvider.ExistsAsync(thumbKeyExp, default));
            }

        }

        // =========================================================================
        // PART 3: TENANT ISOLATION ON ENTITY EXISTENCE HANDLERS
        // =========================================================================
        {
            var tenantA = Guid.NewGuid();
            var tenantB = Guid.NewGuid();

            // 1. LOT HANDLER
            var lotId = Guid.NewGuid();
            using (var scope = Services.CreateScope())
            {
                var lotDb = scope.ServiceProvider.GetRequiredService<InboundDbContext>();
                lotDb.Lots.Add(new Lot
                {
                    Id = lotId,
                    TenantId = tenantA,
                    LotNo = "LOT-LIFE-001",
                    ItemId = Guid.NewGuid(),
                    QcStatus = LotQcStatus.Release
                });
                await lotDb.SaveChangesAsync();
            }

            var lotHandler = GetHandler("LOT");
            SetMockUserTenant(tenantA);
            Assert.True(await lotHandler.ExistsAsync(lotId, default));
            SetMockUserTenant(tenantB);
            Assert.False(await lotHandler.ExistsAsync(lotId, default));

            // 2. EXCEPTION HANDLER
            var excId = Guid.NewGuid();
            using (var scope = Services.CreateScope())
            {
                var excDb = scope.ServiceProvider.GetRequiredService<ExceptionsDbContext>();
                excDb.OperationalExceptions.Add(new OperationalException
                {
                    Id = excId,
                    TenantId = tenantA,
                    Code = "EX-LIFE-001",
                    Type = "SHORTAGE",
                    Severity = "HIGH",
                    Status = "Open",
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "Tester",
                    ReasonCode = "TEST_REASON",
                    ReferenceType = "INBOUND"
                });
                await excDb.SaveChangesAsync();
            }

            var excHandler = GetHandler("EXCEPTION");
            SetMockUserTenant(tenantA);
            Assert.True(await excHandler.ExistsAsync(excId, default));
            SetMockUserTenant(tenantB);
            Assert.False(await excHandler.ExistsAsync(excId, default));

            // 3. LPN HANDLER
            var lpnId = Guid.NewGuid();
            using (var scope = Services.CreateScope())
            {
                var lpnDb = scope.ServiceProvider.GetRequiredService<LpnDbContext>();
                lpnDb.Lpns.Add(new Lpn
                {
                    Id = lpnId,
                    TenantId = tenantA,
                    LpnNo = "LPN-LIFE-001",
                    LocationId = Guid.NewGuid(),
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "Tester"
                });
                await lpnDb.SaveChangesAsync();
            }

            var lpnHandler = GetHandler("LPN");
            SetMockUserTenant(tenantA);
            Assert.True(await lpnHandler.ExistsAsync(lpnId, default));
            SetMockUserTenant(tenantB);
            Assert.False(await lpnHandler.ExistsAsync(lpnId, default));

            // 4. WAVE HANDLER
            var waveId = Guid.NewGuid();
            using (var scope = Services.CreateScope())
            {
                var waveDb = scope.ServiceProvider.GetRequiredService<WaveDbContext>();
                waveDb.PickingWaves.Add(new PickingWave
                {
                    Id = waveId,
                    TenantId = tenantA,
                    WaveNo = "WAVE-LIFE-001",
                    Status = "DRAFT",
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "Tester"
                });
                await waveDb.SaveChangesAsync();
            }

            var waveHandler = GetHandler("WAVE");
            SetMockUserTenant(tenantA);
            Assert.True(await waveHandler.ExistsAsync(waveId, default));
            SetMockUserTenant(tenantB);
            Assert.False(await waveHandler.ExistsAsync(waveId, default));

            // 5. PUTAWAY HANDLER
            var putawayId = Guid.NewGuid();
            using (var scope = Services.CreateScope())
            {
                var putawayDb = scope.ServiceProvider.GetRequiredService<PutawayDbContext>();
                putawayDb.PutawayProposals.Add(new PutawayProposal
                {
                    Id = putawayId,
                    TenantId = tenantA,
                    LotId = Guid.NewGuid(),
                    CandidateLocationId = Guid.NewGuid(),
                    Score = 9,
                    Reason = "Close to picking area",
                    Status = "PENDING",
                    CreatedAt = DateTime.UtcNow
                });
                await putawayDb.SaveChangesAsync();
            }

            var putawayHandler = GetHandler("PUTAWAY_PROPOSAL");
            SetMockUserTenant(tenantA);
            Assert.True(await putawayHandler.ExistsAsync(putawayId, default));
            SetMockUserTenant(tenantB);
            Assert.False(await putawayHandler.ExistsAsync(putawayId, default));

            // 6. CROSS-DOCK HANDLER
            var cdId = Guid.NewGuid();
            using (var scope = Services.CreateScope())
            {
                var cdDb = scope.ServiceProvider.GetRequiredService<CrossDockingDbContext>();
                cdDb.Candidates.Add(new CrossDockCandidate
                {
                    Id = cdId,
                    TenantId = tenantA,
                    ItemId = Guid.NewGuid(),
                    LotId = Guid.NewGuid(),
                    WaveItemId = Guid.NewGuid(),
                    QtyAvailable = 50,
                    QtyRequested = 50,
                    QtyMatched = 50,
                    MatchScore = 100,
                    Status = CrossDockCandidateStatus.Pending,
                    CreatedAt = DateTimeOffset.UtcNow,
                    CreatedBy = "Tester"
                });
                await cdDb.SaveChangesAsync();
            }

            var cdHandler = GetHandler("CROSS_DOCK_CANDIDATE");
            SetMockUserTenant(tenantA);
            Assert.True(await cdHandler.ExistsAsync(cdId, default));
            SetMockUserTenant(tenantB);
            Assert.False(await cdHandler.ExistsAsync(cdId, default));

            // 7. INBOUND ORDER HANDLER
            var ibId = Guid.NewGuid();
            using (var scope = Services.CreateScope())
            {
                var ibDb = scope.ServiceProvider.GetRequiredService<InboundDbContext>();
                ibDb.InboundOrders.Add(new InboundOrder
                {
                    Id = ibId,
                    TenantId = tenantA,
                    OrderNo = "IB-LIFE-001",
                    PartnerId = Guid.NewGuid(),
                    Status = InboundOrderStatus.Open,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "Tester"
                });
                await ibDb.SaveChangesAsync();
            }

            var ibHandler = GetHandler("INBOUND_ORDER");
            SetMockUserTenant(tenantA);
            Assert.True(await ibHandler.ExistsAsync(ibId, default));
            SetMockUserTenant(tenantB);
            Assert.False(await ibHandler.ExistsAsync(ibId, default));

            // 8. QC RESULT HANDLER
            var qcId = Guid.NewGuid();
            using (var scope = Services.CreateScope())
            {
                var qcDb = scope.ServiceProvider.GetRequiredService<QcDbContext>();
                qcDb.QcResults.Add(new QcResult
                {
                    Id = qcId,
                    TenantId = tenantA,
                    QcRequestId = Guid.NewGuid(),
                    IsPassed = true,
                    Inspector = "Inspector",
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "Tester"
                });
                await qcDb.SaveChangesAsync();
            }

            var qcHandler = GetHandler("QC_RESULT");
            SetMockUserTenant(tenantA);
            Assert.True(await qcHandler.ExistsAsync(qcId, default));
            SetMockUserTenant(tenantB);
            Assert.False(await qcHandler.ExistsAsync(qcId, default));

            // 9. RMA REQUEST HANDLER
            var rmaId = Guid.NewGuid();
            using (var scope = Services.CreateScope())
            {
                var rmaDb = scope.ServiceProvider.GetRequiredService<RmaDbContext>();
                rmaDb.RmaRequests.Add(new RmaRequest
                {
                    Id = rmaId,
                    TenantId = tenantA,
                    RmaNo = "RMA-LIFE-001",
                    CustomerId = Guid.NewGuid(),
                    Status = "OPEN",
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "Tester"
                });
                await rmaDb.SaveChangesAsync();
            }

            var rmaHandler = GetHandler("RMA_REQUEST");
            SetMockUserTenant(tenantA);
            Assert.True(await rmaHandler.ExistsAsync(rmaId, default));
            SetMockUserTenant(tenantB);
            Assert.False(await rmaHandler.ExistsAsync(rmaId, default));
        }
    }
}
