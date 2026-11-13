using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dignite.Abp.BlobManagement.Host.Migrations
{
    /// <inheritdoc />
    public partial class Removed_Directory_Restriction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsRestricted",
                table: "BlobDirectoryDescriptors");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRestricted",
                table: "BlobDirectoryDescriptors",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }
    }
}
