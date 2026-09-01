using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Faed.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddListingsAndInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Listings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MerchantProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BrandId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConditionGradeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    ReferencePrice = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    RetailPrice = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    WholesaleIndicativeUnitPrice = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    WholesaleMinQuantity = table.Column<int>(type: "int", nullable: true),
                    AllowB2C = table.Column<bool>(type: "bit", nullable: false),
                    AllowB2B = table.Column<bool>(type: "bit", nullable: false),
                    AllowMixedVariantB2B = table.Column<bool>(type: "bit", nullable: false),
                    ReturnPolicyText = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    WarrantyText = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IncludedItemsText = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    MissingItemsText = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Listings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Listings_Brands_BrandId",
                        column: x => x.BrandId,
                        principalTable: "Brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Listings_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Listings_ConditionGrades_ConditionGradeId",
                        column: x => x.ConditionGradeId,
                        principalTable: "ConditionGrades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Listings_MerchantProfiles_MerchantProfileId",
                        column: x => x.MerchantProfileId,
                        principalTable: "MerchantProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ListingDiscountReasons",
                columns: table => new
                {
                    ListingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DiscountReasonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListingDiscountReasons", x => new { x.ListingId, x.DiscountReasonId });
                    table.ForeignKey(
                        name: "FK_ListingDiscountReasons_DiscountReasons_DiscountReasonId",
                        column: x => x.DiscountReasonId,
                        principalTable: "DiscountReasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ListingDiscountReasons_Listings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "Listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ListingMedia",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ListingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MediaType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    StorageObjectKey = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    AltText = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListingMedia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ListingMedia_Listings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "Listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ListingModerations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ListingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmittedByMerchantProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReasonForReview = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ReviewedByAdminId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ReviewNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListingModerations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ListingModerations_Listings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "Listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ListingOptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ListingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListingOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ListingOptions_Listings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "Listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ListingReferencePriceEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ListingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvidenceType = table.Column<string>(type: "nvarchar(48)", maxLength: 48, nullable: false),
                    ReferenceUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    StorageObjectKey = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    OriginalFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListingReferencePriceEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ListingReferencePriceEvidence_Listings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "Listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ListingVariants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ListingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sku = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    OptionCombinationKey = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    InitialQuantity = table.Column<int>(type: "int", nullable: false),
                    AvailableQuantity = table.Column<int>(type: "int", nullable: false),
                    ReservedQuantity = table.Column<int>(type: "int", nullable: false),
                    SoldQuantity = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListingVariants", x => x.Id);
                    table.CheckConstraint("CK_ListingVariants_Quantities_NonNegative", "[InitialQuantity] >= 0 AND [AvailableQuantity] >= 0 AND [ReservedQuantity] >= 0 AND [SoldQuantity] >= 0");
                    table.ForeignKey(
                        name: "FK_ListingVariants_Listings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "Listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ListingOptionValues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ListingOptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListingOptionValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ListingOptionValues_ListingOptions_ListingOptionId",
                        column: x => x.ListingOptionId,
                        principalTable: "ListingOptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InventoryAdjustments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ListingVariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChangedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    AdjustmentType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    QuantityDelta = table.Column<int>(type: "int", nullable: false),
                    QuantityBefore = table.Column<int>(type: "int", nullable: false),
                    QuantityAfter = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryAdjustments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryAdjustments_ListingVariants_ListingVariantId",
                        column: x => x.ListingVariantId,
                        principalTable: "ListingVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ListingVariantOptionValues",
                columns: table => new
                {
                    ListingVariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ListingOptionValueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListingVariantOptionValues", x => new { x.ListingVariantId, x.ListingOptionValueId });
                    table.ForeignKey(
                        name: "FK_ListingVariantOptionValues_ListingOptionValues_ListingOptionValueId",
                        column: x => x.ListingOptionValueId,
                        principalTable: "ListingOptionValues",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ListingVariantOptionValues_ListingVariants_ListingVariantId",
                        column: x => x.ListingVariantId,
                        principalTable: "ListingVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryAdjustments_ListingVariantId_CreatedAtUtc",
                table: "InventoryAdjustments",
                columns: new[] { "ListingVariantId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ListingDiscountReasons_DiscountReasonId",
                table: "ListingDiscountReasons",
                column: "DiscountReasonId");

            migrationBuilder.CreateIndex(
                name: "IX_ListingMedia_ListingId_MediaType_SortOrder",
                table: "ListingMedia",
                columns: new[] { "ListingId", "MediaType", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ListingModerations_ListingId",
                table: "ListingModerations",
                column: "ListingId");

            migrationBuilder.CreateIndex(
                name: "IX_ListingModerations_Status_SubmittedAtUtc",
                table: "ListingModerations",
                columns: new[] { "Status", "SubmittedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ListingOptions_ListingId_Name",
                table: "ListingOptions",
                columns: new[] { "ListingId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ListingOptionValues_ListingOptionId_Value",
                table: "ListingOptionValues",
                columns: new[] { "ListingOptionId", "Value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ListingReferencePriceEvidence_ListingId",
                table: "ListingReferencePriceEvidence",
                column: "ListingId");

            migrationBuilder.CreateIndex(
                name: "IX_Listings_BrandId",
                table: "Listings",
                column: "BrandId");

            migrationBuilder.CreateIndex(
                name: "IX_Listings_CategoryId",
                table: "Listings",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Listings_ConditionGradeId",
                table: "Listings",
                column: "ConditionGradeId");

            migrationBuilder.CreateIndex(
                name: "IX_Listings_MerchantProfileId_Status",
                table: "Listings",
                columns: new[] { "MerchantProfileId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Listings_Slug",
                table: "Listings",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Listings_Status_CategoryId_PublishedAtUtc",
                table: "Listings",
                columns: new[] { "Status", "CategoryId", "PublishedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ListingVariantOptionValues_ListingOptionValueId",
                table: "ListingVariantOptionValues",
                column: "ListingOptionValueId");

            migrationBuilder.CreateIndex(
                name: "IX_ListingVariants_ListingId_IsActive",
                table: "ListingVariants",
                columns: new[] { "ListingId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ListingVariants_ListingId_OptionCombinationKey",
                table: "ListingVariants",
                columns: new[] { "ListingId", "OptionCombinationKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ListingVariants_ListingId_Sku",
                table: "ListingVariants",
                columns: new[] { "ListingId", "Sku" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InventoryAdjustments");

            migrationBuilder.DropTable(
                name: "ListingDiscountReasons");

            migrationBuilder.DropTable(
                name: "ListingMedia");

            migrationBuilder.DropTable(
                name: "ListingModerations");

            migrationBuilder.DropTable(
                name: "ListingReferencePriceEvidence");

            migrationBuilder.DropTable(
                name: "ListingVariantOptionValues");

            migrationBuilder.DropTable(
                name: "ListingOptionValues");

            migrationBuilder.DropTable(
                name: "ListingVariants");

            migrationBuilder.DropTable(
                name: "ListingOptions");

            migrationBuilder.DropTable(
                name: "Listings");
        }
    }
}
