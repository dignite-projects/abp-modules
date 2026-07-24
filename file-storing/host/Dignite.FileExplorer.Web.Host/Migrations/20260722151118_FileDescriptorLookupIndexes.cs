using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dignite.FileExplorer.Web.Host.Migrations
{
    /// <inheritdoc />
    public partial class FileDescriptorLookupIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_FeFileDescriptors_TenantId_ContainerName_EntityId",
                table: "FeFileDescriptors",
                columns: new[] { "TenantId", "ContainerName", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_FeFileDescriptors_TenantId_ContainerName_ReferBlobName",
                table: "FeFileDescriptors",
                columns: new[] { "TenantId", "ContainerName", "ReferBlobName" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FeFileDescriptors_TenantId_ContainerName_EntityId",
                table: "FeFileDescriptors");

            migrationBuilder.DropIndex(
                name: "IX_FeFileDescriptors_TenantId_ContainerName_ReferBlobName",
                table: "FeFileDescriptors");
        }
    }
}
