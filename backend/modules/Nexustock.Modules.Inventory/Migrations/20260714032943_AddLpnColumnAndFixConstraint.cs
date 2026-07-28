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
            migrationBuilder.AddColumn<Guid>(
                name: "lpn_id",
                table: "inventories",
                type: "uuid",
                nullable: true);

            // Drop constraint cũ (4 cột, không có lpn_id) nếu còn tồn tại trong DB dev/test.
            migrationBuilder.Sql("DROP INDEX IF EXISTS uq_inventories_tenant_item_lot_location;");

            // Tạo constraint mới (5 cột, gồm lpn_id để phân biệt free vs LPN row) nếu DB chưa có.
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX IF NOT EXISTS uq_inventories_tenant_item_lot_location_lpn " +
                "ON inventories (tenant_id, item_id, lot_no, location_id, lpn_id);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_inventories_tenant_item_lot_location_lpn",
                table: "inventories");

            migrationBuilder.DropColumn(
                name: "lpn_id",
                table: "inventories");

            migrationBuilder.CreateIndex(
                name: "uq_inventories_tenant_item_lot_location",
                table: "inventories",
                columns: new[] { "tenant_id", "item_id", "lot_no", "location_id" },
                unique: true);
        }
    }
}
