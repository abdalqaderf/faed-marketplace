using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Faed.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddListingHiddenByAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HiddenByAdmin",
                table: "Listings",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HiddenByAdmin",
                table: "Listings");
        }
    }
}
