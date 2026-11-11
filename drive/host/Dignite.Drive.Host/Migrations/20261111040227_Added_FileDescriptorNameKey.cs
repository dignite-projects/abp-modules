using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dignite.Drive.Host.Migrations
{
    /// <inheritdoc />
    public partial class Added_FileDescriptorNameKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Directories are Drive Pro's: the open table has no DirectoryId.
            migrationBuilder.DropColumn(
                name: "DirectoryId",
                table: "DriveFileDescriptors");

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifierId",
                table: "DriveFileDescriptors",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModificationTime",
                table: "DriveFileDescriptors",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameKey",
                table: "DriveFileDescriptors",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DriveFileDescriptors_NameKey",
                table: "DriveFileDescriptors",
                column: "NameKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DriveFileDescriptors_NameKey",
                table: "DriveFileDescriptors");

            migrationBuilder.DropColumn(
                name: "LastModificationTime",
                table: "DriveFileDescriptors");

            migrationBuilder.DropColumn(
                name: "NameKey",
                table: "DriveFileDescriptors");

            migrationBuilder.DropColumn(
                name: "LastModifierId",
                table: "DriveFileDescriptors");

            migrationBuilder.AddColumn<Guid>(
                name: "DirectoryId",
                table: "DriveFileDescriptors",
                type: "TEXT",
                nullable: true);
        }
    }
}
