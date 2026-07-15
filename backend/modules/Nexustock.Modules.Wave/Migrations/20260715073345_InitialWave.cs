using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexustock.Modules.Wave.Migrations
{
    /// <inheritdoc />
    public partial class InitialWave : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "wave");

            migrationBuilder.CreateTable(
                name: "picking_waves",
                schema: "wave",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WaveNo = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    RowVersion = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_picking_waves", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "wave_pick_tasks",
                schema: "wave",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WaveId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromLocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    QtyToPick = table.Column<decimal>(type: "numeric", nullable: false),
                    QtyPicked = table.Column<decimal>(type: "numeric", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wave_pick_tasks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "wave_items",
                schema: "wave",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WaveId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShipmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShipmentItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    QtyExpected = table.Column<decimal>(type: "numeric", nullable: false),
                    QtyAllocated = table.Column<decimal>(type: "numeric", nullable: false),
                    QtyPicked = table.Column<decimal>(type: "numeric", nullable: false),
                    QtySorted = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wave_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_wave_items_picking_waves_WaveId",
                        column: x => x.WaveId,
                        principalSchema: "wave",
                        principalTable: "picking_waves",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_picking_waves_WaveNo",
                schema: "wave",
                table: "picking_waves",
                column: "WaveNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_wave_items_WaveId",
                schema: "wave",
                table: "wave_items",
                column: "WaveId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "wave_items",
                schema: "wave");

            migrationBuilder.DropTable(
                name: "wave_pick_tasks",
                schema: "wave");

            migrationBuilder.DropTable(
                name: "picking_waves",
                schema: "wave");
        }
    }
}
