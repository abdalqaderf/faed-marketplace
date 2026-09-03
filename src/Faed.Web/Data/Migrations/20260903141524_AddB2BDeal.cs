using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Faed.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddB2BDeal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "B2BDeals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    B2BNegotiationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcceptedRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SellingMerchantProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BuyingMerchantProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    FulfillmentType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ShipmentReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AcceptedUnitPriceSnapshot = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    ShippingCostSnapshot = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    SubtotalSnapshot = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    TotalSnapshot = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    ReservationExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StatusReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_B2BDeals", x => x.Id);
                    table.CheckConstraint("CK_B2BDeals_NonNegativeMoney", "[AcceptedUnitPriceSnapshot] >= 0 AND [SubtotalSnapshot] >= 0 AND [TotalSnapshot] >= 0 AND ([ShippingCostSnapshot] IS NULL OR [ShippingCostSnapshot] >= 0)");
                    table.ForeignKey(
                        name: "FK_B2BDeals_B2BNegotiations_B2BNegotiationId",
                        column: x => x.B2BNegotiationId,
                        principalTable: "B2BNegotiations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_B2BDeals_B2BOfferRevisions_AcceptedRevisionId",
                        column: x => x.AcceptedRevisionId,
                        principalTable: "B2BOfferRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_B2BDeals_MerchantProfiles_BuyingMerchantProfileId",
                        column: x => x.BuyingMerchantProfileId,
                        principalTable: "MerchantProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_B2BDeals_MerchantProfiles_SellingMerchantProfileId",
                        column: x => x.SellingMerchantProfileId,
                        principalTable: "MerchantProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "B2BDealLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    B2BDealId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ListingVariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPriceSnapshot = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    LineTotalSnapshot = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    VariantSnapshot = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_B2BDealLines", x => x.Id);
                    table.CheckConstraint("CK_B2BDealLines_PositiveQuantityAndMoney", "[Quantity] > 0 AND [UnitPriceSnapshot] >= 0 AND [LineTotalSnapshot] >= 0");
                    table.ForeignKey(
                        name: "FK_B2BDealLines_B2BDeals_B2BDealId",
                        column: x => x.B2BDealId,
                        principalTable: "B2BDeals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_B2BDealLines_ListingVariants_ListingVariantId",
                        column: x => x.ListingVariantId,
                        principalTable: "ListingVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_B2BDealLines_B2BDealId_ListingVariantId",
                table: "B2BDealLines",
                columns: new[] { "B2BDealId", "ListingVariantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_B2BDealLines_ListingVariantId",
                table: "B2BDealLines",
                column: "ListingVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_B2BDeals_AcceptedRevisionId",
                table: "B2BDeals",
                column: "AcceptedRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_B2BDeals_B2BNegotiationId",
                table: "B2BDeals",
                column: "B2BNegotiationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_B2BDeals_BuyingMerchantProfileId_Status",
                table: "B2BDeals",
                columns: new[] { "BuyingMerchantProfileId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_B2BDeals_SellingMerchantProfileId_Status",
                table: "B2BDeals",
                columns: new[] { "SellingMerchantProfileId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_B2BDeals_Status_ReservationExpiresAtUtc",
                table: "B2BDeals",
                columns: new[] { "Status", "ReservationExpiresAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "B2BDealLines");

            migrationBuilder.DropTable(
                name: "B2BDeals");
        }
    }
}
