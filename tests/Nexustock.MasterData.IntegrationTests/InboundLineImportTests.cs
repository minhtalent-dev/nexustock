using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nexustock.Modules.Inbound.Contexts;
using Nexustock.Modules.Inbound.Entities;
using Nexustock.Modules.MasterData.Contexts;
using Nexustock.Modules.MasterData.Entities;
using Xunit;

namespace Nexustock.MasterData.IntegrationTests;

public class InboundLineImportTests : IntegrationTestBase
{
    public InboundLineImportTests(CustomWebApplicationFactory factory) : base(factory)
    {
        PermissionService.AllowedPermissions.Add("Inbound.Orders.Create");
    }

    [Fact]
    public async Task PreviewAndCommit_InboundLines_ValidFile_ImportsSuccessfully()
    {
        Guid orderId = Guid.NewGuid();
        string sku = $"INB_SKU_{Guid.NewGuid():N}"[..15];
        string uomCode = $"INB_UOM_{Guid.NewGuid():N}"[..10];

        using (var scope = Services.CreateScope())
        {
            var masterDb = scope.ServiceProvider.GetRequiredService<MasterDataDbContext>();
            var inboundDb = scope.ServiceProvider.GetRequiredService<InboundDbContext>();

            var uom = new Uom
            {
                Id = Guid.NewGuid(),
                TenantId = masterDb.CurrentTenantId,
                Code = uomCode,
                Name = "Inbound UOM",
                IsActive = true
            };
            var product = new Product
            {
                Id = Guid.NewGuid(),
                TenantId = masterDb.CurrentTenantId,
                Code = sku,
                Name = "Inbound Test Product",
                BaseUomId = uom.Id,
                IsActive = true
            };
            masterDb.Uoms.Add(uom);
            masterDb.Products.Add(product);
            await masterDb.SaveChangesAsync();

            var order = new InboundOrder
            {
                Id = orderId,
                TenantId = inboundDb.CurrentTenantId,
                OrderNo = $"PO_{Guid.NewGuid():N}"[..10],
                PartnerId = Guid.NewGuid(),
                Status = InboundOrderStatus.Draft,
                CreatedAt = DateTime.UtcNow
            };
            inboundDb.InboundOrders.Add(order);
            await inboundDb.SaveChangesAsync();
        }

        var csv = $"sku,uomCode,expectedQty,tolerance\n{sku},{uomCode},50,0.05";
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        content.Add(fileContent, "file", "inbound_lines.csv");

        var previewRes = await Client.PostAsync($"/api/inbound/{orderId}/lines/import/preview", content);
        Assert.Equal(HttpStatusCode.OK, previewRes.StatusCode);

        var previewJson = JsonDocument.Parse(await previewRes.Content.ReadAsStringAsync());
        var batchId = previewJson.RootElement.GetProperty("batchId").GetGuid();
        Assert.Equal(0, previewJson.RootElement.GetProperty("errorRows").GetInt32());

        // Commit batch
        var commitRes = await Client.PostAsync($"/api/inbound/{orderId}/lines/import/commit",
            new StringContent(JsonSerializer.Serialize(new { batchId }), Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, commitRes.StatusCode);

        // Verify InboundOrderItem in DB
        using (var scope = Services.CreateScope())
        {
            var inboundDb = scope.ServiceProvider.GetRequiredService<InboundDbContext>();
            var item = await inboundDb.InboundOrderItems.FirstOrDefaultAsync(i => i.InboundOrderId == orderId);
            Assert.NotNull(item);
            Assert.Equal(50m, item.ExpectedQty);
            Assert.Equal(0.05m, item.Tolerance);
            Assert.Equal(0m, item.ReceivedQty);
        }
    }

    [Fact]
    public async Task Preview_InboundLines_WithReceivedQty_RejectsRow()
    {
        Guid orderId = Guid.NewGuid();
        string sku = $"INB_RCV_{Guid.NewGuid():N}"[..15];
        string uomCode = $"UOM_RCV_{Guid.NewGuid():N}"[..10];

        using (var scope = Services.CreateScope())
        {
            var masterDb = scope.ServiceProvider.GetRequiredService<MasterDataDbContext>();
            var inboundDb = scope.ServiceProvider.GetRequiredService<InboundDbContext>();

            var uom = new Uom { Id = Guid.NewGuid(), TenantId = masterDb.CurrentTenantId, Code = uomCode, Name = "UOM", IsActive = true };
            var product = new Product { Id = Guid.NewGuid(), TenantId = masterDb.CurrentTenantId, Code = sku, Name = "Product", BaseUomId = uom.Id, IsActive = true };
            masterDb.Uoms.Add(uom);
            masterDb.Products.Add(product);
            await masterDb.SaveChangesAsync();

            var order = new InboundOrder
            {
                Id = orderId,
                TenantId = inboundDb.CurrentTenantId,
                OrderNo = $"PO_RCV_{Guid.NewGuid():N}"[..10],
                PartnerId = Guid.NewGuid(),
                Status = InboundOrderStatus.Open,
                CreatedAt = DateTime.UtcNow
            };
            order.Items.Add(new InboundOrderItem
            {
                Id = Guid.NewGuid(),
                TenantId = inboundDb.CurrentTenantId,
                InboundOrderId = orderId,
                ItemId = product.Id,
                UomId = uom.Id,
                ExpectedQty = 20,
                ReceivedQty = 5, // Already received 5!
                Tolerance = 0
            });
            inboundDb.InboundOrders.Add(order);
            await inboundDb.SaveChangesAsync();
        }

        var csv = $"sku,uomCode,expectedQty,tolerance\n{sku},{uomCode},100,0";
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        content.Add(fileContent, "file", "lines.csv");

        var previewRes = await Client.PostAsync($"/api/inbound/{orderId}/lines/import/preview", content);
        Assert.Equal(HttpStatusCode.OK, previewRes.StatusCode);

        var json = JsonDocument.Parse(await previewRes.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("errorRows").GetInt32() > 0);
    }
}
