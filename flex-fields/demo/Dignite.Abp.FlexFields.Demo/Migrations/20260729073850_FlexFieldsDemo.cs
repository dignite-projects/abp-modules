using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dignite.Abp.FlexFields.Demo.Migrations
{
    /// <inheritdoc />
    public partial class FlexFieldsDemo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppProductFields",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    FieldTypeName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Configuration = table.Column<string>(type: "TEXT", nullable: false),
                    Required = table.Column<bool>(type: "INTEGER", nullable: false),
                    Searchable = table.Column<bool>(type: "INTEGER", nullable: false),
                    ExtraProperties = table.Column<string>(type: "TEXT", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppProductFields", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppProducts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    FlexFields = table.Column<string>(type: "TEXT", nullable: false),
                    ExtraProperties = table.Column<string>(type: "TEXT", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppProducts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppProductFlexFieldIndexes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FieldId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ValueType = table.Column<int>(type: "INTEGER", nullable: false),
                    StringValue = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    NumberValue = table.Column<decimal>(type: "TEXT", nullable: true),
                    DateTimeValue = table.Column<DateTime>(type: "TEXT", nullable: true),
                    BooleanValue = table.Column<bool>(type: "INTEGER", nullable: true),
                    GuidValue = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppProductFlexFieldIndexes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppProductFlexFieldIndexes_AppProducts_ProductId",
                        column: x => x.ProductId,
                        principalTable: "AppProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppProductFields_Name",
                table: "AppProductFields",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppProductFlexFieldIndexes_ProductId_FieldId",
                table: "AppProductFlexFieldIndexes",
                columns: new[] { "ProductId", "FieldId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppProductFields");

            migrationBuilder.DropTable(
                name: "AppProductFlexFieldIndexes");

            migrationBuilder.DropTable(
                name: "AppProducts");
        }
    }
}
