using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Nexustock.Api.Health;
using Nexustock.Api.Infrastructure;
using Nexustock.Modules.Readiness.Middleware;
using Nexustock.Modules.Replenishment.Services;
using Hangfire;
using Hangfire.PostgreSql;
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
            Contact = new Microsoft.OpenApi.Models.OpenApiContact { Name = "Nexustock Team" }
        });

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

        var apiXmlPath = Path.Combine(AppContext.BaseDirectory, "Nexustock.Api.xml");
        if (File.Exists(apiXmlPath)) c.IncludeXmlComments(apiXmlPath);

        var masterDataXmlPath = Path.Combine(AppContext.BaseDirectory, "Nexustock.Modules.MasterData.xml");
        if (File.Exists(masterDataXmlPath)) c.IncludeXmlComments(masterDataXmlPath);
    });

    builder.Services.AddNexustockModules(builder.Configuration);

    var defaultConn = builder.Configuration.GetConnectionString("Default");
    if (!string.IsNullOrEmpty(defaultConn))
    {
        builder.Services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(defaultConn)));
        builder.Services.AddHangfireServer();
    }

    var jwtSecretKey = builder.Configuration["JWT_SECRET_KEY"]
        ?? throw new InvalidOperationException("JWT_SECRET_KEY is not configured");
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
        healthChecks.AddCheck("database", new PostgreSqlHealthCheck(connectionString));
    if (enableRedis)
        healthChecks.AddCheck("redis", new RedisHealthCheck(redisConnection));

    var migrateOnly = args.Contains("--migrate-only")
        || string.Equals(builder.Configuration["NEXUSTOCK_MIGRATE_ONLY"], "true", StringComparison.OrdinalIgnoreCase);

    var app = builder.Build();

    if (migrateOnly)
    {
        Log.Information("Running database migrations in one-shot mode...");
        var ok = await DatabaseMigrationRunner.MigrateAllAsync(app.Services, DatabaseMigrationRunner.Mode.FailFast);
        if (!ok)
        {
            Log.Fatal("One or more database migrations failed. Exiting with error code.");
            Environment.Exit(1);
        }

        Log.Information("All database migrations completed successfully. Exiting.");
        Environment.Exit(0);
    }

    var uploadPath = app.Configuration["UploadSettings:UploadPath"] ?? "D:\\NexustockUploads";
    var requestPath = app.Configuration["UploadSettings:RequestPath"] ?? "/uploads";
    if (!Directory.Exists(uploadPath))
        Directory.CreateDirectory(uploadPath);

    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploadPath),
        RequestPath = requestPath
    });

    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseCors("AllowFrontendDev");
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseMiddleware<CutoverFreezeMiddleware>();
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
    app.MapHealthChecks("/health/ready", new HealthCheckOptions { AllowCachingResponses = false });
    app.MapControllers();

    if (!string.IsNullOrEmpty(builder.Configuration.GetConnectionString("Default")))
    {
        app.UseHangfireDashboard("/admin/hangfire", new DashboardOptions
        {
            Authorization = Array.Empty<Hangfire.Dashboard.IDashboardAuthorizationFilter>()
        });

        RecurringJob.AddOrUpdate<IReplenishmentService>(
            "replenishment-auto-scan",
            service => service.GenerateTasksAsync(Guid.Parse("00000000-0000-0000-0000-000000000001"), "FEFO"),
            "*/15 * * * *");
    }

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

    if (app.Environment.IsDevelopment())
        await DatabaseMigrationRunner.MigrateAllAsync(app.Services, DatabaseMigrationRunner.Mode.Soft);

    await DatabaseSeeder.SeedAsync(app.Services);

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

internal partial class Program { }
