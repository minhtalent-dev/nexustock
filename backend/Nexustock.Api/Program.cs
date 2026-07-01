using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// ──────────────────────────────────────────────────────────────
//  Health checks - luôn bật liveness, readiness kiểm tra Redis
// ──────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

// TODO: Bật PostgreSQL health khi có DB context sẵn sàng
// builder.Services.AddHealthChecks().AddNpgSql(...)

var enableRedis = builder.Configuration.GetValue<bool>("ENABLE_REDIS");
if (enableRedis)
{
    // TODO: Thêm Redis health check khi cài StackExchange.Redis
    // builder.Services.AddHealthChecks().AddRedis(...)
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ─────────────────── health: live & ready ───────────────────
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,           // chỉ ping server sống
    AllowCachingResponses = false,
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    AllowCachingResponses = false,
});

// ──────────── health-summary ---------- (dùng cho /health-ui) ────────────
app.MapGet("/api/system/health-summary", (IConfiguration config) =>
{
    var summary = new
    {
        status = "healthy",
        version = "0.1.0",
        environment = config.GetValue<string>("ASPNETCORE_ENVIRONMENT") ?? "Development",
        services = new
        {
            api = "healthy",
            database = "pending",               // sẽ thay khi có DB ping
            redis = enableRedis ? "enabled" : "disabled"
        },
        traceId = System.Diagnostics.Activity.Current?.Id ?? Guid.NewGuid().ToString("D")
    };
    return Results.Json(summary, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
});

app.Run();
