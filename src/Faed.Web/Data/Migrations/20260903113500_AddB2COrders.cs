using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Faed.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddB2COrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MerchantDeliveryZones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MerchantProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    DeliveryFee = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    MinimumOrderValue = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    EstimatedDeliveryText = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MerchantDeliveryZones", x => x.Id);
                    table.CheckConstraint("CK_MerchantDeliveryZones_NonNegativeMoney", "[DeliveryFee] >= 0 AND ([MinimumOrderValue] IS NULL OR [MinimumOrderValue] >= 0)");
                    table.ForeignKey(
                        name: "FK_MerchantDeliveryZones_MerchantProfiles_MerchantProfileId",
                        column: x => x.MerchantProfileId,
                        principalTable: "MerchantProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MerchantLocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MerchantProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    AddressLine = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Area = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    City = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Latitude = table.Column<double>(type: "float", nullable: true),
                    Longitude = table.Column<double>(type: "float", nullable: true),
                    PickupInstructions = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: true),
                    PickupHoursText = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MerchantLocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MerchantLocations_MerchantProfiles_MerchantProfileId",
                        column: x => x.MerchantProfileId,
                        principalTable: "MerchantProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BuyerUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    MerchantProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    FulfillmentType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    MerchantLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeliveryZoneId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeliveryFeeSnapshot = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    FulfillmentSnapshot = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    DeliveryAddressText = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: true),
                    ContactName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ContactPhone = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    BuyerNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Subtotal = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    ReservationExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StatusReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConfirmedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                    table.CheckConstraint("CK_Orders_NonNegativeMoney", "[Subtotal] >= 0 AND [Total] >= 0 AND [DeliveryFeeSnapshot] >= 0");
                    table.ForeignKey(
                        name: "FK_Orders_AspNetUsers_BuyerUserId",
                        column: x => x.BuyerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Orders_MerchantDeliveryZones_DeliveryZoneId",
                        column: x => x.DeliveryZoneId,
                        principalTable: "MerchantDeliveryZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Orders_MerchantLocations_MerchantLocationId",
                        column: x => x.MerchantLocationId,
                        principalTable: "MerchantLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Orders_MerchantProfiles_MerchantProfileId",
                        column: x => x.MerchantProfileId,
                        principalTable: "MerchantProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrderItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ListingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ListingVariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPriceSnapshot = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    LineTotalSnapshot = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    ListingTitleSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    VariantSnapshot = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ConditionGradeSnapshot = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    DiscountReasonSnapshot = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItems", x => x.Id);
                    table.CheckConstraint("CK_OrderItems_PositiveQuantityAndMoney", "[Quantity] > 0 AND [UnitPriceSnapshot] >= 0 AND [LineTotalSnapshot] >= 0");
                    table.ForeignKey(
                        name: "FK_OrderItems_ListingVariants_ListingVariantId",
                        column: x => x.ListingVariantId,
                        principalTable: "ListingVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderItems_Listings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "Listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderItems_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MerchantDeliveryZones_MerchantProfileId_IsActive",
                table: "MerchantDeliveryZones",
                columns: new[] { "MerchantProfileId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_MerchantLocations_MerchantProfileId_IsActive",
                table: "MerchantLocations",
                columns: new[] { "MerchantProfileId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_ListingId",
                table: "OrderItems",
                column: "ListingId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_ListingVariantId",
                table: "OrderItems",
                column: "ListingVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_OrderId_ListingVariantId",
                table: "OrderItems",
                columns: new[] { "OrderId", "ListingVariantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_BuyerUserId_CreatedAtUtc",
                table: "Orders",
                columns: new[] { "BuyerUserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_DeliveryZoneId",
                table: "Orders",
                column: "DeliveryZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_MerchantLocationId",
                table: "Orders",
                column: "MerchantLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_MerchantProfileId_Status",
                table: "Orders",
                columns: new[] { "MerchantProfileId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Status_ReservationExpiresAtUtc",
                table: "Orders",
                columns: new[] { "Status", "ReservationExpiresAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderItems");

            migrationBuilder.DropTable(
                name: "Orders");

            migrationBuilder.DropTable(
                name: "MerchantDeliveryZones");

            migrationBuilder.DropTable(
                name: "MerchantLocations");
        }
    }
}
