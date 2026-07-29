using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nexustock.Modules.Inventory.Contexts;
using Nexustock.Modules.Inventory.Entities;
using Nexustock.Modules.MasterData.Contexts;
using Nexustock.Modules.MasterData.Entities;
using Xunit;

namespace Nexustock.MasterData.IntegrationTests;

public class StocktakeCountImportTests : IntegrationTestBase
{
    public StocktakeCountImportTests(CustomWebApplicationFactory factory) : base(factory)
    {
        PermissionService.AllowedPermissions.Add("Inventory.CycleCount.Count");
    }

    [Fact]
    public async Task PreviewAndCommit_StocktakeCounts_ValidFile_UpdatesCountsSuccessfully()
    {
        Guid stocktakeId = Guid.NewGuid();
        string locCode = $"LOC_{Guid.NewGuid():N}"[..10];
        string sku = $"STK_SKU_{Guid.NewGuid():N}"[..15];
        string uomCode = $"STK_UOM_{Guid.NewGuid():N}"[..10];
        string lotNo = $"LOT_{Guid.NewGuid():N}"[..10];

        using (var scope = Services.CreateScope())
        {
            var masterDb = scope.ServiceProvider.GetRequiredService<MasterDataDbContext>();
            var inventoryDb = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();

            var wh = new Warehouse { Id = Guid.NewGuid(), TenantId = masterDb.CurrentTenantId, Code = $"WH_{Guid.NewGuid():N}"[..8], Name = "WH", IsActive = true };
            var zone = new StorageZone { Id = Guid.NewGuid(), TenantId = masterDb.CurrentTenantId, WarehouseId = wh.Id, Code = $"Z_{Guid.NewGuid():N}"[..8], Name = "Zone" };
            var loc = new StorageLocation { Id = Guid.NewGuid(), TenantId = masterDb.CurrentTenantId, ZoneId = zone.Id, Code = locCode, IsActive = true };
            var uom = new Uom { Id = Guid.NewGuid(), TenantId = masterDb.CurrentTenantId, Code = uomCode, Name = "UOM", IsActive = true };
            var product = new Product { Id = Guid.NewGuid(), TenantId = masterDb.CurrentTenantId, Code = sku, Name = "Product", BaseUomId = uom.Id, IsActive = true };

            masterDb.Warehouses.Add(wh);
            masterDb.StorageZones.Add(zone);
            masterDb.StorageLocations.Add(loc);
            masterDb.Uoms.Add(uom);
            masterDb.Products.Add(product);
            await masterDb.SaveChangesAsync();

            var stocktake = new Stocktake
            {
                Id = stocktakeId,
                TenantId = inventoryDb.CurrentTenantId,
                StocktakeNo = $"STK_{Guid.NewGuid():N}"[..10],
                Status = "Counting",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "TEST"
            };
            inventoryDb.Stocktakes.Add(stocktake);
            await inventoryDb.SaveChangesAsync();
        }

        var csv = $"lineNo,locationCode,sku,lotNo,countQty,uomCode\n1,{locCode},{sku},{lotNo},25,{uomCode}";
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        content.Add(fileContent, "file", "counts.csv");

        var previewRes = await Client.PostAsync($"/api/stocktakes/{stocktakeId}/lines/import/preview", content);
        Assert.Equal(HttpStatusCode.OK, previewRes.StatusCode);

        var previewJson = JsonDocument.Parse(await previewRes.Content.ReadAsStringAsync());
        var batchId = previewJson.RootElement.GetProperty("batchId").GetGuid();
        Assert.Equal(0, previewJson.RootElement.GetProperty("errorRows").GetInt32());

        // Commit batch
        var commitRes = await Client.PostAsync($"/api/stocktakes/{stocktakeId}/lines/import/commit",
            new StringContent(JsonSerializer.Serialize(new { batchId }), Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, commitRes.StatusCode);

        // Verify StocktakeItem in DB
        using (var scope = Services.CreateScope())
        {
            var inventoryDb = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            var item = await inventoryDb.StocktakeItems.FirstOrDefaultAsync(si => si.StocktakeId == stocktakeId);
            Assert.NotNull(item);
            Assert.Equal(25m, item.CountedQty);
            Assert.Equal("Counted", item.Status);
        }
    }
}
