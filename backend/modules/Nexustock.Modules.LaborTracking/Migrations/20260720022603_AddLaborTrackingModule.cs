using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexustock.Modules.LaborTracking.Migrations
{
    /// <inheritdoc />
    public partial class AddLaborTrackingModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "labor_session_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    actor = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: true),
                    trace_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_labor_session_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "labor_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_task_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    source_task_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reference_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    reference_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    shift_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    zone_id = table.Column<Guid>(type: "uuid", nullable: true),
                    operation_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    duration_seconds = table.Column<int>(type: "integer", nullable: false),
                    paused_seconds = table.Column<int>(type: "integer", nullable: false),
                    last_paused_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    timeout_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_labor_sessions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "labor_shifts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    shift_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_labor_shifts", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_labor_session_events_occurred_at",
                table: "labor_session_events",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "IX_labor_session_events_tenant_id_session_id",
                table: "labor_session_events",
                columns: new[] { "tenant_id", "session_id" });

            migrationBuilder.CreateIndex(
                name: "IX_labor_sessions_active_user",
                table: "labor_sessions",
                columns: new[] { "tenant_id", "user_id" },
                unique: true,
                filter: "\"status\" IN ('Running', 'Paused')");

            migrationBuilder.CreateIndex(
                name: "IX_labor_sessions_tenant_id_shift_id",
                table: "labor_sessions",
                columns: new[] { "tenant_id", "shift_id" });

            migrationBuilder.CreateIndex(
                name: "IX_labor_sessions_tenant_id_source_task_type_source_task_id",
                table: "labor_sessions",
                columns: new[] { "tenant_id", "source_task_type", "source_task_id" });

            migrationBuilder.CreateIndex(
                name: "IX_labor_sessions_tenant_id_user_id_status",
                table: "labor_sessions",
                columns: new[] { "tenant_id", "user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_labor_sessions_tenant_id_zone_id_started_at",
                table: "labor_sessions",
                columns: new[] { "tenant_id", "zone_id", "started_at" });

            migrationBuilder.CreateIndex(
                name: "IX_labor_shifts_shift_code",
                table: "labor_shifts",
                column: "shift_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_labor_shifts_tenant_id_user_id_status",
                table: "labor_shifts",
                columns: new[] { "tenant_id", "user_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "labor_session_events");

            migrationBuilder.DropTable(
                name: "labor_sessions");

            migrationBuilder.DropTable(
                name: "labor_shifts");
        }
    }
}
