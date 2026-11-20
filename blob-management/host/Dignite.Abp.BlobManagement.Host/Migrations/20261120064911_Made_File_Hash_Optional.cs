using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dignite.Abp.BlobManagement.Host.Migrations
{
    /// <inheritdoc />
    public partial class Made_File_Hash_Optional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Hash",
                table: "BlobFileDescriptors",
                type: "TEXT",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 64);

            // The column held the upper-case hex of every upload's hash; it now holds the base64 a caller sent to be
            // verified. The rows of a demo database keep the old values (SQLite rebuilds the table after custom SQL,
            // so they cannot be cleared here); nothing reads them.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Hash",
                table: "BlobFileDescriptors",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 64,
                oldNullable: true);
        }
    }
}
