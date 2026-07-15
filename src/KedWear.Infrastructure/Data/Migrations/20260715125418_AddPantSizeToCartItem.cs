using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KedWear.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPantSizeToCartItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PantSize",
                table: "CartItems",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PantSize",
                table: "CartItems");
        }
    }
}
