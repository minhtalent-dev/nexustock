using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexustock.Modules.Inventory.Migrations
{
    /// <inheritdoc />
    public partial class AddAllocationTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "allocated_qty",
                table: "shipment_items",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "row_version",
                table: "shipment_items",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "shipment_items",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "allocation_reservations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shipment_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    inventory_balance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    qty = table.Column<decimal>(type: "numeric", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_allocation_reservations", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_allocation_reservations_expiry",
                table: "allocation_reservations",
                column: "expires_at",
                filter: "\"status\" = 'ACTIVE'");

            migrationBuilder.CreateIndex(
                name: "idx_allocation_reservations_shipment_line",
                table: "allocation_reservations",
                columns: new[] { "tenant_id", "shipment_line_id" });

            migrationBuilder.CreateIndex(
                name: "idx_allocation_reservations_tenant_status",
                table: "allocation_reservations",
                columns: new[] { "tenant_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "allocation_reservations");

            migrationBuilder.DropColumn(
                name: "allocated_qty",
                table: "shipment_items");

            migrationBuilder.DropColumn(
                name: "row_version",
                table: "shipment_items");

            migrationBuilder.DropColumn(
                name: "status",
                table: "shipment_items");
        }
    }
}
