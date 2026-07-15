using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexustock.Modules.MasterData.Migrations
{
    /// <inheritdoc />
    public partial class AddIsSerialTrackedToProduct : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_serial_tracked",
                table: "products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "reason_codes",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000051"),
                column: "created_at",
                value: new DateTimeOffset(new DateTime(2026, 7, 15, 3, 42, 0, 989, DateTimeKind.Unspecified).AddTicks(2960), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "reason_codes",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000052"),
                column: "created_at",
                value: new DateTimeOffset(new DateTime(2026, 7, 15, 3, 42, 0, 989, DateTimeKind.Unspecified).AddTicks(2964), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "reason_codes",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000053"),
                column: "created_at",
                value: new DateTimeOffset(new DateTime(2026, 7, 15, 3, 42, 0, 989, DateTimeKind.Unspecified).AddTicks(2966), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "storage_locations",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000041"),
                column: "created_at",
                value: new DateTimeOffset(new DateTime(2026, 7, 15, 3, 42, 0, 989, DateTimeKind.Unspecified).AddTicks(2933), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "storage_locations",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000042"),
                column: "created_at",
                value: new DateTimeOffset(new DateTime(2026, 7, 15, 3, 42, 0, 989, DateTimeKind.Unspecified).AddTicks(2940), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "storage_locations",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000043"),
                column: "created_at",
                value: new DateTimeOffset(new DateTime(2026, 7, 15, 3, 42, 0, 989, DateTimeKind.Unspecified).AddTicks(2942), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "storage_locations",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000044"),
                column: "created_at",
                value: new DateTimeOffset(new DateTime(2026, 7, 15, 3, 42, 0, 989, DateTimeKind.Unspecified).AddTicks(2944), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "storage_locations",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000045"),
                column: "created_at",
                value: new DateTimeOffset(new DateTime(2026, 7, 15, 3, 42, 0, 989, DateTimeKind.Unspecified).AddTicks(2947), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "storage_zones",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000031"),
                column: "created_at",
                value: new DateTimeOffset(new DateTime(2026, 7, 15, 3, 42, 0, 989, DateTimeKind.Unspecified).AddTicks(2892), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "storage_zones",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000032"),
                column: "created_at",
                value: new DateTimeOffset(new DateTime(2026, 7, 15, 3, 42, 0, 989, DateTimeKind.Unspecified).AddTicks(2895), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "storage_zones",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000033"),
                column: "created_at",
                value: new DateTimeOffset(new DateTime(2026, 7, 15, 3, 42, 0, 989, DateTimeKind.Unspecified).AddTicks(2897), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "uoms",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"),
                column: "created_at",
                value: new DateTimeOffset(new DateTime(2026, 7, 15, 3, 42, 0, 989, DateTimeKind.Unspecified).AddTicks(2864), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "uoms",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"),
                column: "created_at",
                value: new DateTimeOffset(new DateTime(2026, 7, 15, 3, 42, 0, 989, DateTimeKind.Unspecified).AddTicks(2866), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "uoms",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000013"),
                column: "created_at",
                value: new DateTimeOffset(new DateTime(2026, 7, 15, 3, 42, 0, 989, DateTimeKind.Unspecified).AddTicks(2868), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "warehouses",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000021"),
                column: "created_at",
                value: new DateTimeOffset(new DateTime(2026, 7, 15, 3, 42, 0, 989, DateTimeKind.Unspecified).AddTicks(2881), new TimeSpan(0, 0, 0, 0, 0)));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_serial_tracked",
                table: "products");

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

            migrationBuilder.UpdateData(
                table: "reason_codes",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000053"),
                column: "created_at",
                value: new DateTimeOffset(new DateTime(2026, 7, 11, 9, 25, 2, 714, DateTimeKind.Unspecified).AddTicks(2546), new TimeSpan(0, 0, 0, 0, 0)));

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
    }
}
