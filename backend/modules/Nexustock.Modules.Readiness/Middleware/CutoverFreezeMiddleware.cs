using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nexustock.Modules.Inventory.Services;
using Nexustock.Modules.Readiness.Contexts;

namespace Nexustock.Modules.Readiness.Middleware;

public sealed class CutoverFreezeMiddleware
{
    private static readonly string[] AllowPrefixes =
    {
        "/health",
        "/api/auth",
        "/api/admin/readiness",
        "/api/admin/cutover",
        "/api/observability",
        "/api/feature-flags",
        "/api/users",
        "/api/roles",
        "/api/permissions",
        "/api/audit-logs",
        "/swagger"
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<CutoverFreezeMiddleware> _logger;

    public CutoverFreezeMiddleware(RequestDelegate next, ILogger<CutoverFreezeMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var method = context.Request.Method;
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method))
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path.Value ?? string.Empty;
        foreach (var prefix in AllowPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }
        }

        Guid tenantId;
        try
        {
            var tenantProvider = context.RequestServices.GetService<ITenantProvider>();
            if (tenantProvider is null || tenantProvider.TenantId == Guid.Empty)
            {
                await _next(context);
                return;
            }
            tenantId = tenantProvider.TenantId;
        }
        catch
        {
            await _next(context);
            return;
        }

        bool? frozen = null;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                using var scope = context.RequestServices.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ReadinessDbContext>();
                var state = await db.CutoverFreezeStates.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.TenantId == tenantId, context.RequestAborted);
                frozen = state?.IsFrozen == true;
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Event={Event} TenantId={TenantId} Attempt={Attempt} TraceId={TraceId}",
                    "readiness.freeze.probe_failed", tenantId, attempt + 1, context.TraceIdentifier);
                if (attempt == 1)
                {
                    // Fail-closed khi không đọc được freeze state
                    frozen = true;
                }
            }
        }

        if (frozen == true)
        {
            context.Response.StatusCode = StatusCodes.Status423Locked;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                errorCode = "CUTOVER_FROZEN",
                message = "Warehouse write APIs are frozen during cutover.",
                traceId = context.TraceIdentifier
            });
            return;
        }

        await _next(context);
    }
}
