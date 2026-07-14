using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexustock.Modules.Replenishment.Migrations
{
    /// <inheritdoc />
    public partial class InitialReplenishment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "replenishment_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    min_qty = table.Column<decimal>(type: "numeric", nullable: false),
                    max_qty = table.Column<decimal>(type: "numeric", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_replenishment_rules", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "replenishment_tasks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lot_no = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    requested_qty = table.Column<decimal>(type: "numeric", nullable: false),
                    actual_qty = table.Column<decimal>(type: "numeric", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    mobile_task_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_replenishment_tasks", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_replenishment_rules_tenant_item_location",
                table: "replenishment_rules",
                columns: new[] { "tenant_id", "item_id", "location_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_replenishment_tasks_tenant_status",
                table: "replenishment_tasks",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "idx_replenishment_tasks_tenant_target_location",
                table: "replenishment_tasks",
                columns: new[] { "tenant_id", "target_location_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "replenishment_rules");

            migrationBuilder.DropTable(
                name: "replenishment_tasks");
        }
    }
}
