using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CinemaS.Migrations
{
    /// <inheritdoc />
    public partial class _2100h09112025 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Is_Active",
                schema: "dbo",
                table: "Seats",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Is_Active",
                schema: "dbo",
                table: "Seats");
        }
    }
}
