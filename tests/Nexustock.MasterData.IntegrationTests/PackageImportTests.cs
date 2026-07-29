using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nexustock.Modules.MasterData.Contexts;
using Nexustock.Modules.MasterData.Entities;
using Xunit;

namespace Nexustock.MasterData.IntegrationTests;

public class PackageImportTests : IntegrationTestBase
{
    public PackageImportTests(CustomWebApplicationFactory factory) : base(factory)
    {
        PermissionService.AllowedPermissions.Add("master_data.import");
        PermissionService.AllowedPermissions.Add("master_data.export");
    }

    [Fact]
    public async Task GetTemplate_Packages_ReturnsCsvWithErrorColumn()
    {
        var response = await Client.GetAsync("/api/imports/template?type=PACKAGES");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var csv = await response.Content.ReadAsStringAsync();
        Assert.Contains("productCode,packageName,barcode,uomCode,conversionFactor,isActive,errorMessage", csv);
    }

    [Fact]
    public async Task PreviewAndCommit_Packages_UpsertsSuccessfully()
    {
        string prodCode = $"PPROD_{Guid.NewGuid():N}"[..15].ToUpperInvariant();
        string uomCode = $"PUOM_{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        string barcode = $"BAR_{Guid.NewGuid():N}"[..15].ToUpperInvariant();

        using (var scope = Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MasterDataDbContext>();
            var uom = new Uom
            {
                Id = Guid.NewGuid(),
                TenantId = db.CurrentTenantId,
                Code = uomCode,
                Name = "Package Unit",
                IsActive = true
            };
            var product = new Product
            {
                Id = Guid.NewGuid(),
                TenantId = db.CurrentTenantId,
                Code = prodCode,
                Name = "Package Test Product",
                BaseUomId = uom.Id,
                IsActive = true
            };
            db.Uoms.Add(uom);
            db.Products.Add(product);
            await db.SaveChangesAsync();
        }

        var csv = $"productCode,packageName,barcode,uomCode,conversionFactor,isActive\n{prodCode},Box 10,{barcode},{uomCode},10,true";
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        content.Add(fileContent, "file", "packages.csv");

        var previewRes = await Client.PostAsync("/api/imports/preview?type=PACKAGES", content);
        Assert.Equal(HttpStatusCode.OK, previewRes.StatusCode);

        var previewJson = JsonDocument.Parse(await previewRes.Content.ReadAsStringAsync());
        var batchId = previewJson.RootElement.GetProperty("batchId").GetGuid();
        Assert.Equal(0, previewJson.RootElement.GetProperty("errorRows").GetInt32());

        // Commit batch
        var commitRes = await Client.PostAsync("/api/imports/commit",
            new StringContent(JsonSerializer.Serialize(new { batchId }), Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, commitRes.StatusCode);

        // Verify Package in DB
        using (var scope = Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MasterDataDbContext>();
            var pkg = await db.Packages.Include(p => p.Product).Include(p => p.Uom)
                .FirstOrDefaultAsync(p => p.Product!.Code == prodCode && p.Uom!.Code == uomCode);

            Assert.NotNull(pkg);
            Assert.Equal("Box 10", pkg.PackageName);
            Assert.Equal(barcode, pkg.Barcode);
            Assert.Equal(10m, pkg.ConversionFactor);
        }

        // Clean Export Test
        var exportRes = await Client.GetAsync("/api/exports?type=PACKAGES&format=csv");
        Assert.Equal(HttpStatusCode.OK, exportRes.StatusCode);
        var exportCsv = await exportRes.Content.ReadAsStringAsync();
        Assert.Contains(prodCode, exportCsv);
        Assert.Contains(barcode, exportCsv);
    }
}
