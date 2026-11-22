using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dignite.Abp.BlobManagement.Host.Migrations
{
    /// <inheritdoc />
    public partial class Removed_Soft_Delete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The rows that were only marked deleted go first. Their BLOBs are deleted already, so once the mark is
            // dropped they would come back as files without bytes; and a deleted directory holds no name key, which
            // every directory has from here on.
            migrationBuilder.Sql("DELETE FROM BlobFileDescriptors WHERE IsDeleted = 1;");
            migrationBuilder.Sql("DELETE FROM BlobDirectoryDescriptors WHERE IsDeleted = 1;");

            migrationBuilder.DropIndex(
                name: "IX_BlobFileDescriptors_TenantId_ContainerName_DirectoryId",
                table: "BlobFileDescriptors");

            migrationBuilder.DropIndex(
                name: "IX_BlobFileDescriptors_TenantId_ContainerName_Name",
                table: "BlobFileDescriptors");

            migrationBuilder.DropColumn(
                name: "DeleterId",
                table: "BlobFileDescriptors");

            migrationBuilder.DropColumn(
                name: "DeletionTime",
                table: "BlobFileDescriptors");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "BlobFileDescriptors");

            migrationBuilder.DropColumn(
                name: "DeleterId",
                table: "BlobDirectoryDescriptors");

            migrationBuilder.DropColumn(
                name: "DeletionTime",
                table: "BlobDirectoryDescriptors");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "BlobDirectoryDescriptors");

            migrationBuilder.AlterColumn<string>(
                name: "NameKey",
                table: "BlobDirectoryDescriptors",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BlobFileDescriptors_TenantId_ContainerName_DirectoryId_Name",
                table: "BlobFileDescriptors",
                columns: new[] { "TenantId", "ContainerName", "DirectoryId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_BlobFileDescriptors_TenantId_IsPending_CreationTime",
                table: "BlobFileDescriptors",
                columns: new[] { "TenantId", "IsPending", "CreationTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BlobFileDescriptors_TenantId_ContainerName_DirectoryId_Name",
                table: "BlobFileDescriptors");

            migrationBuilder.DropIndex(
                name: "IX_BlobFileDescriptors_TenantId_IsPending_CreationTime",
                table: "BlobFileDescriptors");

            migrationBuilder.AddColumn<Guid>(
                name: "DeleterId",
                table: "BlobFileDescriptors",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletionTime",
                table: "BlobFileDescriptors",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "BlobFileDescriptors",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "NameKey",
                table: "BlobDirectoryDescriptors",
                type: "TEXT",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 64);

            migrationBuilder.AddColumn<Guid>(
                name: "DeleterId",
                table: "BlobDirectoryDescriptors",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletionTime",
                table: "BlobDirectoryDescriptors",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "BlobDirectoryDescriptors",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_BlobFileDescriptors_TenantId_ContainerName_DirectoryId",
                table: "BlobFileDescriptors",
                columns: new[] { "TenantId", "ContainerName", "DirectoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_BlobFileDescriptors_TenantId_ContainerName_Name",
                table: "BlobFileDescriptors",
                columns: new[] { "TenantId", "ContainerName", "Name" });
        }
    }
}
