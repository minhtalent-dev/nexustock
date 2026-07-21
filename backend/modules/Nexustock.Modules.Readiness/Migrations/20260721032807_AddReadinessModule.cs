using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexustock.Modules.Readiness.Migrations
{
    /// <inheritdoc />
    public partial class AddReadinessModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "readiness");

            migrationBuilder.CreateTable(
                name: "cutover_freeze_states",
                schema: "readiness",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsFrozen = table.Column<bool>(type: "boolean", nullable: false),
                    FrozenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FrozenBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cutover_freeze_states", x => x.TenantId);
                });

            migrationBuilder.CreateTable(
                name: "cutover_logs",
                schema: "readiness",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Actor = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Note = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    TraceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cutover_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "incident_drills",
                schema: "readiness",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScenarioCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RtoMinutes = table.Column<int>(type: "integer", nullable: false),
                    Passed = table.Column<bool>(type: "boolean", nullable: false),
                    ConductedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ConductedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EvidenceNote = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    TraceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_incident_drills", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "uat_runs",
                schema: "readiness",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScenarioCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ResultNote = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    SignedOffBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    SignedOffAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EvidenceUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    TraceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_uat_runs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_cutover_logs_tenant_step_started",
                schema: "readiness",
                table: "cutover_logs",
                columns: new[] { "TenantId", "StepCode", "StartedAt" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "idx_uat_runs_tenant_scenario_created",
                schema: "readiness",
                table: "uat_runs",
                columns: new[] { "TenantId", "ScenarioCode", "CreatedAt" },
                descending: new[] { false, false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cutover_freeze_states",
                schema: "readiness");

            migrationBuilder.DropTable(
                name: "cutover_logs",
                schema: "readiness");

            migrationBuilder.DropTable(
                name: "incident_drills",
                schema: "readiness");

            migrationBuilder.DropTable(
                name: "uat_runs",
                schema: "readiness");
        }
    }
}
