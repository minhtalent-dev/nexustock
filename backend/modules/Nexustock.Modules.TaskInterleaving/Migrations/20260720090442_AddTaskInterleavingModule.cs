using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexustock.Modules.TaskInterleaving.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskInterleavingModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "task_interleaving");

            migrationBuilder.CreateTable(
                name: "task_recommendation_candidates",
                schema: "task_interleaving",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecommendationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ZoneId = table.Column<Guid>(type: "uuid", nullable: true),
                    TaskStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    AgeSeconds = table.Column<int>(type: "integer", nullable: false),
                    DistanceScore = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    AgeScore = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    PriorityScore = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ContinuityScore = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    PenaltyScore = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    TotalScore = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Explanation = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_recommendation_candidates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "task_recommendations",
                schema: "task_interleaving",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShiftId = table.Column<Guid>(type: "uuid", nullable: true),
                    LaborSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceTaskType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SourceTaskId = table.Column<Guid>(type: "uuid", nullable: true),
                    CurrentLocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CurrentZoneId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SelectedTaskType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SelectedTaskId = table.Column<Guid>(type: "uuid", nullable: true),
                    SelectedScore = table.Column<decimal>(type: "numeric", nullable: true),
                    ReasonCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DecisionNote = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    AcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TraceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_recommendations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_candidates_recommendation_score",
                schema: "task_interleaving",
                table: "task_recommendation_candidates",
                columns: new[] { "RecommendationId", "TotalScore" });

            migrationBuilder.CreateIndex(
                name: "idx_recommendations_tenant_expires",
                schema: "task_interleaving",
                table: "task_recommendations",
                columns: new[] { "TenantId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "idx_recommendations_tenant_selected_task",
                schema: "task_interleaving",
                table: "task_recommendations",
                columns: new[] { "TenantId", "SelectedTaskType", "SelectedTaskId" });

            migrationBuilder.CreateIndex(
                name: "idx_recommendations_tenant_user_status_created",
                schema: "task_interleaving",
                table: "task_recommendations",
                columns: new[] { "TenantId", "UserId", "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "task_recommendation_candidates",
                schema: "task_interleaving");

            migrationBuilder.DropTable(
                name: "task_recommendations",
                schema: "task_interleaving");
        }
    }
}
