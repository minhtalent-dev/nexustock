using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Nexustock.Modules.Inbound.Contexts;
using Nexustock.Modules.Inbound.Entities;
using Nexustock.Modules.Wave.Contexts;
using Nexustock.Modules.Wave.Entities;
using Xunit;

namespace Nexustock.MasterData.IntegrationTests;

public class OpsExportsControllerTests : IntegrationTestBase
{
    private static readonly IReadOnlyDictionary<string, string> ExpectedHeaders = new Dictionary<string, string>
    {
        ["INBOUND_ORDERS"] = "orderNo,status,partnerId,createdAt,createdBy",
        ["SHIPMENTS"] = "shipmentNo,status,partnerId,createdAt,createdBy",
        ["STOCKTAKES"] = "stocktakeNo,status,totalVarianceAmount,createdAt,createdBy",
        ["RMA"] = "rmaNo,status,customerId,referenceNo,createdAt,createdBy",
        ["LOTS"] = "lotNo,itemId,qcStatus,expiryDate,productionDate",
        ["EXCEPTIONS"] = "code,type,severity,status,referenceType,referenceId,locationId,lotNo,qty,reasonCode,note,createdAt",
        ["LPNS"] = "lpnNo,locationId,status,createdAt,createdBy,updatedAt,updatedBy",
        ["INVENTORY_BALANCES"] = "itemId,lotNo,locationId,qtyOnHand,qtyReserved,qtyAvailable,lpnId,createdAt,updatedAt",
        ["WAVES"] = "waveNo,status,createdAt,createdBy,updatedAt",
        ["PUTAWAY_PROPOSALS"] = "warehouseId,lotId,itemId,qty,candidateLocationId,score,reason,status,createdAt",
        ["CROSS_DOCK_CANDIDATES"] = "lotId,inboundOrderItemId,waveItemId,itemId,qtyAvailable,qtyRequested,qtyMatched,matchScore,status,expiresAt,createdAt",
        ["REPLENISHMENT_TASKS"] = "itemId,sourceLocationId,targetLocationId,lotNo,requestedQty,actualQty,status,mobileTaskId,createdAt"
    };

    public OpsExportsControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
        PermissionService.AllowedPermissions.Clear();
        PermissionService.AllowedPermissions.Add("ops.export");
    }

    [Fact]
    public async Task Export_WithoutPermission_ReturnsForbidden()
    {
        PermissionService.AllowedPermissions.Clear();
        var response = await Client.GetAsync("/api/ops/exports?type=INBOUND_ORDERS");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("INVALID_TYPE", "csv")]
    [InlineData("INBOUND_ORDERS", "pdf")]
    public async Task Export_InvalidInput_ReturnsBadRequest(string type, string format)
    {
        var response = await Client.GetAsync($"/api/ops/exports?type={type}&format={format}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(AllTypes))]
    public async Task Export_AllTypes_Csv_ReturnsContractHeaderAndTimestampedFilename(string type)
    {
        var response = await Client.GetAsync($"/api/ops/exports?type={type}&format=csv");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("false", response.Headers.GetValues("X-Export-Truncated").Single());
        Assert.Matches(@"^[a-z_]+_\d{14}\.csv$", response.Content.Headers.ContentDisposition?.FileName?.Trim('"') ?? "");

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Take(3).SequenceEqual(Encoding.UTF8.GetPreamble()));
        var firstLine = Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF').Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)[0];
        Assert.Equal(ExpectedHeaders[type], firstLine);
    }

    [Theory]
    [MemberData(nameof(AllTypes))]
    public async Task Export_AllTypes_Xlsx_ReturnsContractHeaderAndTimestampedFilename(string type)
    {
        var response = await Client.GetAsync($"/api/ops/exports?type={type}&format=xlsx");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("false", response.Headers.GetValues("X-Export-Truncated").Single());
        Assert.Matches(@"^[a-z_]+_\d{14}\.xlsx$", response.Content.Headers.ContentDisposition?.FileName?.Trim('"') ?? "");
    }

    [Fact]
    public async Task Export_InboundOrders_OverCap_ReturnsExactly5000RowsAndTruncatedHeader()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InboundDbContext>();
        var tenantId = Guid.Parse(TestAuthConstants.TenantId);
        var marker = $"CAP-{Guid.NewGuid():N}";
        db.InboundOrders.AddRange(Enumerable.Range(0, 5001).Select(i => new InboundOrder
        {
            Id = Guid.NewGuid(), TenantId = tenantId, OrderNo = $"{marker}-{i:D4}",
            PartnerId = Guid.NewGuid(), Status = InboundOrderStatus.Open,
            CreatedAt = DateTime.UtcNow, CreatedBy = "test"
        }));
        await db.SaveChangesAsync();

        var response = await Client.GetAsync("/api/ops/exports?type=INBOUND_ORDERS&format=csv");
        var csv = (await response.Content.ReadAsStringAsync()).TrimStart('\uFEFF');
        var lines = csv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("true", response.Headers.GetValues("X-Export-Truncated").Single());
        Assert.Equal(5001, lines.Length);
    }

    [Fact]
    public async Task Export_Waves_DoesNotLeakOtherTenant()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WaveDbContext>();
        var ownMarker = $"OWN-{Guid.NewGuid():N}";
        var otherMarker = $"OTHER-{Guid.NewGuid():N}";
        db.PickingWaves.AddRange(
            NewWave(Guid.Parse(TestAuthConstants.TenantId), ownMarker),
            NewWave(Guid.NewGuid(), otherMarker));
        await db.SaveChangesAsync();

        var response = await Client.GetAsync("/api/ops/exports?type=WAVES&format=csv");
        var csv = await response.Content.ReadAsStringAsync();

        Assert.Contains(ownMarker, csv);
        Assert.DoesNotContain(otherMarker, csv);
    }

    private static PickingWave NewWave(Guid tenantId, string waveNo) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, WaveNo = waveNo, Status = "DRAFT",
        CreatedAt = DateTime.UtcNow, CreatedBy = "test"
    };

    public static IEnumerable<object[]> AllTypes() => ExpectedHeaders.Keys.Select(type => new object[] { type });
}
