using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nexustock.Modules.MasterData.Contexts;
using Nexustock.Modules.MasterData.Services;
using Xunit;

namespace Nexustock.MasterData.IntegrationTests;

public class ImportBatchLifecycleTests : IntegrationTestBase
{
    public ImportBatchLifecycleTests(CustomWebApplicationFactory factory) : base(factory)
    {
        PermissionService.AllowedPermissions.Add("master_data.import");
        PermissionService.AllowedPermissions.Add("master_data.export");
    }

    [Fact]
    public async Task Preview_Exceeding5000Rows_ReturnsImportTooLarge()
    {
        var sb = new StringBuilder();
        sb.AppendLine("code,name,baseUomCode");
        for (int i = 1; i <= 5001; i++)
        {
            sb.AppendLine($"P{i:00000},Product {i},PCS");
        }

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(sb.ToString()));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        content.Add(fileContent, "file", "large.csv");

        var response = await Client.PostAsync("/api/imports/preview?type=ITEMS", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("IMPORT_TOO_LARGE", body);
    }

    [Fact]
    public async Task Recommit_AlreadyCommittedBatch_ReturnsConflict()
    {
        Guid batchId;
        using (var scope = Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MasterDataDbContext>();
            batchId = Guid.NewGuid();
            var batch = new Nexustock.Modules.MasterData.Entities.ImportBatch
            {
                Id = batchId,
                TenantId = db.CurrentTenantId,
                ImportType = "PACKAGES",
                Status = "COMMITTED",
                TotalRows = 1,
                SuccessRows = 1,
                ErrorRows = 0,
                CreatedBy = "Test User",
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
            };
            db.ImportBatches.Add(batch);
            await db.SaveChangesAsync();
        }

        var response = await Client.PostAsync("/api/imports/commit",
            new StringContent(JsonSerializer.Serialize(new { batchId }), Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body);
        Assert.False(json.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("IMPORT_BATCH_ALREADY_COMMITTED", json.RootElement.GetProperty("errorCsvContent").GetString());
    }

    [Fact]
    public async Task ClaimBatch_EnforcesOwnerTtlTargetAndErrors()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MasterDataDbContext>();
        var coordinator = scope.ServiceProvider.GetRequiredService<IImportBatchCoordinator>();
        var tenantId = db.CurrentTenantId;
        var targetId = Guid.NewGuid();

        var ownerBatch = await coordinator.CreateBatchAsync(tenantId, "INBOUND_LINES", targetId, "owner.csv", "hash-owner", "owner", 1, 1, 0, default);
        var expiredBatch = await coordinator.CreateBatchAsync(tenantId, "INBOUND_LINES", targetId, "expired.csv", "hash-expired", "owner", 1, 1, 0, default);
        expiredBatch.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        var errorBatch = await coordinator.CreateBatchAsync(tenantId, "INBOUND_LINES", targetId, "errors.csv", "hash-errors", "owner", 1, 0, 1, default);
        await db.SaveChangesAsync();

        var ownerResult = await coordinator.ClaimBatchForCommitAsync(ownerBatch.Id, tenantId, "INBOUND_LINES", targetId, "other", default);
        var targetResult = await coordinator.ClaimBatchForCommitAsync(ownerBatch.Id, tenantId, "INBOUND_LINES", Guid.NewGuid(), "owner", default);
        var expiredResult = await coordinator.ClaimBatchForCommitAsync(expiredBatch.Id, tenantId, "INBOUND_LINES", targetId, "owner", default);
        var errorResult = await coordinator.ClaimBatchForCommitAsync(errorBatch.Id, tenantId, "INBOUND_LINES", targetId, "owner", default);

        Assert.Equal(BatchClaimStatus.OwnerMismatch, ownerResult.Status);
        Assert.Equal("IMPORT_BATCH_NOT_FOUND", ownerResult.ErrorMessage);
        Assert.Equal(BatchClaimStatus.TargetMismatch, targetResult.Status);
        Assert.Equal("IMPORT_TARGET_MISMATCH", targetResult.ErrorMessage);
        Assert.Equal(BatchClaimStatus.Expired, expiredResult.Status);
        Assert.Equal("IMPORT_BATCH_EXPIRED", expiredResult.ErrorMessage);
        Assert.Equal(BatchClaimStatus.HasErrors, errorResult.Status);
        Assert.Equal("IMPORT_BATCH_HAS_ERRORS", errorResult.ErrorMessage);
    }
}
