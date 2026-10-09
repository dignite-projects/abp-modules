using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dignite.NotificationCenter.Web.Host.Migrations
{
    /// <inheritdoc />
    public partial class Added_NotificationDefinitionStore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NotifDefinitionGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ExtraProperties = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotifDefinitionGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotifDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GroupName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    PermissionName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    FeatureName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    ExtraProperties = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotifDefinitions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotifDefinitionGroups_Name",
                table: "NotifDefinitionGroups",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotifDefinitions_GroupName",
                table: "NotifDefinitions",
                column: "GroupName");

            migrationBuilder.CreateIndex(
                name: "IX_NotifDefinitions_Name",
                table: "NotifDefinitions",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotifDefinitionGroups");

            migrationBuilder.DropTable(
                name: "NotifDefinitions");
        }
    }
}
