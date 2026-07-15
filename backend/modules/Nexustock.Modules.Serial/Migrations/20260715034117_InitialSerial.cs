using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexustock.Modules.Serial.Migrations
{
    /// <inheritdoc />
    public partial class InitialSerial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "serial_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    serial_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    from_location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    to_location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reference_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_serial_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "serial_numbers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    serial_no = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_serial_numbers", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_serial_events_tenant_serial",
                table: "serial_events",
                columns: new[] { "tenant_id", "serial_id" });

            migrationBuilder.CreateIndex(
                name: "idx_serials_tenant_location",
                table: "serial_numbers",
                columns: new[] { "tenant_id", "location_id" });

            migrationBuilder.CreateIndex(
                name: "idx_serials_tenant_status",
                table: "serial_numbers",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "uq_serials_tenant_item_no",
                table: "serial_numbers",
                columns: new[] { "tenant_id", "item_id", "serial_no" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "serial_events");

            migrationBuilder.DropTable(
                name: "serial_numbers");
        }
    }
}
