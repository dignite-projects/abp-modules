using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dignite.FileExplorer.Web.Host.Migrations
{
    /// <inheritdoc />
    public partial class FileContentHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_FeFileDescriptors_TenantId_ContainerName_Md5",
                table: "FeFileDescriptors",
                columns: new[] { "TenantId", "ContainerName", "Md5" },
                unique: true,
                filter: "Md5 <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FeFileDescriptors_TenantId_ContainerName_Md5",
                table: "FeFileDescriptors");
        }
    }
}
