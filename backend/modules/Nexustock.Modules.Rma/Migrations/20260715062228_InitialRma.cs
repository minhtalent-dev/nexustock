using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexustock.Modules.Rma.Migrations
{
    /// <inheritdoc />
    public partial class InitialRma : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "rma");

            migrationBuilder.CreateTable(
                name: "rma_qc_results",
                schema: "rma",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RmaItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    QcStatus = table.Column<string>(type: "text", nullable: false),
                    Disposition = table.Column<string>(type: "text", nullable: false),
                    Qty = table.Column<decimal>(type: "numeric", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rma_qc_results", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "rma_requests",
                schema: "rma",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RmaNo = table.Column<string>(type: "text", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferenceNo = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    RowVersion = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rma_requests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "rma_items",
                schema: "rma",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RmaId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    QtyExpected = table.Column<decimal>(type: "numeric", nullable: false),
                    QtyReceived = table.Column<decimal>(type: "numeric", nullable: false),
                    SerialNo = table.Column<string>(type: "text", nullable: true),
                    ReasonCode = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rma_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_rma_items_rma_requests_RmaId",
                        column: x => x.RmaId,
                        principalSchema: "rma",
                        principalTable: "rma_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_rma_items_RmaId",
                schema: "rma",
                table: "rma_items",
                column: "RmaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rma_items",
                schema: "rma");

            migrationBuilder.DropTable(
                name: "rma_qc_results",
                schema: "rma");

            migrationBuilder.DropTable(
                name: "rma_requests",
                schema: "rma");
        }
    }
}
