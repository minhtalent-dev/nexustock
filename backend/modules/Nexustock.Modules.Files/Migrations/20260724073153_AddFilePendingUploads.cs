using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexustock.Modules.Files.Migrations
{
    /// <inheritdoc />
    public partial class AddFilePendingUploads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PendingUploadId",
                schema: "files",
                table: "file_attachments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "file_pending_uploads",
                schema: "files",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    LegacyUrl = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    BoundAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PurgedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AttachmentId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_file_pending_uploads", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_file_pending_uploads_attachment",
                schema: "files",
                table: "file_pending_uploads",
                column: "AttachmentId",
                unique: true,
                filter: "\"AttachmentId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_file_pending_uploads_tenant_key",
                schema: "files",
                table: "file_pending_uploads",
                columns: new[] { "TenantId", "StorageKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_file_pending_uploads_tenant_status_exp",
                schema: "files",
                table: "file_pending_uploads",
                columns: new[] { "TenantId", "Status", "ExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "file_pending_uploads",
                schema: "files");

            migrationBuilder.DropColumn(
                name: "PendingUploadId",
                schema: "files",
                table: "file_attachments");
        }
    }
}
