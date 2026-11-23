using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dignite.Abp.BlobManagement.Host.Migrations
{
    /// <inheritdoc />
    public partial class Restored_Soft_Delete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BlobDirectoryDescriptors_NameKey",
                table: "BlobDirectoryDescriptors");

            migrationBuilder.DropColumn(
                name: "NameKey",
                table: "BlobDirectoryDescriptors");

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.AddColumn<string>(
                name: "NameKey",
                table: "BlobDirectoryDescriptors",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_BlobDirectoryDescriptors_NameKey",
                table: "BlobDirectoryDescriptors",
                column: "NameKey",
                unique: true);
        }
    }
}
