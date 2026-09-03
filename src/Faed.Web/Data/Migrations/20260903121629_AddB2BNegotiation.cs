using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Faed.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddB2BNegotiation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "B2BNegotiations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ListingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SellingMerchantProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BuyingMerchantProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CurrentRevisionNumber = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_B2BNegotiations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_B2BNegotiations_Listings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "Listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_B2BNegotiations_MerchantProfiles_BuyingMerchantProfileId",
                        column: x => x.BuyingMerchantProfileId,
                        principalTable: "MerchantProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_B2BNegotiations_MerchantProfiles_SellingMerchantProfileId",
                        column: x => x.SellingMerchantProfileId,
                        principalTable: "MerchantProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "B2BOfferRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    B2BNegotiationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevisionNumber = table.Column<int>(type: "int", nullable: false),
                    ProposedByMerchantProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProposedUnitPrice = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    ProposedTotal = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    OfferExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_B2BOfferRevisions", x => x.Id);
                    table.CheckConstraint("CK_B2BOfferRevisions_NonNegativeMoney", "[ProposedUnitPrice] >= 0 AND [ProposedTotal] >= 0");
                    table.ForeignKey(
                        name: "FK_B2BOfferRevisions_B2BNegotiations_B2BNegotiationId",
                        column: x => x.B2BNegotiationId,
                        principalTable: "B2BNegotiations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_B2BOfferRevisions_MerchantProfiles_ProposedByMerchantProfileId",
                        column: x => x.ProposedByMerchantProfileId,
                        principalTable: "MerchantProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "B2BOfferLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    B2BOfferRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ListingVariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_B2BOfferLines", x => x.Id);
                    table.CheckConstraint("CK_B2BOfferLines_PositiveQuantity", "[Quantity] > 0");
                    table.ForeignKey(
                        name: "FK_B2BOfferLines_B2BOfferRevisions_B2BOfferRevisionId",
                        column: x => x.B2BOfferRevisionId,
                        principalTable: "B2BOfferRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_B2BOfferLines_ListingVariants_ListingVariantId",
                        column: x => x.ListingVariantId,
                        principalTable: "ListingVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_B2BNegotiations_BuyingMerchantProfileId_Status",
                table: "B2BNegotiations",
                columns: new[] { "BuyingMerchantProfileId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_B2BNegotiations_ListingId",
                table: "B2BNegotiations",
                column: "ListingId");

            migrationBuilder.CreateIndex(
                name: "IX_B2BNegotiations_SellingMerchantProfileId_Status",
                table: "B2BNegotiations",
                columns: new[] { "SellingMerchantProfileId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_B2BOfferLines_B2BOfferRevisionId_ListingVariantId",
                table: "B2BOfferLines",
                columns: new[] { "B2BOfferRevisionId", "ListingVariantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_B2BOfferLines_ListingVariantId",
                table: "B2BOfferLines",
                column: "ListingVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_B2BOfferRevisions_B2BNegotiationId_RevisionNumber",
                table: "B2BOfferRevisions",
                columns: new[] { "B2BNegotiationId", "RevisionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_B2BOfferRevisions_ProposedByMerchantProfileId",
                table: "B2BOfferRevisions",
                column: "ProposedByMerchantProfileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "B2BOfferLines");

            migrationBuilder.DropTable(
                name: "B2BOfferRevisions");

            migrationBuilder.DropTable(
                name: "B2BNegotiations");
        }
    }
}
