using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexustock.Modules.MasterData.Migrations
{
    /// <inheritdoc />
    public partial class SeedLabelReprintReasonCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "reason_codes",
                columns: new[] { "id", "code", "created_at", "created_by", "description", "is_active", "reason_type", "row_version", "tenant_id", "updated_at", "updated_by" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000002251"), "LABEL_DAMAGED", new DateTimeOffset(new DateTime(2026, 7, 17, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SYSTEM", "Tem bị hỏng", true, "LABEL_REPRINT", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("00000000-0000-0000-0000-000000002252"), "PRINTER_JAM", new DateTimeOffset(new DateTime(2026, 7, 17, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SYSTEM", "Máy in bị kẹt", true, "LABEL_REPRINT", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("00000000-0000-0000-0000-000000002253"), "WRONG_LABEL_APPLIED", new DateTimeOffset(new DateTime(2026, 7, 17, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SYSTEM", "Dán sai tem", true, "LABEL_REPRINT", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("00000000-0000-0000-0000-000000002254"), "SUPERVISOR_APPROVED", new DateTimeOffset(new DateTime(2026, 7, 17, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "SYSTEM", "Quản lý phê duyệt in lại", true, "LABEL_REPRINT", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(table: "reason_codes", keyColumn: "id", keyValue: new Guid("00000000-0000-0000-0000-000000002251"));
            migrationBuilder.DeleteData(table: "reason_codes", keyColumn: "id", keyValue: new Guid("00000000-0000-0000-0000-000000002252"));
            migrationBuilder.DeleteData(table: "reason_codes", keyColumn: "id", keyValue: new Guid("00000000-0000-0000-0000-000000002253"));
            migrationBuilder.DeleteData(table: "reason_codes", keyColumn: "id", keyValue: new Guid("00000000-0000-0000-0000-000000002254"));
        }
    }
}
