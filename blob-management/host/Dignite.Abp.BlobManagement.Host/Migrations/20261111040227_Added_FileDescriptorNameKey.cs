using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dignite.Abp.BlobManagement.Host.Migrations
{
    /// <inheritdoc />
    public partial class Added_FileDescriptorNameKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Directories are Blob Management Pro's: the open table has no DirectoryId.
            migrationBuilder.DropColumn(
                name: "DirectoryId",
                table: "BlobManagementFileDescriptors");

            migrationBuilder.AddColumn<Guid>(
                name: "LastModifierId",
                table: "BlobManagementFileDescriptors",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModificationTime",
                table: "BlobManagementFileDescriptors",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameKey",
                table: "BlobManagementFileDescriptors",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BlobManagementFileDescriptors_NameKey",
                table: "BlobManagementFileDescriptors",
                column: "NameKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BlobManagementFileDescriptors_NameKey",
                table: "BlobManagementFileDescriptors");

            migrationBuilder.DropColumn(
                name: "LastModificationTime",
                table: "BlobManagementFileDescriptors");

            migrationBuilder.DropColumn(
                name: "NameKey",
                table: "BlobManagementFileDescriptors");

            migrationBuilder.DropColumn(
                name: "LastModifierId",
                table: "BlobManagementFileDescriptors");

            migrationBuilder.AddColumn<Guid>(
                name: "DirectoryId",
                table: "BlobManagementFileDescriptors",
                type: "TEXT",
                nullable: true);
        }
    }
}
