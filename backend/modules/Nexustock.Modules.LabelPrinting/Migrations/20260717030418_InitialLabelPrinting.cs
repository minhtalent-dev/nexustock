using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexustock.Modules.LabelPrinting.Migrations
{
    /// <inheritdoc />
    public partial class InitialLabelPrinting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "label_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    raw_template = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_label_templates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "print_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    printer_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    payload_json = table.Column<string>(type: "text", nullable: false),
                    rendered_command = table.Column<string>(type: "text", nullable: false),
                    rendered_command_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    source_job_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    reprint_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    error_message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_print_jobs", x => x.id);
                    table.ForeignKey(
                        name: "FK_print_jobs_label_templates_template_id",
                        column: x => x.template_id,
                        principalTable: "label_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "label_templates",
                columns: new[] { "id", "created_at", "created_by", "is_active", "language", "name", "raw_template", "row_version", "template_code", "tenant_id", "updated_at", "updated_by" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000002201"), new DateTimeOffset(new DateTime(2026, 7, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SYSTEM", true, "zpl", "Default LPN Label", "^XA^FO40,40^FD{{lpnCode}}^FS^FO40,90^FD{{itemCode}}^FS^XZ", 1, "DEFAULT_LPN_ZPL", new Guid("00000000-0000-0000-0000-000000000001"), null, null });

            migrationBuilder.CreateIndex(
                name: "idx_label_templates_tenant_active",
                table: "label_templates",
                columns: new[] { "tenant_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "uq_label_templates_tenant_code",
                table: "label_templates",
                columns: new[] { "tenant_id", "template_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_print_jobs_tenant_created",
                table: "print_jobs",
                columns: new[] { "tenant_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "idx_print_jobs_tenant_status",
                table: "print_jobs",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_print_jobs_template_id",
                table: "print_jobs",
                column: "template_id");

            migrationBuilder.CreateIndex(
                name: "uq_print_jobs_tenant_idempotency",
                table: "print_jobs",
                columns: new[] { "tenant_id", "idempotency_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "print_jobs");

            migrationBuilder.DropTable(
                name: "label_templates");
        }
    }
}
