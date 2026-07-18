using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexustock.Modules.Observability.Migrations
{
    /// <inheritdoc />
    public partial class Phase25_Observability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActivityTimeline",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorName = table.Column<string>(type: "text", nullable: true),
                    TraceId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    MetadataJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityTimeline", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KpiSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    MetricKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MetricGroup = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Value = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Unit = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SourceModule = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ComputedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MetadataJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KpiSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OperationalAlerts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AlertType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    SourceModule = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SourceEntityType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SourceEntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    TraceId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    MetricValue = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    ThresholdValue = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    AcknowledgedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    AcknowledgedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolvedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationalAlerts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TraceLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    TraceId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SpanName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Source = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    DurationMs = table.Column<int>(type: "integer", nullable: true),
                    MetadataJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TraceLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityTimeline_ActorUserId",
                table: "ActivityTimeline",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityTimeline_TenantId_EntityType_EntityId_CreatedAt",
                table: "ActivityTimeline",
                columns: new[] { "TenantId", "EntityType", "EntityId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityTimeline_TenantId_TraceId",
                table: "ActivityTimeline",
                columns: new[] { "TenantId", "TraceId" });

            migrationBuilder.CreateIndex(
                name: "IX_KpiSnapshots_ComputedAt",
                table: "KpiSnapshots",
                column: "ComputedAt");

            migrationBuilder.CreateIndex(
                name: "IX_KpiSnapshots_TenantId_MetricGroup_MetricKey_PeriodEnd",
                table: "KpiSnapshots",
                columns: new[] { "TenantId", "MetricGroup", "MetricKey", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationalAlerts_TenantId_AlertType_Status",
                table: "OperationalAlerts",
                columns: new[] { "TenantId", "AlertType", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationalAlerts_TenantId_Status_Severity_CreatedAt",
                table: "OperationalAlerts",
                columns: new[] { "TenantId", "Status", "Severity", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationalAlerts_TraceId",
                table: "OperationalAlerts",
                column: "TraceId");

            migrationBuilder.CreateIndex(
                name: "IX_TraceLogs_TenantId_CreatedAt",
                table: "TraceLogs",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TraceLogs_TraceId_CreatedAt",
                table: "TraceLogs",
                columns: new[] { "TraceId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityTimeline");

            migrationBuilder.DropTable(
                name: "KpiSnapshots");

            migrationBuilder.DropTable(
                name: "OperationalAlerts");

            migrationBuilder.DropTable(
                name: "TraceLogs");
        }
    }
}
