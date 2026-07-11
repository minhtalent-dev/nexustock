using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexustock.Modules.Qc.Migrations
{
    /// <inheritdoc />
    public partial class AddQcModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "material_holds",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    held_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    released_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    released_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_material_holds", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "qc_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sample_plan = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_qc_requests", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "qc_results",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    qc_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_passed = table.Column<bool>(type: "boolean", nullable: false),
                    metrics = table.Column<string>(type: "text", nullable: true),
                    attachment_refs = table.Column<string>(type: "text", nullable: true),
                    inspector = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_qc_results", x => x.id);
                    table.ForeignKey(
                        name: "FK_qc_results_qc_requests_qc_request_id",
                        column: x => x.qc_request_id,
                        principalTable: "qc_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_material_holds_tenant_lot_status",
                table: "material_holds",
                columns: new[] { "tenant_id", "lot_id", "status" });

            migrationBuilder.CreateIndex(
                name: "idx_qc_requests_tenant_lot_status",
                table: "qc_requests",
                columns: new[] { "tenant_id", "lot_id", "status" });

            migrationBuilder.CreateIndex(
                name: "uq_qc_requests_pending_lot",
                table: "qc_requests",
                column: "lot_id",
                unique: true,
                filter: "\"status\" = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_qc_results_qc_request_id",
                table: "qc_results",
                column: "qc_request_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "material_holds");

            migrationBuilder.DropTable(
                name: "qc_results");

            migrationBuilder.DropTable(
                name: "qc_requests");
        }
    }
}
