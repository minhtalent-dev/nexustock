using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexustock.Modules.Exceptions.Migrations
{
    /// <inheritdoc />
    public partial class AddExceptionFrameworkTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "operational_exceptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reference_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    reference_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    lot_no = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    qty = table.Column<decimal>(type: "numeric", nullable: false),
                    reason_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operational_exceptions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "exception_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    exception_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    sla_deadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exception_assignments", x => x.id);
                    table.ForeignKey(
                        name: "FK_exception_assignments_operational_exceptions_exception_id",
                        column: x => x.exception_id,
                        principalTable: "operational_exceptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "exception_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    exception_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transition = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    actor = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exception_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_exception_events_operational_exceptions_exception_id",
                        column: x => x.exception_id,
                        principalTable: "operational_exceptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_exception_assign_tenant_exception",
                table: "exception_assignments",
                columns: new[] { "tenant_id", "exception_id" });

            migrationBuilder.CreateIndex(
                name: "IX_exception_assignments_exception_id",
                table: "exception_assignments",
                column: "exception_id");

            migrationBuilder.CreateIndex(
                name: "idx_exception_events_tenant_exception",
                table: "exception_events",
                columns: new[] { "tenant_id", "exception_id" });

            migrationBuilder.CreateIndex(
                name: "IX_exception_events_exception_id",
                table: "exception_events",
                column: "exception_id");

            migrationBuilder.CreateIndex(
                name: "idx_exceptions_tenant_location",
                table: "operational_exceptions",
                columns: new[] { "tenant_id", "location_id" });

            migrationBuilder.CreateIndex(
                name: "idx_exceptions_tenant_reference",
                table: "operational_exceptions",
                columns: new[] { "tenant_id", "reference_id" });

            migrationBuilder.CreateIndex(
                name: "uq_operational_exceptions_tenant_code",
                table: "operational_exceptions",
                columns: new[] { "tenant_id", "code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "exception_assignments");

            migrationBuilder.DropTable(
                name: "exception_events");

            migrationBuilder.DropTable(
                name: "operational_exceptions");
        }
    }
}
