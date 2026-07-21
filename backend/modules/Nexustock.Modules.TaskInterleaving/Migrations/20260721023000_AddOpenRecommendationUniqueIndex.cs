using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexustock.Modules.TaskInterleaving.Migrations
{
    /// <inheritdoc />
    public partial class AddOpenRecommendationUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "SelectedScore",
                schema: "task_interleaving",
                table: "task_recommendations",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "uq_recommendations_tenant_user_open",
                schema: "task_interleaving",
                table: "task_recommendations",
                columns: new[] { "TenantId", "UserId" },
                unique: true,
                filter: "\"Status\" = 'Open'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_recommendations_tenant_user_open",
                schema: "task_interleaving",
                table: "task_recommendations");

            migrationBuilder.AlterColumn<decimal>(
                name: "SelectedScore",
                schema: "task_interleaving",
                table: "task_recommendations",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)",
                oldPrecision: 18,
                oldScale: 4,
                oldNullable: true);
        }
    }
}
