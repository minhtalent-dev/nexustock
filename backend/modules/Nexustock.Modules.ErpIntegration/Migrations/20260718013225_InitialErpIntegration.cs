using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexustock.Modules.ErpIntegration.Migrations
{
    /// <inheritdoc />
    public partial class InitialErpIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "integration_import_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    import_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    total_rows = table.Column<int>(type: "integer", nullable: false),
                    valid_rows = table.Column<int>(type: "integer", nullable: false),
                    error_rows = table.Column<int>(type: "integer", nullable: false),
                    preview_payload = table.Column<string>(type: "text", nullable: false),
                    trace_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_integration_import_jobs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "integration_mappings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_system = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    mapping_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    external_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    internal_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_integration_mappings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "integration_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    payload_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    external_system = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    external_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    contract_version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    direction = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    message_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    payload = table.Column<string>(type: "text", nullable: false),
                    response_payload = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    error_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    trace_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_integration_messages", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_integration_import_jobs_tenant_status",
                table: "integration_import_jobs",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "idx_integration_import_jobs_tenant_trace",
                table: "integration_import_jobs",
                columns: new[] { "tenant_id", "trace_id" });

            migrationBuilder.CreateIndex(
                name: "idx_integration_import_jobs_tenant_type",
                table: "integration_import_jobs",
                columns: new[] { "tenant_id", "import_type" });

            migrationBuilder.CreateIndex(
                name: "idx_integration_mappings_tenant_internal",
                table: "integration_mappings",
                columns: new[] { "tenant_id", "internal_code" });

            migrationBuilder.CreateIndex(
                name: "uq_integration_mappings_tenant_sys_type_code",
                table: "integration_mappings",
                columns: new[] { "tenant_id", "external_system", "mapping_type", "external_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_integration_messages_tenant_hash",
                table: "integration_messages",
                columns: new[] { "tenant_id", "payload_hash" });

            migrationBuilder.CreateIndex(
                name: "idx_integration_messages_tenant_ref",
                table: "integration_messages",
                columns: new[] { "tenant_id", "external_reference" });

            migrationBuilder.CreateIndex(
                name: "idx_integration_messages_tenant_status",
                table: "integration_messages",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "idx_integration_messages_tenant_system",
                table: "integration_messages",
                columns: new[] { "tenant_id", "external_system" });

            migrationBuilder.CreateIndex(
                name: "idx_integration_messages_tenant_trace",
                table: "integration_messages",
                columns: new[] { "tenant_id", "trace_id" });

            migrationBuilder.CreateIndex(
                name: "uq_integration_messages_tenant_idem",
                table: "integration_messages",
                columns: new[] { "tenant_id", "idempotency_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "integration_import_jobs");

            migrationBuilder.DropTable(
                name: "integration_mappings");

            migrationBuilder.DropTable(
                name: "integration_messages");
        }
    }
}
