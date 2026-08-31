using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Faed.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMerchantVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdminActionLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdminUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ActionType = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: false),
                    TargetType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TargetId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminActionLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MerchantProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    BusinessName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PublicSlug = table.Column<string>(type: "nvarchar(220)", maxLength: 220, nullable: false),
                    ContactEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ContactPhone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    VerificationStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedByAdminId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MerchantProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MerchantProfiles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MerchantVerificationDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MerchantProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    StorageObjectKey = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MerchantVerificationDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MerchantVerificationDocuments_MerchantProfiles_MerchantProfileId",
                        column: x => x.MerchantProfileId,
                        principalTable: "MerchantProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminActionLogs_CreatedAtUtc",
                table: "AdminActionLogs",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AdminActionLogs_TargetType_TargetId",
                table: "AdminActionLogs",
                columns: new[] { "TargetType", "TargetId" });

            migrationBuilder.CreateIndex(
                name: "IX_MerchantProfiles_PublicSlug",
                table: "MerchantProfiles",
                column: "PublicSlug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MerchantProfiles_UserId",
                table: "MerchantProfiles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MerchantProfiles_VerificationStatus",
                table: "MerchantProfiles",
                column: "VerificationStatus");

            migrationBuilder.CreateIndex(
                name: "IX_MerchantVerificationDocuments_MerchantProfileId",
                table: "MerchantVerificationDocuments",
                column: "MerchantProfileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminActionLogs");

            migrationBuilder.DropTable(
                name: "MerchantVerificationDocuments");

            migrationBuilder.DropTable(
                name: "MerchantProfiles");
        }
    }
}
