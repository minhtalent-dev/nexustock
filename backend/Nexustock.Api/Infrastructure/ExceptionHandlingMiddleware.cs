using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nexustock.Modules.Exceptions.Contexts;
using Nexustock.Modules.Exceptions.Entities;

namespace Nexustock.Api.Infrastructure;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationalBusinessException ex)
        {
            _logger.LogWarning(ex, "Bay loi nghiep vu van hanh WMS: {Message}", ex.Message);
            await HandleOperationalBusinessExceptionAsync(context, ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Loi he thong chua duoc bay: {Message}", ex.Message);
            await HandleGenericExceptionAsync(context, ex);
        }
    }

    private async Task HandleOperationalBusinessExceptionAsync(HttpContext context, OperationalBusinessException ex)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = StatusCodes.Status400BadRequest;

        var username = context.User?.Identity?.Name ?? "System";
        string exceptionCode = "EX-GENERIC";

        using (var scope = context.RequestServices.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ExceptionsDbContext>();
            var tenantId = dbContext.CurrentTenantId;

            var dateStr = DateTime.UtcNow.ToString("yyMMdd");
            var countToday = dbContext.OperationalExceptions
                .IgnoreQueryFilters()
                .Count(e => e.TenantId == tenantId && e.Code.StartsWith($"EX-{dateStr}-"));
            exceptionCode = $"EX-{dateStr}-{(countToday + 1):D4}";

            var opException = new OperationalException
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = exceptionCode,
                Type = ex.ErrorCode,
                Severity = ex.Severity,
                Status = "Open",
                ReferenceType = ex.ReferenceType,
                ReferenceId = ex.ReferenceId,
                LocationId = ex.LocationId,
                LotNo = ex.LotNo,
                Qty = ex.Qty,
                ReasonCode = ex.ErrorCode,
                Note = ex.Message,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = username
            };
            dbContext.OperationalExceptions.Add(opException);

            var @event = new ExceptionEvent
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ExceptionId = opException.Id,
                Transition = "CREATE_AUTO",
                Actor = username,
                Note = "Tu dong ghi nhan tu Middleware",
                CreatedAt = DateTime.UtcNow
            };
            dbContext.ExceptionEvents.Add(@event);

            await dbContext.SaveChangesAsync();
        }

        var responseObj = new
        {
            errorCode = ex.ErrorCode,
            code = exceptionCode,
            message = ex.Message
        };

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        await context.Response.WriteAsync(JsonSerializer.Serialize(responseObj, options));
    }

    private async Task HandleGenericExceptionAsync(HttpContext context, Exception ex)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;

        var responseObj = new
        {
            errorCode = "SYSTEM_ERROR",
            message = "Da xay ra loi he thong nghiem trong, vui long lien he admin."
        };

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        await context.Response.WriteAsync(JsonSerializer.Serialize(responseObj, options));
    }
}
