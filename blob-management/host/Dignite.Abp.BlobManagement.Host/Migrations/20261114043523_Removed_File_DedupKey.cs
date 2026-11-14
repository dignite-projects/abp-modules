using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dignite.Abp.BlobManagement.Host.Migrations
{
    /// <inheritdoc />
    public partial class Removed_File_DedupKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BlobFileDescriptors_DedupKey",
                table: "BlobFileDescriptors");

            migrationBuilder.DropIndex(
                name: "IX_BlobFileDescriptors_TenantId_ContainerName_Hash",
                table: "BlobFileDescriptors");

            migrationBuilder.DropColumn(
                name: "DedupKey",
                table: "BlobFileDescriptors");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DedupKey",
                table: "BlobFileDescriptors",
                type: "TEXT",
                maxLength: 162,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BlobFileDescriptors_DedupKey",
                table: "BlobFileDescriptors",
                column: "DedupKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BlobFileDescriptors_TenantId_ContainerName_Hash",
                table: "BlobFileDescriptors",
                columns: new[] { "TenantId", "ContainerName", "Hash" });
        }
    }
}
