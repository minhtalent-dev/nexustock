using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nexustock.Modules.Exceptions.Contexts;
using Nexustock.Modules.Exceptions.Entities;
using Nexustock.Modules.Files.Contexts;
using Nexustock.Modules.Files.Dtos;
using Nexustock.Modules.Files.Entities;
using Nexustock.Modules.Files.Services;
using Nexustock.Modules.Inbound.Contexts;
using Nexustock.Modules.Inbound.Entities;
using Nexustock.Modules.Inventory.Contexts;
using Nexustock.Modules.Inventory.Entities;
using Nexustock.Modules.Lpn.Contexts;
using Nexustock.Modules.Lpn.Entities;
using Xunit;

namespace Nexustock.MasterData.IntegrationTests;

[Trait("Category", "Phase46E")]
public class RfAttachmentAcceptanceTests : IntegrationTestBase
{
    public RfAttachmentAcceptanceTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    private static readonly Guid TestTenantId = Guid.Parse(TestAuthConstants.TenantId);

    [Fact]
    public async Task RfCamera_BindToTargetEntities_Succeeds_AndEnforcesSourceValidation()
    {
        var filesDb = Services.GetRequiredService<FilesDbContext>();
        var inventoryDb = Services.GetRequiredService<InventoryDbContext>();
        var inboundDb = Services.GetRequiredService<InboundDbContext>();
        var exceptionsDb = Services.GetRequiredService<ExceptionsDbContext>();
        var lpnDb = Services.GetRequiredService<LpnDbContext>();
        var service = Services.GetRequiredService<IAttachmentService>();

        // 1. Seed entity fixtures for INBOUND_ORDER, SHIPMENT, EXCEPTION, LPN
        var inboundOrder = new InboundOrder
        {
            Id = Guid.NewGuid(),
            TenantId = TestTenantId,
            OrderNo = "PO-RF-001",
            PartnerId = Guid.NewGuid(),
            Status = InboundOrderStatus.Open,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Test"
        };
        inboundDb.InboundOrders.Add(inboundOrder);

        var shipment = new Shipment
        {
            Id = Guid.NewGuid(),
            TenantId = TestTenantId,
            ShipmentNo = "SHP-RF-001",
            PartnerId = Guid.NewGuid(),
            Status = "Open",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Test"
        };
        inventoryDb.Shipments.Add(shipment);

        var opException = new OperationalException
        {
            Id = Guid.NewGuid(),
            TenantId = TestTenantId,
            Code = "EXP-RF-001",
            Type = "DAMAGED_GOODS",
            ReasonCode = "DAMAGE",
            ReferenceType = "INBOUND",
            ReferenceId = Guid.NewGuid(),
            Status = "OPEN",
            Severity = "HIGH",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Test"
        };
        exceptionsDb.OperationalExceptions.Add(opException);

        var lpn = new Lpn
        {
            Id = Guid.NewGuid(),
            TenantId = TestTenantId,
            LpnNo = "LPN-RF-001",
            LocationId = Guid.NewGuid(),
            Status = "ACTIVE",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Test"
        };
        lpnDb.Lpns.Add(lpn);

        await inboundDb.SaveChangesAsync();
        await inventoryDb.SaveChangesAsync();
        await exceptionsDb.SaveChangesAsync();
        await lpnDb.SaveChangesAsync();

        // 2. Seed pending uploads
        var uploadId1 = Guid.NewGuid();
        var uploadId2 = Guid.NewGuid();
        var uploadId3 = Guid.NewGuid();
        var uploadId4 = Guid.NewGuid();

        filesDb.FilePendingUploads.AddRange(
            new FilePendingUpload
            {
                Id = uploadId1,
                TenantId = TestTenantId,
                FileName = "inbound_photo.jpg",
                ContentType = "image/jpeg",
                SizeBytes = 204800,
                Kind = "IMAGE",
                Provider = "LOCAL",
                StorageKey = "inbound_photo.jpg",
                LegacyUrl = "/fake/inbound_photo.jpg",
                Status = "PENDING",
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(2)
            },
            new FilePendingUpload
            {
                Id = uploadId2,
                TenantId = TestTenantId,
                FileName = "shipment_photo.jpg",
                ContentType = "image/jpeg",
                SizeBytes = 307200,
                Kind = "IMAGE",
                Provider = "LOCAL",
                StorageKey = "shipment_photo.jpg",
                LegacyUrl = "/fake/shipment_photo.jpg",
                Status = "PENDING",
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(2)
            },
            new FilePendingUpload
            {
                Id = uploadId3,
                TenantId = TestTenantId,
                FileName = "exception_photo.jpg",
                ContentType = "image/jpeg",
                SizeBytes = 153600,
                Kind = "IMAGE",
                Provider = "LOCAL",
                StorageKey = "exception_photo.jpg",
                LegacyUrl = "/fake/exception_photo.jpg",
                Status = "PENDING",
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(2)
            },
            new FilePendingUpload
            {
                Id = uploadId4,
                TenantId = TestTenantId,
                FileName = "lpn_photo.jpg",
                ContentType = "image/jpeg",
                SizeBytes = 409600,
                Kind = "IMAGE",
                Provider = "LOCAL",
                StorageKey = "lpn_photo.jpg",
                LegacyUrl = "/fake/lpn_photo.jpg",
                Status = "PENDING",
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(2)
            }
        );
        await filesDb.SaveChangesAsync();

        // 3. Bind RF_CAMERA to each entity
        var bound1 = await service.BindAsync(new BindAttachmentRequest(uploadId1, "INBOUND_ORDER", inboundOrder.Id, "RF_CAMERA"), "User1", default);
        Assert.NotNull(bound1);
        Assert.Equal("INBOUND_ORDER", bound1.EntityType);

        var bound2 = await service.BindAsync(new BindAttachmentRequest(uploadId2, "SHIPMENT", shipment.Id, "RF_CAMERA"), "User1", default);
        Assert.NotNull(bound2);

        var bound3 = await service.BindAsync(new BindAttachmentRequest(uploadId3, "EXCEPTION", opException.Id, "RF_CAMERA"), "User1", default);
        Assert.NotNull(bound3);

        var bound4 = await service.BindAsync(new BindAttachmentRequest(uploadId4, "LPN", lpn.Id, "RF_CAMERA"), "User1", default);
        Assert.NotNull(bound4);

        // 4. Idempotency retry test
        var retryBound1 = await service.BindAsync(new BindAttachmentRequest(uploadId1, "INBOUND_ORDER", inboundOrder.Id, "RF_CAMERA"), "User1", default);
        Assert.Equal(bound1.Id, retryBound1.Id);

        var totalInboundAttachments = await filesDb.FileAttachments
            .CountAsync(a => a.EntityType == "INBOUND_ORDER" && a.EntityId == inboundOrder.Id && a.DeletedAt == null);
        Assert.Equal(1, totalInboundAttachments);

        // 5. Source validation tests
        var invalidUploadId = Guid.NewGuid();
        filesDb.FilePendingUploads.Add(new FilePendingUpload
        {
            Id = invalidUploadId,
            TenantId = TestTenantId,
            FileName = "bad_source.jpg",
            ContentType = "image/jpeg",
            SizeBytes = 100,
            Kind = "IMAGE",
            Provider = "LOCAL",
            StorageKey = "bad_source.jpg",
            LegacyUrl = "/fake/bad_source.jpg",
            Status = "PENDING",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(2)
        });
        await filesDb.SaveChangesAsync();

        var exBadSource = await Assert.ThrowsAsync<FileDomainException>(() =>
            service.BindAsync(new BindAttachmentRequest(invalidUploadId, "INBOUND_ORDER", inboundOrder.Id, "INVALID_SOURCE"), "User1", default));
        Assert.Equal("ATTACHMENT_SOURCE_NOT_ALLOWED", exBadSource.ErrorCode);

        // 6. Fake Entity ID tests
        var uploadIdFake = Guid.NewGuid();
        filesDb.FilePendingUploads.Add(new FilePendingUpload
        {
            Id = uploadIdFake,
            TenantId = TestTenantId,
            FileName = "fake_entity.jpg",
            ContentType = "image/jpeg",
            SizeBytes = 100,
            Kind = "IMAGE",
            Provider = "LOCAL",
            StorageKey = "fake_entity.jpg",
            LegacyUrl = "/fake/fake_entity.jpg",
            Status = "PENDING",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(2)
        });
        await filesDb.SaveChangesAsync();

        var fakeEntityId = Guid.NewGuid();
        var exFakeEntity = await Assert.ThrowsAsync<FileDomainException>(() =>
            service.BindAsync(new BindAttachmentRequest(uploadIdFake, "INBOUND_ORDER", fakeEntityId, "RF_CAMERA"), "User1", default));
        Assert.Equal("ATTACHMENT_ENTITY_NOT_FOUND", exFakeEntity.ErrorCode);
    }
}
