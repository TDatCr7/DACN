using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CinemaS.Migrations
{
    /// <inheritdoc />
    public partial class AddLastPointRecalculatedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "Last_Point_Recalculated_At",
                schema: "dbo",
                table: "Users",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Last_Point_Recalculated_At",
                schema: "dbo",
                table: "Users");
        }
    }
}
