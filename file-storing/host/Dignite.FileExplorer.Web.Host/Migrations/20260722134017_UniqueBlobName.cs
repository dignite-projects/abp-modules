using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dignite.FileExplorer.Web.Host.Migrations
{
    /// <inheritdoc />
    public partial class UniqueBlobName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FeFileDescriptors_TenantId_ContainerName_BlobName",
                table: "FeFileDescriptors");

            migrationBuilder.CreateIndex(
                name: "IX_FeFileDescriptors_TenantId_ContainerName_BlobName",
                table: "FeFileDescriptors",
                columns: new[] { "TenantId", "ContainerName", "BlobName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FeFileDescriptors_TenantId_ContainerName_BlobName",
                table: "FeFileDescriptors");

            migrationBuilder.CreateIndex(
                name: "IX_FeFileDescriptors_TenantId_ContainerName_BlobName",
                table: "FeFileDescriptors",
                columns: new[] { "TenantId", "ContainerName", "BlobName" });
        }
    }
}
