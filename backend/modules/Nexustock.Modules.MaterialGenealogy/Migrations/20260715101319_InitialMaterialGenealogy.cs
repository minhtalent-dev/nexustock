using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexustock.Modules.MaterialGenealogy.Migrations
{
    /// <inheritdoc />
    public partial class InitialMaterialGenealogy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "genealogy");

            migrationBuilder.CreateTable(
                name: "genealogy_events",
                schema: "genealogy",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "text", nullable: false),
                    LotId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_genealogy_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "lot_relations",
                schema: "genealogy",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentLotId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildLotId = table.Column<Guid>(type: "uuid", nullable: false),
                    RelationType = table.Column<string>(type: "text", nullable: false),
                    QtyTransferred = table.Column<decimal>(type: "numeric", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lot_relations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_genealogy_events_TenantId_LotId",
                schema: "genealogy",
                table: "genealogy_events",
                columns: new[] { "TenantId", "LotId" });

            migrationBuilder.CreateIndex(
                name: "IX_lot_relations_TenantId_ChildLotId",
                schema: "genealogy",
                table: "lot_relations",
                columns: new[] { "TenantId", "ChildLotId" });

            migrationBuilder.CreateIndex(
                name: "IX_lot_relations_TenantId_ParentLotId",
                schema: "genealogy",
                table: "lot_relations",
                columns: new[] { "TenantId", "ParentLotId" });

            migrationBuilder.CreateIndex(
                name: "IX_lot_relations_TenantId_ParentLotId_ChildLotId",
                schema: "genealogy",
                table: "lot_relations",
                columns: new[] { "TenantId", "ParentLotId", "ChildLotId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "genealogy_events",
                schema: "genealogy");

            migrationBuilder.DropTable(
                name: "lot_relations",
                schema: "genealogy");
        }
    }
}
