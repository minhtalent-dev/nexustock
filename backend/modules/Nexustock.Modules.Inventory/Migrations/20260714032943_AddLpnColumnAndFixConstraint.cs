using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexustock.Modules.Inventory.Migrations
{
    /// <inheritdoc />
    public partial class AddLpnColumnAndFixConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop constraint cũ (4 cột, không có lpn_id)
            migrationBuilder.DropIndex(
                name: "uq_inventories_tenant_item_lot_location",
                table: "inventories");

            // Tạo constraint mới (5 cột, gồm lpn_id để phân biệt free vs LPN row)
            migrationBuilder.CreateIndex(
                name: "uq_inventories_tenant_item_lot_location_lpn",
                table: "inventories",
                columns: new[] { "tenant_id", "item_id", "lot_no", "location_id", "lpn_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_inventories_tenant_item_lot_location_lpn",
                table: "inventories");

            migrationBuilder.CreateIndex(
                name: "uq_inventories_tenant_item_lot_location",
                table: "inventories",
                columns: new[] { "tenant_id", "item_id", "lot_no", "location_id" },
                unique: true);
        }
    }
}
