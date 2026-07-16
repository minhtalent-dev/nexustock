using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexustock.Modules.LocalAgent.Migrations
{
    /// <inheritdoc />
    public partial class AddStationNameToAgentPairingCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StationName",
                table: "AgentPairingCodes",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StationName",
                table: "AgentPairingCodes");
        }
    }
}
