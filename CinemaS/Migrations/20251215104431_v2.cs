using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CinemaS.Migrations
{
    /// <inheritdoc />
    public partial class v2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Original_Total",
                schema: "dbo",
                table: "Invoices",
                type: "money",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Original_Total",
                schema: "dbo",
                table: "Invoices");
        }
    }
}
