using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexustock.Modules.Files.Migrations
{
    /// <inheritdoc />
    public partial class AddAttachmentThumbnailLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ThumbnailKey",
                schema: "files",
                table: "file_pending_uploads",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ObjectsPurgedAt",
                schema: "files",
                table: "file_attachments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ThumbnailKey",
                schema: "files",
                table: "file_attachments",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ThumbnailKey",
                schema: "files",
                table: "file_pending_uploads");

            migrationBuilder.DropColumn(
                name: "ObjectsPurgedAt",
                schema: "files",
                table: "file_attachments");

            migrationBuilder.DropColumn(
                name: "ThumbnailKey",
                schema: "files",
                table: "file_attachments");
        }
    }
}
