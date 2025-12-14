using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CinemaS.Migrations
{
    /// <inheritdoc />
    public partial class v1 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "Is_Aisle",
                table: "Seats",  // Thay "YourTableName" bằng tên bảng thực tế của bạn
                nullable: false,
                defaultValue: false,   // Giá trị mặc định là False
                oldClrType: typeof(bool),
                oldNullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "Is_Aisle",
                table: "Seats",
                nullable: true,
                oldClrType: typeof(bool),
                oldDefaultValue: false);
        }
    }
}
