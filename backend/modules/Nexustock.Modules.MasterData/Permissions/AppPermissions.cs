using Nexustock.Modules.MasterData.Entities;

namespace Nexustock.Modules.MasterData.Permissions;

public static class AppPermissions
{
    public const string Group = "MasterData";

    public static readonly IReadOnlyList<Permission> All = new List<Permission>
    {
        // Đơn vị tính
        new() { Id = new Guid("00000000-0000-0000-0000-000000001001"), Code = "MasterData.Uoms.View", Name = "Xem đơn vị tính", Group = Group, IsActive = true },
        new() { Id = new Guid("00000000-0000-0000-0000-000000001002"), Code = "MasterData.Uoms.Create", Name = "Thêm đơn vị tính", Group = Group, IsActive = true },
        new() { Id = new Guid("00000000-0000-0000-0000-000000001003"), Code = "MasterData.Uoms.Edit", Name = "Sửa đơn vị tính", Group = Group, IsActive = true },
        new() { Id = new Guid("00000000-0000-0000-0000-000000001004"), Code = "MasterData.Uoms.Delete", Name = "Xóa đơn vị tính", Group = Group, IsActive = true },

        // Vật tư
        new() { Id = new Guid("00000000-0000-0000-0000-000000002001"), Code = "MasterData.Products.View", Name = "Xem vật tư", Group = Group, IsActive = true },
        new() { Id = new Guid("00000000-0000-0000-0000-000000002002"), Code = "MasterData.Products.Create", Name = "Thêm vật tư", Group = Group, IsActive = true },
        new() { Id = new Guid("00000000-0000-0000-0000-000000002003"), Code = "MasterData.Products.Edit", Name = "Sửa vật tư", Group = Group, IsActive = true },
        new() { Id = new Guid("00000000-0000-0000-0000-000000002004"), Code = "MasterData.Products.Delete", Name = "Xóa vật tư", Group = Group, IsActive = true },

        // Nhà kho
        new() { Id = new Guid("00000000-0000-0000-0000-000000003001"), Code = "MasterData.Warehouses.View", Name = "Xem nhà kho", Group = Group, IsActive = true },
        new() { Id = new Guid("00000000-0000-0000-0000-000000003002"), Code = "MasterData.Warehouses.Create", Name = "Thêm nhà kho", Group = Group, IsActive = true },
        new() { Id = new Guid("00000000-0000-0000-0000-000000003003"), Code = "MasterData.Warehouses.Edit", Name = "Sửa nhà kho", Group = Group, IsActive = true },
        new() { Id = new Guid("00000000-0000-0000-0000-000000003004"), Code = "MasterData.Warehouses.Delete", Name = "Xóa nhà kho", Group = Group, IsActive = true },

        // Vùng kho
        new() { Id = new Guid("00000000-0000-0000-0000-000000004001"), Code = "MasterData.Zones.View", Name = "Xem vùng kho", Group = Group, IsActive = true },
        new() { Id = new Guid("00000000-0000-0000-0000-000000004002"), Code = "MasterData.Zones.Create", Name = "Thêm vùng kho", Group = Group, IsActive = true },
        new() { Id = new Guid("00000000-0000-0000-0000-000000004003"), Code = "MasterData.Zones.Edit", Name = "Sửa vùng kho", Group = Group, IsActive = true },
        new() { Id = new Guid("00000000-0000-0000-0000-000000004004"), Code = "MasterData.Zones.Delete", Name = "Xóa vùng kho", Group = Group, IsActive = true },

        // Vị trí kệ
        new() { Id = new Guid("00000000-0000-0000-0000-000000005001"), Code = "MasterData.Locations.View", Name = "Xem vị trí kệ", Group = Group, IsActive = true },
        new() { Id = new Guid("00000000-0000-0000-0000-000000005002"), Code = "MasterData.Locations.Create", Name = "Thêm vị trí kệ", Group = Group, IsActive = true },
        new() { Id = new Guid("00000000-0000-0000-0000-000000005003"), Code = "MasterData.Locations.Edit", Name = "Sửa vị trí kệ", Group = Group, IsActive = true },
        new() { Id = new Guid("00000000-0000-0000-0000-000000005004"), Code = "MasterData.Locations.Delete", Name = "Xóa vị trí kệ", Group = Group, IsActive = true },

        // Đối tác
        new() { Id = new Guid("00000000-0000-0000-0000-000000006001"), Code = "MasterData.Partners.View", Name = "Xem đối tác", Group = Group, IsActive = true },
        new() { Id = new Guid("00000000-0000-0000-0000-000000006002"), Code = "MasterData.Partners.Create", Name = "Thêm đối tác", Group = Group, IsActive = true },
        new() { Id = new Guid("00000000-0000-0000-0000-000000006003"), Code = "MasterData.Partners.Edit", Name = "Sửa đối tác", Group = Group, IsActive = true },
        new() { Id = new Guid("00000000-0000-0000-0000-000000006004"), Code = "MasterData.Partners.Delete", Name = "Xóa đối tác", Group = Group, IsActive = true },

        // Mã lý do
        new() { Id = new Guid("00000000-0000-0000-0000-000000007001"), Code = "MasterData.Reasons.View", Name = "Xem mã lý do", Group = Group, IsActive = true },
        new() { Id = new Guid("00000000-0000-0000-0000-000000007002"), Code = "MasterData.Reasons.Create", Name = "Thêm mã lý do", Group = Group, IsActive = true },
        new() { Id = new Guid("00000000-0000-0000-0000-000000007003"), Code = "MasterData.Reasons.Edit", Name = "Sửa mã lý do", Group = Group, IsActive = true },
        new() { Id = new Guid("00000000-0000-0000-0000-000000007004"), Code = "MasterData.Reasons.Delete", Name = "Xóa mã lý do", Group = Group, IsActive = true },

        // Nhập dữ liệu
        new() { Id = new Guid("00000000-0000-0000-0000-000000008001"), Code = "MasterData.Imports.Preview", Name = "Xem trước dữ liệu nhập", Group = Group, IsActive = true },
        new() { Id = new Guid("00000000-0000-0000-0000-000000008002"), Code = "MasterData.Imports.Commit", Name = "Xác nhận nhập dữ liệu", Group = Group, IsActive = true },
        new() { Id = new Guid("00000000-0000-0000-0000-000000008003"), Code = "MasterData.Imports.DownloadErrors", Name = "Tải file lỗi nhập dữ liệu", Group = Group, IsActive = true },
    };
}
