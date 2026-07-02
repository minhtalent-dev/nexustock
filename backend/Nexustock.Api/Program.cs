using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Nexustock.Api.Health;
using Nexustock.Api.Infrastructure;
using Serilog;
using System.Text.Json;

EnvLoader.LoadDotEnvFromNearestParent();

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .WriteTo.File("logs/nexustock-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    Log.Information("Nexustock API starting...");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowFrontendDev", policy =>
        {
            policy.WithOrigins("http://localhost:3000", "http://localhost:3003")
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
    });

    var connectionString = builder.Configuration.GetConnectionString("Default");
    var enableRedis = builder.Configuration.GetValue<bool>("ENABLE_REDIS");
    var redisConnection = builder.Configuration.GetValue<string>("REDIS_CONNECTION") ?? "localhost:6379";

    var healthChecks = builder.Services.AddHealthChecks();

    if (!string.IsNullOrWhiteSpace(connectionString))
    {
        healthChecks.AddCheck("database", new PostgreSqlHealthCheck(connectionString));
    }

    if (enableRedis)
    {
        healthChecks.AddCheck("redis", new RedisHealthCheck(redisConnection));
    }

    var app = builder.Build();

    app.UseCors("AllowFrontendDev");
    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = _ => false,
        AllowCachingResponses = false,
    });

    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        AllowCachingResponses = false,
    });

    app.MapGet("/api/system/health-summary", async (IConfiguration config, HealthCheckService healthCheckService) =>
    {
        var report = await healthCheckService.CheckHealthAsync();
        var databaseStatus = report.Entries.TryGetValue("database", out var database)
            ? ToServiceStatus(database.Status)
            : "pending";

        var redisStatus = enableRedis
            ? report.Entries.TryGetValue("redis", out var redis)
                ? ToServiceStatus(redis.Status)
                : "unhealthy"
            : "disabled";

        var summary = new
        {
            status = ToServiceStatus(report.Status),
            version = "0.1.0",
            environment = config.GetValue<string>("ASPNETCORE_ENVIRONMENT") ?? app.Environment.EnvironmentName,
            services = new
            {
                api = "healthy",
                database = databaseStatus,
                redis = redisStatus
            },
            traceId = System.Diagnostics.Activity.Current?.Id ?? Guid.NewGuid().ToString("D")
        };

        return Results.Json(summary, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    });

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

static string ToServiceStatus(HealthStatus status) => status switch
{
    HealthStatus.Healthy => "healthy",
    HealthStatus.Degraded => "degraded",
    _ => "unhealthy"
};
