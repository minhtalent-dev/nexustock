using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexustock.Modules.Rules.Migrations
{
    /// <inheritdoc />
    public partial class AddRuleEngineTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rule_execution_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rule_set_id = table.Column<Guid>(type: "uuid", nullable: true),
                    rule_type_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    input_context_json = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    matched = table.Column<bool>(type: "boolean", nullable: false),
                    result_action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    details = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rule_execution_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rule_sets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    active_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    active_to = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rule_sets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rule_actions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rule_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    action_parameters = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rule_actions", x => x.id);
                    table.ForeignKey(
                        name: "FK_rule_actions_rule_sets_rule_set_id",
                        column: x => x.rule_set_id,
                        principalTable: "rule_sets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rule_conditions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rule_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    field = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    @operator = table.Column<string>(name: "operator", type: "character varying(20)", maxLength: 20, nullable: false),
                    value = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rule_conditions", x => x.id);
                    table.ForeignKey(
                        name: "FK_rule_conditions_rule_sets_rule_set_id",
                        column: x => x.rule_set_id,
                        principalTable: "rule_sets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_rule_actions_tenant_ruleset",
                table: "rule_actions",
                columns: new[] { "tenant_id", "rule_set_id" });

            migrationBuilder.CreateIndex(
                name: "IX_rule_actions_rule_set_id",
                table: "rule_actions",
                column: "rule_set_id");

            migrationBuilder.CreateIndex(
                name: "idx_rule_conditions_tenant_ruleset",
                table: "rule_conditions",
                columns: new[] { "tenant_id", "rule_set_id" });

            migrationBuilder.CreateIndex(
                name: "IX_rule_conditions_rule_set_id",
                table: "rule_conditions",
                column: "rule_set_id");

            migrationBuilder.CreateIndex(
                name: "idx_rule_logs_tenant_ruleset",
                table: "rule_execution_logs",
                columns: new[] { "tenant_id", "rule_set_id" });

            migrationBuilder.CreateIndex(
                name: "idx_rule_logs_tenant_ruletype",
                table: "rule_execution_logs",
                columns: new[] { "tenant_id", "rule_type_code" });

            migrationBuilder.CreateIndex(
                name: "idx_rule_sets_tenant_type",
                table: "rule_sets",
                columns: new[] { "tenant_id", "type" });

            migrationBuilder.CreateIndex(
                name: "uq_rule_sets_tenant_code",
                table: "rule_sets",
                columns: new[] { "tenant_id", "code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rule_actions");

            migrationBuilder.DropTable(
                name: "rule_conditions");

            migrationBuilder.DropTable(
                name: "rule_execution_logs");

            migrationBuilder.DropTable(
                name: "rule_sets");
        }
    }
}
