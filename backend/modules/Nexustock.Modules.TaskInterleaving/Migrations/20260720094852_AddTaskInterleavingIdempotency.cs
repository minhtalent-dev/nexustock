using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexustock.Modules.TaskInterleaving.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskInterleavingIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AcceptIdempotencyKey",
                schema: "task_interleaving",
                table: "task_recommendations",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_recommendations_tenant_idempotency",
                schema: "task_interleaving",
                table: "task_recommendations",
                columns: new[] { "TenantId", "AcceptIdempotencyKey" },
                unique: true,
                filter: "\"AcceptIdempotencyKey\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_recommendations_tenant_idempotency",
                schema: "task_interleaving",
                table: "task_recommendations");

            migrationBuilder.DropColumn(
                name: "AcceptIdempotencyKey",
                schema: "task_interleaving",
                table: "task_recommendations");
        }
    }
}
