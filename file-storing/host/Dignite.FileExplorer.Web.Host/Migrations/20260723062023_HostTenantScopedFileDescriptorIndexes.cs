using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dignite.FileExplorer.Web.Host.Migrations
{
    /// <inheritdoc />
    public partial class HostTenantScopedFileDescriptorIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FeFileDescriptors_TenantId_ContainerName_BlobName",
                table: "FeFileDescriptors");

            migrationBuilder.DropIndex(
                name: "IX_FeFileDescriptors_TenantId_ContainerName_Md5",
                table: "FeFileDescriptors");

            migrationBuilder.CreateIndex(
                name: "IX_FeFileDescriptors_ContainerName_BlobName",
                table: "FeFileDescriptors",
                columns: new[] { "ContainerName", "BlobName" },
                unique: true,
                filter: "TenantId IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FeFileDescriptors_ContainerName_Md5",
                table: "FeFileDescriptors",
                columns: new[] { "ContainerName", "Md5" },
                unique: true,
                filter: "TenantId IS NULL AND Md5 <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_FeFileDescriptors_DirectoryId",
                table: "FeFileDescriptors",
                column: "DirectoryId");

            migrationBuilder.CreateIndex(
                name: "IX_FeFileDescriptors_TenantId_ContainerName_BlobName",
                table: "FeFileDescriptors",
                columns: new[] { "TenantId", "ContainerName", "BlobName" },
                unique: true,
                filter: "TenantId IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FeFileDescriptors_TenantId_ContainerName_Md5",
                table: "FeFileDescriptors",
                columns: new[] { "TenantId", "ContainerName", "Md5" },
                unique: true,
                filter: "TenantId IS NOT NULL AND Md5 <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_FeDirectoryDescriptors_ParentId",
                table: "FeDirectoryDescriptors",
                column: "ParentId");

            migrationBuilder.AddForeignKey(
                name: "FK_FeDirectoryDescriptors_FeDirectoryDescriptors_ParentId",
                table: "FeDirectoryDescriptors",
                column: "ParentId",
                principalTable: "FeDirectoryDescriptors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FeFileDescriptors_FeDirectoryDescriptors_DirectoryId",
                table: "FeFileDescriptors",
                column: "DirectoryId",
                principalTable: "FeDirectoryDescriptors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FeDirectoryDescriptors_FeDirectoryDescriptors_ParentId",
                table: "FeDirectoryDescriptors");

            migrationBuilder.DropForeignKey(
                name: "FK_FeFileDescriptors_FeDirectoryDescriptors_DirectoryId",
                table: "FeFileDescriptors");

            migrationBuilder.DropIndex(
                name: "IX_FeFileDescriptors_ContainerName_BlobName",
                table: "FeFileDescriptors");

            migrationBuilder.DropIndex(
                name: "IX_FeFileDescriptors_ContainerName_Md5",
                table: "FeFileDescriptors");

            migrationBuilder.DropIndex(
                name: "IX_FeFileDescriptors_DirectoryId",
                table: "FeFileDescriptors");

            migrationBuilder.DropIndex(
                name: "IX_FeFileDescriptors_TenantId_ContainerName_BlobName",
                table: "FeFileDescriptors");

            migrationBuilder.DropIndex(
                name: "IX_FeFileDescriptors_TenantId_ContainerName_Md5",
                table: "FeFileDescriptors");

            migrationBuilder.DropIndex(
                name: "IX_FeDirectoryDescriptors_ParentId",
                table: "FeDirectoryDescriptors");

            migrationBuilder.CreateIndex(
                name: "IX_FeFileDescriptors_TenantId_ContainerName_BlobName",
                table: "FeFileDescriptors",
                columns: new[] { "TenantId", "ContainerName", "BlobName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FeFileDescriptors_TenantId_ContainerName_Md5",
                table: "FeFileDescriptors",
                columns: new[] { "TenantId", "ContainerName", "Md5" },
                unique: true,
                filter: "Md5 <> ''");
        }
    }
}
