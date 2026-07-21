using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nexustock.Modules.Observability.Contexts;
using Nexustock.Modules.Observability.Entities;
using Serilog;

namespace Nexustock.Api.Infrastructure;

/// <summary>
/// Seed permissions, demo inventory data và feature flags khởi tạo.
/// </summary>
public static class DatabaseSeeder
{
    private static readonly (string Code, string Name, string Group)[] ExtraPermissions =
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
        ("Wave.Manage", "Quản lý Wave Picking", "Wave"),
        ("local_agent.view", "Xem trạng thái trạm và thiết bị", "LocalAgent"),
        ("local_agent.pair", "Thực hiện ghép cặp trạm mới", "LocalAgent"),
        ("local_agent.revoke", "Thu hồi quyền của trạm làm việc", "LocalAgent"),
        ("label_printing.view", "Xem lệnh in tem", "LabelPrinting"),
        ("label_printing.print", "Thực hiện in tem", "LabelPrinting"),
        ("label_printing.reprint", "Thực hiện in lại tem", "LabelPrinting"),
        ("integration.view", "Xem log tích hợp", "Integration"),
        ("integration.import", "Thực hiện import thủ công", "Integration"),
        ("integration.export", "Xuất dữ liệu tồn kho", "Integration"),
        ("webhook.manage", "Quản lý Webhook Subscription", "Webhook"),
        ("webhook.replay", "Replay Webhook DLQ", "Webhook"),
        ("cross_docking.read", "Xem danh sách cross-dock candidates", "CrossDocking"),
        ("cross_docking.create", "Đánh giá lô hàng cho cross-dock", "CrossDocking"),
        ("cross_docking.approve", "Chấp nhận/từ chối cross-dock candidate", "CrossDocking"),
        ("cross_docking.export", "Xuất báo cáo cross-dock", "CrossDocking"),
        ("labor_tracking.read", "Xem danh sách và KPI labor tracking", "LaborTracking"),
        ("labor_tracking.create", "Tạo phiên làm việc labor tracking", "LaborTracking"),
        ("labor_tracking.update", "Cập nhật trạng thái session labor tracking", "LaborTracking"),
        ("labor_tracking.delete", "Xóa dữ liệu hoặc đóng ca labor tracking", "LaborTracking"),
        ("task_interleaving.read", "Xem danh sách và gợi ý task interleaving", "TaskInterleaving"),
        ("task_interleaving.accept", "Chấp nhận gợi ý task interleaving", "TaskInterleaving"),
        ("task_interleaving.reject", "Từ chối gợi ý task interleaving", "TaskInterleaving"),
        ("readiness.read", "Xem readiness probe và cutover board", "Readiness"),
        ("readiness.uat.write", "Ghi kết quả UAT run", "Readiness"),
        ("readiness.uat.signoff", "Ký nghiệm thu UAT", "Readiness"),
        ("readiness.cutover.freeze", "Freeze/unfreeze write API khi cutover", "Readiness"),
        ("readiness.drill.write", "Ghi kết quả incident drill", "Readiness")
    };

    private static readonly (string Name, string Description)[] DefaultFeatureFlags =
    {
        ("FF_CROSS_DOCKING_ENABLED", "Enable Cross-docking feature"),
        ("FF_LABOR_TRACKING_ENABLED", "Enable Labor Tracking feature"),
        ("FF_TASK_INTERLEAVING_ENABLED", "Enable Task Interleaving feature"),
        ("FF_READINESS_GATE_ENABLED", "Enable Readiness Gate API and UI"),
        ("FF_CUTOVER_FREEZE_ENABLED", "Allow cutover freeze/unfreeze write APIs")
    };

    public static async Task SeedAsync(IServiceProvider services)
    {
        try
        {
            var appPermissions = Nexustock.Modules.MasterData.Permissions.AppPermissions.All
                .Select(p => (p.Code, p.Name, p.Group))
                .Concat(ExtraPermissions);

            await Nexustock.Modules.Identity.Seeders.IdentitySeeder.SeedAsync(services, appPermissions);

            using var scope = services.CreateScope();
            await SeedDemoInventoryAsync(scope.ServiceProvider);
            await SeedFeatureFlagsAsync(scope.ServiceProvider);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occurred while seeding the database");
        }
    }

    private static async Task SeedDemoInventoryAsync(IServiceProvider sp)
    {
        var inventoryDb = sp.GetRequiredService<Nexustock.Modules.Inventory.Contexts.InventoryDbContext>();
        var masterDb = sp.GetRequiredService<Nexustock.Modules.MasterData.Contexts.MasterDataDbContext>();
        var tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

        var hasTasks = await inventoryDb.MobileTasks.AnyAsync();
        if (!hasTasks)
        {
            var locA = await masterDb.StorageLocations.FirstOrDefaultAsync(l => l.Code == "LOC-A-01");
            var locB = await masterDb.StorageLocations.FirstOrDefaultAsync(l => l.Code == "LOC-A-02");

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

        var hasInventory = await inventoryDb.Inventories.AnyAsync(i => i.LotNo == "LOT-SAMPLE-001");
        if (!hasInventory)
        {
            var product = await masterDb.Products.FirstOrDefaultAsync();
            var locA = await masterDb.StorageLocations.FirstOrDefaultAsync(l => l.Code == "LOC-A-01");
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

        var hasPutawayLot = await inventoryDb.Lots.AnyAsync(l => l.LotNo == "LOT-PUT-E2E-001");
        if (!hasPutawayLot)
        {
            var product = await masterDb.Products.FirstOrDefaultAsync();
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

    private static async Task SeedFeatureFlagsAsync(IServiceProvider sp)
    {
        var observabilityDb = sp.GetRequiredService<ObservabilityDbContext>();
        foreach (var (name, description) in DefaultFeatureFlags)
        {
            var exists = await observabilityDb.FeatureFlags.AnyAsync(f => f.Name == name);
            if (exists) continue;

            observabilityDb.FeatureFlags.Add(new FeatureFlag
            {
                Name = name,
                Enabled = true,
                RolloutPercentage = 100,
                WhitelistUserIds = string.Empty,
                Description = description,
                UpdatedAt = DateTimeOffset.UtcNow
            });
            await observabilityDb.SaveChangesAsync();
            Log.Information("Seeded FeatureFlag {FlagName}.", name);
        }
    }
}
