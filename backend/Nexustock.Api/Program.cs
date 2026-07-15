using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Nexustock.Api.Health;
using Nexustock.Api.Infrastructure;
using Nexustock.Modules.Identity;
using Nexustock.Modules.MasterData;
using Nexustock.Modules.Inbound;
using Nexustock.Modules.Qc;
using Nexustock.Modules.Inventory;
using Nexustock.Modules.Exceptions;
using Nexustock.Modules.Rules;
using Nexustock.Modules.Putaway;
using Nexustock.Modules.Allocation;
using Nexustock.Modules.Replenishment;
using Nexustock.Modules.Replenishment.Services;
using Nexustock.Modules.Lpn;
using Nexustock.Modules.Serial;
using Nexustock.Modules.Rma;
using Nexustock.Modules.Wave;
using Nexustock.Modules.Wave.Contexts;
using Nexustock.Modules.Lpn.Contexts;
using Nexustock.Modules.Lpn.Services;
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
    builder.Services.AddInboundModule(builder.Configuration);
    builder.Services.AddQcModule(builder.Configuration);
    builder.Services.AddInventoryModule(builder.Configuration);
    builder.Services.AddExceptionsModule(builder.Configuration);
    builder.Services.AddRulesModule(builder.Configuration);
    builder.Services.AddPutawayModule(builder.Configuration);
    builder.Services.AddAllocationModule(builder.Configuration);
    builder.Services.AddReplenishmentModule(builder.Configuration);
    builder.Services.AddLpnModule(builder.Configuration);
    builder.Services.AddSerialModule(builder.Configuration);
    builder.Services.AddRmaModule(builder.Configuration);
    builder.Services.AddWaveModule(builder.Configuration);

    // Register Hangfire for Background Jobs
    var defaultConn = builder.Configuration.GetConnectionString("Default");
    if (!string.IsNullOrEmpty(defaultConn))
    {
        builder.Services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(options =>
            {
                options.UseNpgsqlConnection(defaultConn);
            }));
        builder.Services.AddHangfireServer();
    }

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

    var uploadPath = app.Configuration["UploadSettings:UploadPath"] ?? "D:\\NexustockUploads";
    var requestPath = app.Configuration["UploadSettings:RequestPath"] ?? "/uploads";

    if (!Directory.Exists(uploadPath))
    {
        Directory.CreateDirectory(uploadPath);
    }

    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploadPath),
        RequestPath = requestPath
    });

    app.UseMiddleware<ExceptionHandlingMiddleware>();
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

    // Hangfire Dashboard and Recurring Jobs
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

    // Apply Migrations
    using (var scope = app.Services.CreateScope())
    {
        try
        {
            var identityDb = scope.ServiceProvider.GetRequiredService<Nexustock.Modules.Identity.Contexts.IdentityDbContext>();
            await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.MigrateAsync(identityDb.Database);
            Log.Information("Identity database migrated successfully.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occurred while migrating the Identity database");
        }

        try
        {
            var masterDataDb = scope.ServiceProvider.GetRequiredService<Nexustock.Modules.MasterData.Contexts.MasterDataDbContext>();
            await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.MigrateAsync(masterDataDb.Database);
            Log.Information("MasterData database migrated successfully.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occurred while migrating the MasterData database");
        }

        try
        {
            var inboundDb = scope.ServiceProvider.GetRequiredService<Nexustock.Modules.Inbound.Contexts.InboundDbContext>();
            await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.MigrateAsync(inboundDb.Database);
            Log.Information("Inbound database migrated successfully.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occurred while migrating the Inbound database");
        }

        try
        {
            var qcDb = scope.ServiceProvider.GetRequiredService<Nexustock.Modules.Qc.Contexts.QcDbContext>();
            await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.MigrateAsync(qcDb.Database);
            Log.Information("Qc database migrated successfully.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occurred while migrating the Qc database");
        }

        try
        {
            var lpnDb = scope.ServiceProvider.GetRequiredService<LpnDbContext>();
            await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.MigrateAsync(lpnDb.Database);
            Log.Information("Lpn database migrated successfully.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occurred while migrating the Lpn database");
        }

        try
        {
            var inventoryDb = scope.ServiceProvider.GetRequiredService<Nexustock.Modules.Inventory.Contexts.InventoryDbContext>();
            await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.MigrateAsync(inventoryDb.Database);
            Log.Information("Inventory database migrated successfully.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occurred while migrating the Inventory database");
        }

        try
        {
            var exceptionsDb = scope.ServiceProvider.GetRequiredService<Nexustock.Modules.Exceptions.Contexts.ExceptionsDbContext>();
            await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.MigrateAsync(exceptionsDb.Database);
            Log.Information("Exceptions database migrated successfully.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occurred while migrating the Exceptions database");
        }

        try
        {
            var rulesDb = scope.ServiceProvider.GetRequiredService<Nexustock.Modules.Rules.Contexts.RulesDbContext>();
            await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.MigrateAsync(rulesDb.Database);
            Log.Information("Rules database migrated successfully.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occurred while migrating the Rules database");
        }

        try
        {
            var putawayDb = scope.ServiceProvider.GetRequiredService<Nexustock.Modules.Putaway.Contexts.PutawayDbContext>();
            await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.MigrateAsync(putawayDb.Database);
            Log.Information("Putaway database migrated successfully.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occurred while migrating the Putaway database");
        }

        try
        {
            var replenishmentDb = scope.ServiceProvider.GetRequiredService<Nexustock.Modules.Replenishment.Contexts.ReplenishmentDbContext>();
            await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.MigrateAsync(replenishmentDb.Database);
            Log.Information("Replenishment database migrated successfully.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occurred while migrating the Replenishment database");
        }

        try
        {
            var rmaDb = scope.ServiceProvider.GetRequiredService<Nexustock.Modules.Rma.Contexts.RmaDbContext>();
            await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.MigrateAsync(rmaDb.Database);
            Log.Information("Rma database migrated successfully.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occurred while migrating the Rma database");
        }

        try
        {
            var waveDb = scope.ServiceProvider.GetRequiredService<WaveDbContext>();
            await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.MigrateAsync(waveDb.Database);
            Log.Information("Wave database migrated successfully.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occurred while migrating the Wave database");
        }
    }

    // Run Database Seeding
    try
    {
        var identityPermissions = new List<(string Code, string Name, string Group)>
        {
            ("Identity.Users.View", "Xem người dùng", "Identity"),
            ("Identity.Users.Create", "Thêm người dùng", "Identity"),
            ("Identity.Users.Edit", "Sửa người dùng", "Identity"),
            ("Identity.Roles.View", "Xem vai trò & quyền", "Identity"),
            ("Identity.Roles.Create", "Thêm vai trò", "Identity"),
            ("Identity.Roles.Edit", "Sửa vai trò", "Identity"),
            ("Identity.Roles.Delete", "Xóa vai trò", "Identity"),
            ("Identity.Audit.View", "Xem nhật ký hệ thống", "Identity"),
            ("Inbound.Orders.View", "Xem danh sách phiếu nhập", "Inbound"),
            ("Inbound.Orders.Create", "Tạo mới phiếu nhập", "Inbound"),
            ("Inbound.Orders.Receive", "Nhận hàng thực tế", "Inbound"),
            ("Inbound.Orders.Approve", "Phê duyệt nhận hàng vượt dung sai", "Inbound"),
            ("Inbound.Lots.View", "Tra cứu lô hàng", "Inbound"),
            ("Qc.Queue.View", "Xem hàng chờ QC", "QC"),
            ("Qc.Results.Create", "Ghi kết quả QC", "QC"),
            ("Qc.Lots.Hold", "Khóa lô hàng", "QC"),
            ("Qc.Lots.Release", "Giải phóng lô hàng", "QC"),
            ("Qc.Lots.Reject", "Từ chối lô hàng", "QC"),
            ("Inventory.Balances.View", "Xem số dư tồn kho", "Inventory"),
            ("Inventory.Movements.Create", "Dịch chuyển tồn kho", "Inventory"),
            ("Inventory.Locks.Manage", "Quản lý khóa vị trí", "Inventory"),
            ("Outbound.Shipments.View", "Xem đơn xuất kho", "Outbound"),
            ("Outbound.Shipments.Create", "Tạo đơn xuất kho", "Outbound"),
            ("Outbound.Picks.Execute", "Thực hiện lấy hàng", "Outbound"),
            ("Outbound.Packing.Execute", "Thực hiện đóng gói", "Outbound"),
            ("Inventory.CycleCount.View", "Xem đợt kiểm kê", "Inventory"),
            ("Inventory.CycleCount.Create", "Tạo đợt kiểm kê", "Inventory"),
            ("Inventory.CycleCount.Count", "Nhập kết quả kiểm kê", "Inventory"),
            ("Inventory.CycleCount.Approve.L1", "Duyệt chênh lệch cấp 1 (<10M VNĐ)", "Inventory"),
            ("Inventory.CycleCount.Approve.L2", "Duyệt chênh lệch cấp 2 (10M-100M VNĐ)", "Inventory"),
            ("Inventory.CycleCount.Approve.L3", "Duyệt chênh lệch cấp 3 (>100M VNĐ)", "Inventory"),
            ("rf_mobile_core_scan.read", "Xem thiết bị và log di động", "Mobile"),
            ("rf_mobile_core_scan.create", "Quét mã và gửi sự kiện", "Mobile"),
            ("rf_mobile_core_scan.update", "Thực hiện nhiệm vụ di động", "Mobile"),
            ("exception_framework_mvp.read", "Xem danh sach ngoai le", "Exceptions"),
            ("exception_framework_mvp.create", "Tao ngoai le van hanh", "Exceptions"),
            ("exception_framework_mvp.update", "Gan va cap nhat ngoai le", "Exceptions"),
            ("exception_framework_mvp.approve", "Phe duyet/Resolve ngoai le", "Exceptions"),
            ("rule_engine_foundation.read", "Xem cấu hình luật động", "Rules"),
            ("rule_engine_foundation.create", "Tạo mới luật động", "Rules"),
            ("rule_engine_foundation.update", "Cập nhật luật động", "Rules"),
            ("putaway_slotting.read", "Xem cấu hình và đề xuất cất hàng", "Putaway"),
            ("putaway_slotting.create", "Thực hiện và từ chối đề xuất cất hàng", "Putaway"),
            ("allocation_reservation.read", "Xem danh sách giữ hàng và tồn khả dụng", "Allocation"),
            ("allocation_reservation.create", "Thực hiện phân bổ và giải phóng giữ hàng", "Allocation"),
            ("replenishment.read", "Xem cấu hình và nhiệm vụ bổ sung", "Replenishment"),
            ("replenishment.create", "Tạo cấu hình bổ sung", "Replenishment"),
            ("replenishment.execute", "Chạy quét và hoàn tất bổ sung", "Replenishment"),
            ("lpn.read", "Xem thông tin LPN", "LPN"),
            ("lpn.create", "Tạo mới LPN", "LPN"),
            ("lpn.update", "Đóng/Rút và di chuyển LPN", "LPN"),
            ("lpn.execute", "Thực hiện quét LPN di động", "LPN"),
            ("serial.execute", "Xác thực và quét Serial di động", "Serial"),
            ("rma.read", "Xem danh sách trả hàng RMA", "RMA"),
            ("rma.create", "Tạo yêu cầu trả hàng RMA", "RMA"),
            ("rma.update", "Tiếp nhận hàng trả RMA", "RMA"),
            ("rma.qc", "Kiểm định và xử lý hàng RMA", "RMA"),
            ("Wave.Manage", "Quản lý Wave Picking", "Wave")
        };

        var appPermissions = Nexustock.Modules.MasterData.Permissions.AppPermissions.All
            .Select(p => (p.Code, p.Name, p.Group))
            .Concat(identityPermissions);

        await Nexustock.Modules.Identity.Seeders.IdentitySeeder.SeedAsync(app.Services, appPermissions);

        using (var scope = app.Services.CreateScope())
        {
            var inventoryDb = scope.ServiceProvider.GetRequiredService<Nexustock.Modules.Inventory.Contexts.InventoryDbContext>();
            var masterDb = scope.ServiceProvider.GetRequiredService<Nexustock.Modules.MasterData.Contexts.MasterDataDbContext>();
            
            var hasTasks = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync(inventoryDb.MobileTasks);
            if (!hasTasks)
            {
                var tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
                var locA = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(masterDb.StorageLocations, l => l.Code == "LOC-A-01");
                var locB = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(masterDb.StorageLocations, l => l.Code == "LOC-A-02");
                
                if (locA != null)
                {
                    inventoryDb.MobileTasks.Add(new Nexustock.Modules.Inventory.Entities.MobileTask
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        ReferenceType = "PICKING",
                        ReferenceId = Guid.NewGuid(),
                        Step = "SCAN_LOC",
                        LocationId = locA.Id,
                        Status = "Open",
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = "System"
                    });
                }
                
                if (locB != null)
                {
                    inventoryDb.MobileTasks.Add(new Nexustock.Modules.Inventory.Entities.MobileTask
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        ReferenceType = "PICKING",
                        ReferenceId = Guid.NewGuid(),
                        Step = "SCAN_LOC",
                        LocationId = locB.Id,
                        Status = "Open",
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = "System"
                    });
                }
                
                await inventoryDb.SaveChangesAsync();
                Log.Information("Seeded MobileTasks for integration testing.");
            }

            var hasInventory = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync(inventoryDb.Inventories, i => i.LotNo == "LOT-SAMPLE-001");
            if (!hasInventory)
            {
                var tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
                var product = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(masterDb.Products);
                var locA = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(masterDb.StorageLocations, l => l.Code == "LOC-A-01");
                if (product != null && locA != null)
                {
                    inventoryDb.Inventories.Add(new Nexustock.Modules.Inventory.Entities.Inventory
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        ItemId = product.Id,
                        LocationId = locA.Id,
                        LotNo = "LOT-SAMPLE-001",
                        QtyOnHand = 100,
                        QtyReserved = 0,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = "System"
                    });
                    await inventoryDb.SaveChangesAsync();
                    Log.Information("Seeded Inventory Balance for LOT-SAMPLE-001 at LOC-A-01.");
                }
            }

            // Seed test Lot for Putaway E2E test
            var hasPutawayLot = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync(inventoryDb.Lots, l => l.LotNo == "LOT-PUT-E2E-001");
            if (!hasPutawayLot)
            {
                var tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
                var product = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(masterDb.Products);
                if (product != null)
                {
                    inventoryDb.Lots.Add(new Nexustock.Modules.Inventory.Entities.Lot
                    {
                        Id = Guid.Parse("a1b2c3d4-1234-4567-89ab-cdef01234567"),
                        TenantId = tenantId,
                        LotNo = "LOT-PUT-E2E-001",
                        ItemId = product.Id,
                        QcStatus = "Release"
                    });
                    await inventoryDb.SaveChangesAsync();
                    Log.Information("Seeded test Lot LOT-PUT-E2E-001 for Putaway E2E test.");
                }
            }
        }
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
// Watch trigger


static string ToServiceStatus(HealthStatus status) => status switch
{
    HealthStatus.Healthy => "healthy",
    HealthStatus.Degraded => "degraded",
    _ => "unhealthy"
};

// Partial class để WebApplicationFactory có thể tham chiếu trong integration test
// Top-level statement Program class là internal, nên partial cũng phải internal
internal partial class Program { }

