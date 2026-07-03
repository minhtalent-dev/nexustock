using System.Net;
using System.Text;
using System.Text.Json;

namespace Nexustock.MasterData.IntegrationTests;

/// <summary>
/// Integration tests cho ImportsController: template CSV, preview, commit, export errors.
/// </summary>
public class ImportsControllerTests : IntegrationTestBase
{
    [Fact]
    public async Task GetTemplate_Items_ReturnsCsvWithErrorColumn()
    {
        var response = await Client.GetAsync("/api/imports/template?type=ITEMS");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);

        var csv = await response.Content.ReadAsStringAsync();
        Assert.Contains("errorMessage", csv);
        Assert.Contains("SP001", csv);
    }

    [Fact]
    public async Task GetTemplate_Locations_ReturnsCsvWithErrorColumn()
    {
        var response = await Client.GetAsync("/api/imports/template?type=LOCATIONS");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);

        var csv = await response.Content.ReadAsStringAsync();
        Assert.Contains("errorMessage", csv);
        Assert.Contains("warehouseCode", csv);
    }

    [Fact]
    public async Task GetTemplate_InvalidType_ReturnsBadRequest()
    {
        var response = await Client.GetAsync("/api/imports/template?type=INVALID");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Preview_Items_ReturnsValidatedBatchWithErrors()
    {
        var csv = "code,name,baseUomCode,trackingPolicy,shelfLifeDays,minStock\nSP001,Test,PCS,NONE,0,10";
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        content.Add(fileContent, "file", "test.csv");

        var response = await Client.PostAsync("/api/imports/preview?type=ITEMS", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body);
        Assert.Equal("ITEMS", json.RootElement.GetProperty("importType").GetString());
        Assert.Equal("VALIDATED", json.RootElement.GetProperty("status").GetString());
        Assert.True(json.RootElement.GetProperty("totalRows").GetInt32() > 0);
    }

    [Fact]
    public async Task ExportErrors_Items_ReturnsOnlyInvalidRows()
    {
        // SP001 xuất hiện 2 lần → dòng thứ hai sẽ bị lỗi trùng mã trong file
        var csv = "code,name,baseUomCode,trackingPolicy,shelfLifeDays,minStock\nSP001,Test,PCS,NONE,0,10\nSP001,Duplicate,PCS,NONE,0,10\nNEW_EXPORT_OK,Valid,PCS,NONE,0,10";
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        content.Add(fileContent, "file", "errors.csv");

        var previewResponse = await Client.PostAsync("/api/imports/preview?type=ITEMS", content);
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);

        var previewBody = await previewResponse.Content.ReadAsStringAsync();
        var previewJson = JsonDocument.Parse(previewBody);
        var batchId = previewJson.RootElement.GetProperty("batchId").GetGuid();

        var exportResponse = await Client.GetAsync($"/api/imports/errors/{batchId}");
        Assert.Equal(HttpStatusCode.OK, exportResponse.StatusCode);

        var exportedCsv = await exportResponse.Content.ReadAsStringAsync();
        Assert.Contains("SP001", exportedCsv);
        Assert.DoesNotContain("NEW_EXPORT_OK", exportedCsv);
    }

    [Fact]
    public async Task Preview_EmptyFile_ReturnsBadRequest()
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Array.Empty<byte>());
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        content.Add(fileContent, "file", "empty.csv");

        var response = await Client.PostAsync("/api/imports/preview?type=ITEMS", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
