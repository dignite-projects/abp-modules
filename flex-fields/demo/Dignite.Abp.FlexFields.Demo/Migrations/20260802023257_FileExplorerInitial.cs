using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dignite.Abp.FlexFields.Demo.Migrations
{
    /// <inheritdoc />
    public partial class FileExplorerInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FeDirectoryDescriptors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ContainerName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ParentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ExtraProperties = table.Column<string>(type: "TEXT", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatorId = table.Column<Guid>(type: "TEXT", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeDirectoryDescriptors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeDirectoryDescriptors_FeDirectoryDescriptors_ParentId",
                        column: x => x.ParentId,
                        principalTable: "FeDirectoryDescriptors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FeFileDescriptors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ContainerName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    BlobName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Size = table.Column<long>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    MimeType = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Md5 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ReferBlobName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    CellName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    DirectoryId = table.Column<Guid>(type: "TEXT", nullable: true),
                    EntityId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatorId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DeleterId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ExtraProperties = table.Column<string>(type: "TEXT", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeFileDescriptors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeFileDescriptors_FeDirectoryDescriptors_DirectoryId",
                        column: x => x.DirectoryId,
                        principalTable: "FeDirectoryDescriptors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FeDirectoryDescriptors_ParentId",
                table: "FeDirectoryDescriptors",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_FeDirectoryDescriptors_TenantId_ContainerName_CreatorId_ParentId",
                table: "FeDirectoryDescriptors",
                columns: new[] { "TenantId", "ContainerName", "CreatorId", "ParentId" });

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
                name: "IX_FeFileDescriptors_TenantId_ContainerName_CreationTime_CreatorId_DirectoryId",
                table: "FeFileDescriptors",
                columns: new[] { "TenantId", "ContainerName", "CreationTime", "CreatorId", "DirectoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_FeFileDescriptors_TenantId_ContainerName_EntityId",
                table: "FeFileDescriptors",
                columns: new[] { "TenantId", "ContainerName", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_FeFileDescriptors_TenantId_ContainerName_Md5",
                table: "FeFileDescriptors",
                columns: new[] { "TenantId", "ContainerName", "Md5" },
                unique: true,
                filter: "TenantId IS NOT NULL AND Md5 <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_FeFileDescriptors_TenantId_ContainerName_ReferBlobName",
                table: "FeFileDescriptors",
                columns: new[] { "TenantId", "ContainerName", "ReferBlobName" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FeFileDescriptors");

            migrationBuilder.DropTable(
                name: "FeDirectoryDescriptors");
        }
    }
}
