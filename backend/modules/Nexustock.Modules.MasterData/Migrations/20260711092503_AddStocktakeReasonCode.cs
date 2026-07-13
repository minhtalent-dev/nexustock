using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Nexustock.Modules.MasterData.Migrations
{
    /// <inheritdoc />
    public partial class AddStocktakeReasonCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.UpdateData(
                table: "reason_codes",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000051"),
                column: "created_at",
                value: new DateTimeOffset(new DateTime(2026, 7, 11, 9, 25, 2, 714, DateTimeKind.Unspecified).AddTicks(2540), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "reason_codes",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000052"),
                column: "created_at",
                value: new DateTimeOffset(new DateTime(2026, 7, 11, 9, 25, 2, 714, DateTimeKind.Unspecified).AddTicks(2544), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.InsertData(
                table: "reason_codes",
                columns: new[] { "id", "code", "created_at", "created_by", "description", "is_active", "reason_type", "row_version", "tenant_id", "updated_at", "updated_by" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000053"), "STOCKTAKE", new DateTimeOffset(new DateTime(2026, 7, 11, 9, 25, 2, 714, DateTimeKind.Unspecified).AddTicks(2546), new TimeSpan(0, 0, 0, 0, 0)), "SYSTEM", "Khóa vị trí phục vụ kiểm kê chu kỳ", true, "HOLD", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null });

            migrationBuilder.UpdateData(
                table: "storage_locations",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000041"),
                column: "created_at",
                value: new DateTimeOffset(new DateTime(2026, 7, 11, 9, 25, 2, 714, DateTimeKind.Unspecified).AddTicks(2507), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "storage_locations",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000042"),
                column: "created_at",
                value: new DateTimeOffset(new DateTime(2026, 7, 11, 9, 25, 2, 714, DateTimeKind.Unspecified).AddTicks(2515), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "storage_locations",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000043"),
                column: "created_at",
                value: new DateTimeOffset(new DateTime(2026, 7, 11, 9, 25, 2, 714, DateTimeKind.Unspecified).AddTicks(2519), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "storage_locations",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000044"),
                column: "created_at",
                value: new DateTimeOffset(new DateTime(2026, 7, 11, 9, 25, 2, 714, DateTimeKind.Unspecified).AddTicks(2522), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "storage_locations",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000045"),
                column: "created_at",
                value: new DateTimeOffset(new DateTime(2026, 7, 11, 9, 25, 2, 714, DateTimeKind.Unspecified).AddTicks(2525), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "storage_zones",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000031"),
                column: "created_at",
                value: new DateTimeOffset(new DateTime(2026, 7, 11, 9, 25, 2, 714, DateTimeKind.Unspecified).AddTicks(2482), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "storage_zones",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000032"),
                column: "created_at",
                value: new DateTimeOffset(new DateTime(2026, 7, 11, 9, 25, 2, 714, DateTimeKind.Unspecified).AddTicks(2487), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "storage_zones",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000033"),
                column: "created_at",
                value: new DateTimeOffset(new DateTime(2026, 7, 11, 9, 25, 2, 714, DateTimeKind.Unspecified).AddTicks(2489), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "uoms",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"),
                column: "created_at",
                value: new DateTimeOffset(new DateTime(2026, 7, 11, 9, 25, 2, 714, DateTimeKind.Unspecified).AddTicks(2443), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "uoms",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"),
                column: "created_at",
                value: new DateTimeOffset(new DateTime(2026, 7, 11, 9, 25, 2, 714, DateTimeKind.Unspecified).AddTicks(2446), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "uoms",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000013"),
                column: "created_at",
                value: new DateTimeOffset(new DateTime(2026, 7, 11, 9, 25, 2, 714, DateTimeKind.Unspecified).AddTicks(2448), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "warehouses",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000021"),
                column: "created_at",
                value: new DateTimeOffset(new DateTime(2026, 7, 11, 9, 25, 2, 714, DateTimeKind.Unspecified).AddTicks(2466), new TimeSpan(0, 0, 0, 0, 0)));

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.DeleteData(
                table: "reason_codes",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000053"));

            migrationBuilder.UpdateData(
                table: "reason_codes",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000051"),
                column: "created_at",
                value: new DateTimeOffset(new DateTime(2026, 7, 2, 2, 33, 33, 742, DateTimeKind.Unspecified).AddTicks(4482), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "reason_codes",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000052"),
                column: "created_at",
                value: new DateTimeOffset(new DateTime(2026, 7, 2, 2, 33, 33, 742, DateTimeKind.Unspecified).AddTicks(4485), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "storage_locations",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000041"),
                column: "created_at",
                value: new DateTimeOffset(new DateTime(2026, 7, 2, 2, 33, 33, 742, DateTimeKind.Unspecified).AddTicks(4426), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "storage_locations",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000042"),
                column: "created_at",
                value: new DateTimeOffset(new DateTime(2026, 7, 2, 2, 33, 33, 742, DateTimeKind.Unspecified).AddTicks(4435), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "storage_locations",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000043"),
                column: "created_at",
                value: new DateTimeOffset(new DateTime(2026, 7, 2, 2, 33, 33, 742, DateTimeKind.Unspecified).AddTicks(4463), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "storage_locations",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000044"),
                column: "created_at",
                value: new DateTimeOffset(new DateTime(2026, 7, 2, 2, 33, 33, 742, DateTimeKind.Unspecified).AddTicks(4466), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "storage_locations",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000045"),
                column: "created_at",
                value: new DateTimeOffset(new DateTime(2026, 7, 2, 2, 33, 33, 742, DateTimeKind.Unspecified).AddTicks(4468), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "storage_zones",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000031"),
                column: "created_at",
                value: new DateTimeOffset(new DateTime(2026, 7, 2, 2, 33, 33, 742, DateTimeKind.Unspecified).AddTicks(4409), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "storage_zones",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000032"),
                column: "created_at",
                value: new DateTimeOffset(new DateTime(2026, 7, 2, 2, 33, 33, 742, DateTimeKind.Unspecified).AddTicks(4412), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "storage_zones",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000033"),
                column: "created_at",
                value: new DateTimeOffset(new DateTime(2026, 7, 2, 2, 33, 33, 742, DateTimeKind.Unspecified).AddTicks(4414), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "uoms",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"),
                column: "created_at",
                value: new DateTimeOffset(new DateTime(2026, 7, 2, 2, 33, 33, 742, DateTimeKind.Unspecified).AddTicks(4380), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "uoms",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"),
                column: "created_at",
                value: new DateTimeOffset(new DateTime(2026, 7, 2, 2, 33, 33, 742, DateTimeKind.Unspecified).AddTicks(4382), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "uoms",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000013"),
                column: "created_at",
                value: new DateTimeOffset(new DateTime(2026, 7, 2, 2, 33, 33, 742, DateTimeKind.Unspecified).AddTicks(4384), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "warehouses",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000021"),
                column: "created_at",
                value: new DateTimeOffset(new DateTime(2026, 7, 2, 2, 33, 33, 742, DateTimeKind.Unspecified).AddTicks(4396), new TimeSpan(0, 0, 0, 0, 0)));
        }
    }
}
