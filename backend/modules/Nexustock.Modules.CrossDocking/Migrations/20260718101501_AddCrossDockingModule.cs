using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexustock.Modules.CrossDocking.Migrations
{
    /// <inheritdoc />
    public partial class AddCrossDockingModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CrossDockCandidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    LotId = table.Column<Guid>(type: "uuid", nullable: false),
                    InboundOrderItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    WaveItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    QtyAvailable = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    QtyRequested = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    QtyMatched = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    MatchScore = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RejectedReason = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrossDockCandidates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrossDockEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Actor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: true),
                    TraceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrossDockEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CrossDockEvents_CrossDockCandidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "CrossDockCandidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CrossDockCandidates_CreatedAt",
                table: "CrossDockCandidates",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CrossDockCandidates_LotId",
                table: "CrossDockCandidates",
                column: "LotId");

            migrationBuilder.CreateIndex(
                name: "IX_CrossDockCandidates_Status",
                table: "CrossDockCandidates",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CrossDockCandidates_TenantId",
                table: "CrossDockCandidates",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CrossDockCandidates_WaveItemId",
                table: "CrossDockCandidates",
                column: "WaveItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CrossDockEvents_CandidateId",
                table: "CrossDockEvents",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_CrossDockEvents_OccurredAt",
                table: "CrossDockEvents",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_CrossDockEvents_TenantId_CandidateId",
                table: "CrossDockEvents",
                columns: new[] { "TenantId", "CandidateId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CrossDockEvents");

            migrationBuilder.DropTable(
                name: "CrossDockCandidates");
        }
    }
}
