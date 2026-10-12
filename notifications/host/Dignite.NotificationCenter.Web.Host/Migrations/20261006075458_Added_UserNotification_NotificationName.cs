using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dignite.NotificationCenter.Web.Host.Migrations
{
    /// <inheritdoc />
    public partial class Added_UserNotification_NotificationName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NotificationName",
                table: "NotifUserNotifications",
                type: "TEXT",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            // Backfill the demo database's existing inbox rows from their notification.
            migrationBuilder.Sql(
                "UPDATE NotifUserNotifications SET NotificationName = " +
                "(SELECT n.NotificationName FROM NotifNotifications n WHERE n.Id = NotifUserNotifications.NotificationId) " +
                "WHERE EXISTS (SELECT 1 FROM NotifNotifications n WHERE n.Id = NotifUserNotifications.NotificationId);");

            migrationBuilder.CreateIndex(
                name: "IX_NotifUserNotifications_TenantId_UserId_NotificationName_State_CreationTime",
                table: "NotifUserNotifications",
                columns: new[] { "TenantId", "UserId", "NotificationName", "State", "CreationTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NotifUserNotifications_TenantId_UserId_NotificationName_State_CreationTime",
                table: "NotifUserNotifications");

            migrationBuilder.DropColumn(
                name: "NotificationName",
                table: "NotifUserNotifications");
        }
    }
}
