using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Faed.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class HardenDisputeInvariants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActiveTransactionKey",
                table: "Disputes",
                type: "nvarchar(48)",
                maxLength: 48,
                nullable: true);

            // Backfill the filtered-unique key for any dispute already Open/UnderReview so the
            // one-active-dispute-per-transaction invariant covers existing rows too. The
            // expression matches Dispute.ActiveKeyFor: "O:"/"D:" + Guid.ToString("N").
            migrationBuilder.Sql(@"
UPDATE [Disputes]
SET [ActiveTransactionKey] =
    CASE
        WHEN [OrderId] IS NOT NULL THEN 'O:' + LOWER(REPLACE(CONVERT(varchar(36), [OrderId]), '-', ''))
        ELSE 'D:' + LOWER(REPLACE(CONVERT(varchar(36), [B2BDealId]), '-', ''))
    END
WHERE [Status] IN (N'Open', N'UnderReview');");

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "AdminActionLogs",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Disputes_ActiveTransactionKey_Unique",
                table: "Disputes",
                column: "ActiveTransactionKey",
                unique: true,
                filter: "[ActiveTransactionKey] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Disputes_ActiveTransactionKey_Unique",
                table: "Disputes");

            migrationBuilder.DropColumn(
                name: "ActiveTransactionKey",
                table: "Disputes");

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "AdminActionLogs",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);
        }
    }
}
