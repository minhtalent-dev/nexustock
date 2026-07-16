using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexustock.Modules.Inventory.Migrations
{
    /// <inheritdoc />
    public partial class AddScalePackingWeightGovernance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "manual_override_id",
                table: "packing_records",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "scale_stable",
                table: "packing_records",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "weight_source",
                table: "packing_records",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "manual_weight_overrides",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shipment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    package_no = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    manual_weight = table.Column<decimal>(type: "numeric", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: false),
                    used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manual_weight_overrides", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_packing_records_tenant_manual_override",
                table: "packing_records",
                columns: new[] { "tenant_id", "manual_override_id" });


            migrationBuilder.CreateIndex(
                name: "idx_manual_weight_overrides_lookup",
                table: "manual_weight_overrides",
                columns: new[] { "tenant_id", "shipment_id", "package_no", "used_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "manual_weight_overrides");

            migrationBuilder.DropIndex(
                name: "idx_packing_records_tenant_manual_override",
                table: "packing_records");


            migrationBuilder.DropColumn(
                name: "manual_override_id",
                table: "packing_records");

            migrationBuilder.DropColumn(
                name: "scale_stable",
                table: "packing_records");

            migrationBuilder.DropColumn(
                name: "weight_source",
                table: "packing_records");

        }
    }
}
