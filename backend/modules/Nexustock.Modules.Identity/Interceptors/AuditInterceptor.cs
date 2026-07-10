using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Nexustock.Modules.Identity.Entities;

namespace Nexustock.Modules.Identity.Interceptors;

public class AuditInterceptor : SaveChangesInterceptor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditInterceptor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context == null) return base.SavingChanges(eventData, result);

        var auditEntries = OnBeforeSaveChanges(eventData.Context);
        if (auditEntries.Count > 0)
        {
            SaveAuditLogs(eventData.Context, auditEntries);
        }

        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context == null) return await base.SavingChangesAsync(eventData, result, cancellationToken);

        var auditEntries = OnBeforeSaveChanges(eventData.Context);
        if (auditEntries.Count > 0)
        {
            await SaveAuditLogsAsync(eventData.Context, auditEntries, cancellationToken);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private List<AuditEntry> OnBeforeSaveChanges(DbContext context)
    {
        context.ChangeTracker.DetectChanges();
        var auditEntries = new List<AuditEntry>();

        var httpContext = _httpContextAccessor.HttpContext;
        Guid? userId = null;
        Guid? tenantId = null;

        if (httpContext?.User != null)
        {
            var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(userIdClaim, out var parsedUserId)) userId = parsedUserId;

            var tenantIdClaim = httpContext.User.FindFirst("tenantId")?.Value;
            if (Guid.TryParse(tenantIdClaim, out var parsedTenantId)) tenantId = parsedTenantId;
        }

        var traceId = httpContext?.TraceIdentifier;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.Entity is AuditLog || entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
                continue;

            var auditEntry = new AuditEntry
            {
                UserId = userId,
                TenantId = tenantId,
                TraceId = traceId,
                EntityName = entry.Metadata.DisplayName() ?? entry.Entity.GetType().Name,
                Action = entry.State switch
                {
                    EntityState.Added => "Insert",
                    EntityState.Modified => "Update",
                    EntityState.Deleted => "Delete",
                    _ => entry.State.ToString()
                }
            };

            auditEntries.Add(auditEntry);

            // Save key properties
            foreach (var property in entry.Properties)
            {
                if (property.Metadata.IsPrimaryKey())
                {
                    auditEntry.EntityId = property.CurrentValue?.ToString() ?? string.Empty;
                }

                switch (entry.State)
                {
                    case EntityState.Added:
                        auditEntry.NewValues[property.Metadata.Name] = property.CurrentValue;
                        break;

                    case EntityState.Deleted:
                        auditEntry.OldValues[property.Metadata.Name] = property.OriginalValue;
                        break;

                    case EntityState.Modified:
                        if (property.IsModified)
                        {
                            auditEntry.ChangedColumns.Add(property.Metadata.Name);
                            auditEntry.OldValues[property.Metadata.Name] = property.OriginalValue;
                            auditEntry.NewValues[property.Metadata.Name] = property.CurrentValue;
                        }
                        break;
                }
            }
        }

        return auditEntries;
    }

    private void SaveAuditLogs(DbContext context, List<AuditEntry> auditEntries)
    {
        foreach (var entry in auditEntries)
        {
            var oldValuesJson = entry.OldValues.Count > 0 ? JsonSerializer.Serialize(entry.OldValues) : null;
            var newValuesJson = entry.NewValues.Count > 0 ? JsonSerializer.Serialize(entry.NewValues) : null;
            var changedColumnsJson = entry.ChangedColumns.Count > 0 ? JsonSerializer.Serialize(entry.ChangedColumns) : null;

            const string sql = @"
                INSERT INTO ""AuditLogs"" (""Id"", ""UserId"", ""TenantId"", ""EntityName"", ""EntityId"", ""Action"", ""OldValues"", ""NewValues"", ""ChangedColumns"", ""TraceId"", ""Timestamp"")
                VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10)";

            context.Database.ExecuteSqlRaw(sql,
                Guid.NewGuid(),
                entry.UserId,
                entry.TenantId,
                entry.EntityName,
                entry.EntityId,
                entry.Action,
                oldValuesJson,
                newValuesJson,
                changedColumnsJson,
                entry.TraceId,
                DateTime.UtcNow);
        }
    }

    private async Task SaveAuditLogsAsync(DbContext context, List<AuditEntry> auditEntries, CancellationToken cancellationToken)
    {
        foreach (var entry in auditEntries)
        {
            var oldValuesJson = entry.OldValues.Count > 0 ? JsonSerializer.Serialize(entry.OldValues) : null;
            var newValuesJson = entry.NewValues.Count > 0 ? JsonSerializer.Serialize(entry.NewValues) : null;
            var changedColumnsJson = entry.ChangedColumns.Count > 0 ? JsonSerializer.Serialize(entry.ChangedColumns) : null;

            const string sql = @"
                INSERT INTO ""AuditLogs"" (""Id"", ""UserId"", ""TenantId"", ""EntityName"", ""EntityId"", ""Action"", ""OldValues"", ""NewValues"", ""ChangedColumns"", ""TraceId"", ""Timestamp"")
                VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10})";

            await context.Database.ExecuteSqlRawAsync(sql, new object[] {
                Guid.NewGuid(),
                entry.UserId,
                entry.TenantId,
                entry.EntityName,
                entry.EntityId,
                entry.Action,
                oldValuesJson,
                newValuesJson,
                changedColumnsJson,
                entry.TraceId,
                DateTime.UtcNow
            }, cancellationToken);
        }
    }
}

internal class AuditEntry
{
    public Guid? UserId { get; set; }
    public Guid? TenantId { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public Dictionary<string, object?> OldValues { get; } = new();
    public Dictionary<string, object?> NewValues { get; } = new();
    public List<string> ChangedColumns { get; } = new();
    public string? TraceId { get; set; }
}
