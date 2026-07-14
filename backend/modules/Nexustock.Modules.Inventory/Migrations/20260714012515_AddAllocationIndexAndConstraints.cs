using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexustock.Modules.Inventory.Migrations
{
    /// <inheritdoc />
    public partial class AddAllocationIndexAndConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "chk_inventory_balances_qty_available",
                table: "inventories",
                sql: "qty_on_hand >= qty_reserved");

            migrationBuilder.AddCheckConstraint(
                name: "chk_inventory_balances_qty_reserved",
                table: "inventories",
                sql: "qty_reserved >= 0.0");

            migrationBuilder.CreateIndex(
                name: "idx_allocation_reservations_balance",
                table: "allocation_reservations",
                columns: new[] { "tenant_id", "inventory_balance_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "chk_inventory_balances_qty_available",
                table: "inventories");

            migrationBuilder.DropCheckConstraint(
                name: "chk_inventory_balances_qty_reserved",
                table: "inventories");

            migrationBuilder.DropIndex(
                name: "idx_allocation_reservations_balance",
                table: "allocation_reservations");
        }
    }
}
