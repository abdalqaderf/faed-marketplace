using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Faed.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Categories_Categories_ParentCategoryId",
                        column: x => x.ParentCategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConditionGrades",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConditionGrades", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DiscountReasons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscountReasons", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    MonthlyPriceJod = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    ActiveListingQuota = table.Column<int>(type: "int", nullable: false),
                    HasFeaturedPlacement = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPlans", x => x.Id);
                    table.CheckConstraint("CK_SubscriptionPlans_Positive", "[MonthlyPriceJod] >= 0 AND [ActiveListingQuota] >= 1");
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                name: "Listings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MerchantProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConditionGradeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    ReferencePrice = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    RetailPrice = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    ReturnPolicyText = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    WarrantyType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    WarrantyMonths = table.Column<int>(type: "int", nullable: true),
                    IncludedItemsText = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    MissingItemsText = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HiddenByAdmin = table.Column<bool>(type: "bit", nullable: false),
                    HiddenBySubscriptionLapse = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Listings", x => x.Id);
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
                name: "MerchantSubscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MerchantProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubscriptionPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    StartsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PaymentReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ActivatedByAdminId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MerchantSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MerchantSubscriptions_MerchantProfiles_MerchantProfileId",
                        column: x => x.MerchantProfileId,
                        principalTable: "MerchantProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MerchantSubscriptions_SubscriptionPlans_SubscriptionPlanId",
                        column: x => x.SubscriptionPlanId,
                        principalTable: "SubscriptionPlans",
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
                name: "Orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reference = table.Column<string>(type: "nchar(6)", fixedLength: true, maxLength: 6, nullable: false),
                    BuyerUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    MerchantProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    FulfillmentType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    MerchantLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.CheckConstraint("CK_Orders_NonNegativeMoney", "[Subtotal] >= 0 AND [Total] >= 0");
                    table.ForeignKey(
                        name: "FK_Orders_AspNetUsers_BuyerUserId",
                        column: x => x.BuyerUserId,
                        principalTable: "AspNetUsers",
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

            migrationBuilder.CreateTable(
                name: "Reviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewedMerchantProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewerUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Rating = table.Column<int>(type: "int", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reviews", x => x.Id);
                    table.CheckConstraint("CK_Reviews_RatingRange", "[Rating] >= 1 AND [Rating] <= 5");
                    table.ForeignKey(
                        name: "FK_Reviews_AspNetUsers_ReviewerUserId",
                        column: x => x.ReviewerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reviews_MerchantProfiles_ReviewedMerchantProfileId",
                        column: x => x.ReviewedMerchantProfileId,
                        principalTable: "MerchantProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reviews_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
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
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_ParentCategoryId_SortOrder",
                table: "Categories",
                columns: new[] { "ParentCategoryId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Slug",
                table: "Categories",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConditionGrades_Code",
                table: "ConditionGrades",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiscountReasons_Code",
                table: "DiscountReasons",
                column: "Code",
                unique: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_MerchantLocations_MerchantProfileId_IsActive",
                table: "MerchantLocations",
                columns: new[] { "MerchantProfileId", "IsActive" });

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
                name: "IX_MerchantSubscriptions_MerchantProfileId",
                table: "MerchantSubscriptions",
                column: "MerchantProfileId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MerchantSubscriptions_Status_ExpiresAtUtc",
                table: "MerchantSubscriptions",
                columns: new[] { "Status", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MerchantSubscriptions_SubscriptionPlanId",
                table: "MerchantSubscriptions",
                column: "SubscriptionPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_MerchantVerificationDocuments_MerchantProfileId",
                table: "MerchantVerificationDocuments",
                column: "MerchantProfileId");

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
                name: "IX_Orders_MerchantLocationId",
                table: "Orders",
                column: "MerchantLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_MerchantProfileId_Status",
                table: "Orders",
                columns: new[] { "MerchantProfileId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Reference",
                table: "Orders",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Status_ReservationExpiresAtUtc",
                table: "Orders",
                columns: new[] { "Status", "ReservationExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_OrderId_Unique",
                table: "Reviews",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_ReviewedMerchantProfileId_CreatedAtUtc",
                table: "Reviews",
                columns: new[] { "ReviewedMerchantProfileId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_ReviewerUserId",
                table: "Reviews",
                column: "ReviewerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPlans_Code",
                table: "SubscriptionPlans",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

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
                name: "MerchantSubscriptions");

            migrationBuilder.DropTable(
                name: "MerchantVerificationDocuments");

            migrationBuilder.DropTable(
                name: "OrderItems");

            migrationBuilder.DropTable(
                name: "Reviews");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "DiscountReasons");

            migrationBuilder.DropTable(
                name: "ListingOptionValues");

            migrationBuilder.DropTable(
                name: "SubscriptionPlans");

            migrationBuilder.DropTable(
                name: "ListingVariants");

            migrationBuilder.DropTable(
                name: "Orders");

            migrationBuilder.DropTable(
                name: "ListingOptions");

            migrationBuilder.DropTable(
                name: "MerchantLocations");

            migrationBuilder.DropTable(
                name: "Listings");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "ConditionGrades");

            migrationBuilder.DropTable(
                name: "MerchantProfiles");

            migrationBuilder.DropTable(
                name: "AspNetUsers");
        }
    }
}
