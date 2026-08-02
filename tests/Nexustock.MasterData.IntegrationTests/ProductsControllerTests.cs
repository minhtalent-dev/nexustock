using System.Net;
using System.Net.Http.Json;
using Nexustock.Modules.MasterData.DTOs;

namespace Nexustock.MasterData.IntegrationTests;

public class ProductsControllerTests : IntegrationTestBase
{
    public ProductsControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task GetProducts_ReturnsPagedResult()
    {
        var response = await Client.GetAsync("/api/master-data/products?search=&page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsync<PagedResult<ProductDto>>();
        Assert.NotNull(content);
        Assert.NotNull(content.Items);
    }

    [Fact]
    public async Task CreateProduct_ReturnsCreatedProduct()
    {
        // Tạo UOM cần thiết trước
        var uomRequest = new { code = "PCS-INTTEST", name = "Piece for Integration Test", isActive = true };
        var uomResponse = await Client.PostAsJsonAsync("/api/master-data/uoms", uomRequest);
        Assert.True(uomResponse.IsSuccessStatusCode, $"UOM creation failed: {uomResponse.StatusCode}");
        var uom = await uomResponse.Content.ReadFromJsonAsync<UomDto>();
        Assert.NotNull(uom);

        // Tạo product dùng UOM vừa tạo
        var request = new
        {
            code = "TEST-001",
            name = "Test Product",
            description = "Integration test create product",
            barcode = "BAR-TEST-001",
            baseUomId = uom.Id,
            isActive = true,
            config = new
            {
                iqcCheckType = "FULL",
                vendorInnerLotCtl = false,
                isWafer = false,
                lotValidationRegex = (string?)null,
                minStock = 0m,
                maxStock = 9999m,
                weightClass = "MEDIUM",
                rotationSpeed = "SLOW",
                trackSerial = false,
                length = 0m,
                width = 0m,
                height = 0m,
                weight = 0m
            },
            packages = Array.Empty<object>()
        };

        var response = await Client.PostAsJsonAsync("/api/master-data/products", request);
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"Expected success but got {response.StatusCode}. Body: {responseBody}");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task GetProduct_NotFound_ForMissingId()
    {
        var missingId = Guid.NewGuid();
        var response = await Client.GetAsync($"/api/master-data/products/{missingId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
