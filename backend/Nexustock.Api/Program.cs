using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Nexustock.Api.Health;
using Nexustock.Api.Infrastructure;
using Nexustock.Modules.Identity;
using Nexustock.Modules.MasterData;
using Serilog;
using System.Text.Json;

try { EnvLoader.LoadDotEnvFromNearestParent(); } catch { /* Bỏ qua lỗi .env khi test */ }

try
{
    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Debug()
        .WriteTo.Console()
        .WriteTo.File("logs/nexustock-.log", rollingInterval: RollingInterval.Day)
        .CreateLogger();

    Log.Information("Nexustock API starting...");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddControllers();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
        {
            Title = "Nexustock API",
            Version = "v1",
            Description = "Nexustock Warehouse Management System API",
            Contact = new Microsoft.OpenApi.Models.OpenApiContact
            {
                Name = "Nexustock Team",
            }
        });

        // JWT Bearer Token Security Definition
        c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Description = "Enter JWT Bearer token (without 'Bearer ' prefix)"
        });

        c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference = new Microsoft.OpenApi.Models.OpenApiReference
                    {
                        Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });

        // Include XML comments từ assembly
        var apiXmlPath = Path.Combine(AppContext.BaseDirectory, "Nexustock.Api.xml");
        if (File.Exists(apiXmlPath)) c.IncludeXmlComments(apiXmlPath);

        var masterDataXmlPath = Path.Combine(AppContext.BaseDirectory, "Nexustock.Modules.MasterData.xml");
        if (File.Exists(masterDataXmlPath)) c.IncludeXmlComments(masterDataXmlPath);
    });
    builder.Services.AddMasterDataModule(builder.Configuration);
    builder.Services.AddIdentityModule(builder.Configuration);

    // JWT Authentication
    var jwtSecretKey = builder.Configuration["JWT_SECRET_KEY"] ?? throw new InvalidOperationException("JWT_SECRET_KEY is not configured");
    var jwtIssuer = builder.Configuration["JWT_ISSUER"] ?? "Nexustock";
    var jwtAudience = builder.Configuration["JWT_AUDIENCE"] ?? "Nexustock";

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = "Bearer";
        options.DefaultChallengeScheme = "Bearer";
    })
    .AddJwtBearer("Bearer", options =>
    {
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes(jwtSecretKey)),
            ClockSkew = TimeSpan.Zero
        };
    });

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
    app.UseAuthentication();  // Phải đặt trước UseAuthorization
    app.UseAuthorization();
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
    app.MapControllers();

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

    // Run Database Seeding
    try
    {
        var appPermissions = Nexustock.Modules.MasterData.Permissions.AppPermissions.All
            .Select(p => (p.Code, p.Name, p.Group));
        await Nexustock.Modules.Identity.Seeders.IdentitySeeder.SeedAsync(app.Services, appPermissions);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "An error occurred while seeding the database");
    }

    app.Run();
}
catch (Exception ex) when (ex.GetType().Name != "HostAbortedException")
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

// Partial class để WebApplicationFactory có thể tham chiếu trong integration test
// Top-level statement Program class là internal, nên partial cũng phải internal
internal partial class Program { }

