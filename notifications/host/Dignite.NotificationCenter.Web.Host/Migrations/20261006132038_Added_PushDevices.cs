using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dignite.NotificationCenter.Web.Host.Migrations
{
    /// <inheritdoc />
    public partial class Added_PushDevices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NotifPushDevices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Token = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    TokenKey = table.Column<string>(type: "TEXT", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    CultureName = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    SessionId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    CreationTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSeenTime = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotifPushDevices", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotifPushDevices_TenantId_UserId",
                table: "NotifPushDevices",
                columns: new[] { "TenantId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_NotifPushDevices_TokenKey",
                table: "NotifPushDevices",
                column: "TokenKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotifPushDevices");
        }
    }
}
