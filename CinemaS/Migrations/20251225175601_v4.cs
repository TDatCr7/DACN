using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CinemaS.Migrations
{
    /// <inheritdoc />
    public partial class v4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                schema: "dbo",
                table: "Membership_Rank",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Max_Point",
                schema: "dbo",
                table: "Membership_Rank",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Only_Normal_Seat",
                schema: "dbo",
                table: "Membership_Rank",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Point_Multiplier",
                schema: "dbo",
                table: "Membership_Rank",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Snack_Discount_Percent",
                schema: "dbo",
                table: "Membership_Rank",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Ticket_Discount_Percent",
                schema: "dbo",
                table: "Membership_Rank",
                type: "decimal(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                schema: "dbo",
                table: "Membership_Rank");

            migrationBuilder.DropColumn(
                name: "Max_Point",
                schema: "dbo",
                table: "Membership_Rank");

            migrationBuilder.DropColumn(
                name: "Only_Normal_Seat",
                schema: "dbo",
                table: "Membership_Rank");

            migrationBuilder.DropColumn(
                name: "Point_Multiplier",
                schema: "dbo",
                table: "Membership_Rank");

            migrationBuilder.DropColumn(
                name: "Snack_Discount_Percent",
                schema: "dbo",
                table: "Membership_Rank");

            migrationBuilder.DropColumn(
                name: "Ticket_Discount_Percent",
                schema: "dbo",
                table: "Membership_Rank");
        }
    }
}
