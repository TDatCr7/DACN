using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CinemaS.Migrations
{
    /// <inheritdoc />
    public partial class v1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.CreateTable(
                name: "Cinema_Types",
                schema: "dbo",
                columns: table => new
                {
                    Cinema_Type_ID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cinema_Types", x => x.Cinema_Type_ID);
                });

            migrationBuilder.CreateTable(
                name: "Genres",
                schema: "dbo",
                columns: table => new
                {
                    Genres_ID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Genres", x => x.Genres_ID);
                });

            migrationBuilder.CreateTable(
                name: "Membership_Rank",
                schema: "dbo",
                columns: table => new
                {
                    Membership_Rank_ID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RequirePoint = table.Column<int>(type: "int", nullable: true),
                    PointReturnTicket = table.Column<int>(type: "int", nullable: true),
                    PointReturnCombo = table.Column<int>(type: "int", nullable: true),
                    PriorityLevel = table.Column<int>(type: "int", nullable: true),
                    Created_At = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Updated_At = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Membership_Rank", x => x.Membership_Rank_ID);
                });

            migrationBuilder.CreateTable(
                name: "Movie_Role",
                schema: "dbo",
                columns: table => new
                {
                    Movie_Role_ID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Movie_Role", x => x.Movie_Role_ID);
                });

            migrationBuilder.CreateTable(
                name: "Participants",
                schema: "dbo",
                columns: table => new
                {
                    Participants_ID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    BirthName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    NickName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Gender = table.Column<byte>(type: "tinyint", nullable: true),
                    Nationality = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Mini_Bio = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Avatar = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Participants", x => x.Participants_ID);
                });

            migrationBuilder.CreateTable(
                name: "Payment_Methods",
                schema: "dbo",
                columns: table => new
                {
                    Payment_Method_ID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Type = table.Column<byte>(type: "tinyint", nullable: true),
                    Provider = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Created_At = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Updated_At = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payment_Methods", x => x.Payment_Method_ID);
                });

            migrationBuilder.CreateTable(
                name: "Permission",
                schema: "dbo",
                columns: table => new
                {
                    Permission_ID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Method = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Created_At = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Updated_At = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permission", x => x.Permission_ID);
                });

            migrationBuilder.CreateTable(
                name: "Provinces",
                schema: "dbo",
                columns: table => new
                {
                    Province_ID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Created_At = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Updated_At = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Provinces", x => x.Province_ID);
                });

            migrationBuilder.CreateTable(
                name: "Role",
                schema: "dbo",
                columns: table => new
                {
                    Role_ID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Role", x => x.Role_ID);
                });

            migrationBuilder.CreateTable(
                name: "Seat_Types",
                schema: "dbo",
                columns: table => new
                {
                    Seat_Type_ID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Price = table.Column<decimal>(type: "money", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Seat_Types", x => x.Seat_Type_ID);
                });

            migrationBuilder.CreateTable(
                name: "Snack_Types",
                schema: "dbo",
                columns: table => new
                {
                    Snack_Type_ID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Snack_Types", x => x.Snack_Type_ID);
                });

            migrationBuilder.CreateTable(
                name: "Status",
                schema: "dbo",
                columns: table => new
                {
                    Status_ID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Status", x => x.Status_ID);
                });

            migrationBuilder.CreateTable(
                name: "Ticket_Types",
                schema: "dbo",
                columns: table => new
                {
                    Ticket_Type_ID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Price = table.Column<decimal>(type: "money", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ticket_Types", x => x.Ticket_Type_ID);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                schema: "dbo",
                columns: table => new
                {
                    User_ID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Membership_Rank_ID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Password = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Full_Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Date_Of_Birth = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Gender = table.Column<byte>(type: "tinyint", nullable: true),
                    Save_Point = table.Column<int>(type: "int", nullable: true),
                    Facebook = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Google = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: true),
                    Created_At = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Updated_At = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.User_ID);
                    table.ForeignKey(
                        name: "FK_Users_Membership_Rank_Membership_Rank_ID",
                        column: x => x.Membership_Rank_ID,
                        principalSchema: "dbo",
                        principalTable: "Membership_Rank",
                        principalColumn: "Membership_Rank_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Movie_Theaters",
                schema: "dbo",
                columns: table => new
                {
                    Movie_Theater_ID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Hotline = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: true),
                    I_frame_Code = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Province_ID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Movie_Theaters", x => x.Movie_Theater_ID);
                    table.ForeignKey(
                        name: "FK_Movie_Theaters_Provinces_Province_ID",
                        column: x => x.Province_ID,
                        principalSchema: "dbo",
                        principalTable: "Provinces",
                        principalColumn: "Province_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Role_Permission",
                schema: "dbo",
                columns: table => new
                {
                    Permission_ID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Role_ID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Role_Permission", x => new { x.Permission_ID, x.Role_ID });
                    table.ForeignKey(
                        name: "FK_Role_Permission_Permission_Permission_ID",
                        column: x => x.Permission_ID,
                        principalSchema: "dbo",
                        principalTable: "Permission",
                        principalColumn: "Permission_ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Role_Permission_Role_Role_ID",
                        column: x => x.Role_ID,
                        principalSchema: "dbo",
                        principalTable: "Role",
                        principalColumn: "Role_ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Snacks",
                schema: "dbo",
                columns: table => new
                {
                    Snack_ID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Snack_Type_ID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Price = table.Column<decimal>(type: "money", nullable: true),
                    Image = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Snacks", x => x.Snack_ID);
                    table.ForeignKey(
                        name: "FK_Snacks_Snack_Types_Snack_Type_ID",
                        column: x => x.Snack_Type_ID,
                        principalSchema: "dbo",
                        principalTable: "Snack_Types",
                        principalColumn: "Snack_Type_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Movies",
                schema: "dbo",
                columns: table => new
                {
                    Movies_ID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Status_ID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Summary = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Detail_Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Release_Date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Duration = table.Column<int>(type: "int", nullable: true),
                    Rating = table.Column<double>(type: "float", nullable: true),
                    Age = table.Column<int>(type: "int", nullable: true),
                    Trailer_Link = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Poster_Image = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Banner_Image = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Created_At = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Updated_At = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Movies", x => x.Movies_ID);
                    table.ForeignKey(
                        name: "FK_Movies_Status_Status_ID",
                        column: x => x.Status_ID,
                        principalSchema: "dbo",
                        principalTable: "Status",
                        principalColumn: "Status_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Promotion",
                schema: "dbo",
                columns: table => new
                {
                    Promotion_ID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    User_ID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Content = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Start_Day = table.Column<DateTime>(type: "datetime2", nullable: true),
                    End_Day = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Discount = table.Column<double>(type: "float", nullable: true),
                    Status = table.Column<bool>(type: "bit", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Promotion", x => x.Promotion_ID);
                    table.ForeignKey(
                        name: "FK_Promotion_Users_User_ID",
                        column: x => x.User_ID,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "User_ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "User_Role",
                schema: "dbo",
                columns: table => new
                {
                    Role_ID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    User_ID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_User_Role", x => new { x.Role_ID, x.User_ID });
                    table.ForeignKey(
                        name: "FK_User_Role_Role_Role_ID",
                        column: x => x.Role_ID,
                        principalSchema: "dbo",
                        principalTable: "Role",
                        principalColumn: "Role_ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_User_Role_Users_User_ID",
                        column: x => x.User_ID,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "User_ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Cinema_Theaters",
                schema: "dbo",
                columns: table => new
                {
                    Cinema_Theater_ID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Cinema_Type_ID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Movie_Theater_ID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Num_Of_Rows = table.Column<int>(type: "int", nullable: true),
                    Num_Of_Columns = table.Column<int>(type: "int", nullable: true),
                    Regular_Seat_Row = table.Column<int>(type: "int", nullable: true),
                    Double_Seat_Row = table.Column<int>(type: "int", nullable: true),
                    VIP_Seat_Row = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cinema_Theaters", x => x.Cinema_Theater_ID);
                    table.ForeignKey(
                        name: "FK_Cinema_Theaters_Cinema_Types_Cinema_Type_ID",
                        column: x => x.Cinema_Type_ID,
                        principalSchema: "dbo",
                        principalTable: "Cinema_Types",
                        principalColumn: "Cinema_Type_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Cinema_Theaters_Movie_Theaters_Movie_Theater_ID",
                        column: x => x.Movie_Theater_ID,
                        principalSchema: "dbo",
                        principalTable: "Movie_Theaters",
                        principalColumn: "Movie_Theater_ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Movies_Genres",
                schema: "dbo",
                columns: table => new
                {
                    Movie_Genre_ID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Movies_ID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Genres_ID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Movies_Genres", x => new { x.Movie_Genre_ID, x.Movies_ID, x.Genres_ID });
                    table.ForeignKey(
                        name: "FK_Movies_Genres_Genres_Genres_ID",
                        column: x => x.Genres_ID,
                        principalSchema: "dbo",
                        principalTable: "Genres",
                        principalColumn: "Genres_ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Movies_Genres_Movies_Movies_ID",
                        column: x => x.Movies_ID,
                        principalSchema: "dbo",
                        principalTable: "Movies",
                        principalColumn: "Movies_ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Movies_Participants",
                schema: "dbo",
                columns: table => new
                {
                    Movie_Participant_ID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Participants_ID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Movies_ID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Movie_Role_ID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Movies_Participants", x => new { x.Movie_Participant_ID, x.Participants_ID, x.Movies_ID });
                    table.ForeignKey(
                        name: "FK_Movies_Participants_Movie_Role_Movie_Role_ID",
                        column: x => x.Movie_Role_ID,
                        principalSchema: "dbo",
                        principalTable: "Movie_Role",
                        principalColumn: "Movie_Role_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Movies_Participants_Movies_Movies_ID",
                        column: x => x.Movies_ID,
                        principalSchema: "dbo",
                        principalTable: "Movies",
                        principalColumn: "Movies_ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Movies_Participants_Participants_Participants_ID",
                        column: x => x.Participants_ID,
                        principalSchema: "dbo",
                        principalTable: "Participants",
                        principalColumn: "Participants_ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Invoices",
                schema: "dbo",
                columns: table => new
                {
                    Invoice_ID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Staff_ID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Promotion_ID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Customer_ID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: true),
                    Total_Ticket = table.Column<int>(type: "int", nullable: true),
                    PaymentMethod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Total_Price = table.Column<decimal>(type: "money", nullable: true),
                    Created_At = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Updated_At = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Payment_Method_ID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.Invoice_ID);
                    table.ForeignKey(
                        name: "FK_Invoices_Payment_Methods_Payment_Method_ID",
                        column: x => x.Payment_Method_ID,
                        principalSchema: "dbo",
                        principalTable: "Payment_Methods",
                        principalColumn: "Payment_Method_ID");
                    table.ForeignKey(
                        name: "FK_Invoices_Promotion_Promotion_ID",
                        column: x => x.Promotion_ID,
                        principalSchema: "dbo",
                        principalTable: "Promotion",
                        principalColumn: "Promotion_ID");
                    table.ForeignKey(
                        name: "FK_Invoices_Users_Customer_ID",
                        column: x => x.Customer_ID,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "User_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Invoices_Users_Staff_ID",
                        column: x => x.Staff_ID,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "User_ID");
                });

            migrationBuilder.CreateTable(
                name: "Seats",
                schema: "dbo",
                columns: table => new
                {
                    Seat_ID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Seat_Type_ID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Cinema_Theater_ID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    RowIndex = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    ColumnIndex = table.Column<int>(type: "int", nullable: true),
                    Label = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Seats", x => x.Seat_ID);
                    table.ForeignKey(
                        name: "FK_Seats_Cinema_Theaters_Cinema_Theater_ID",
                        column: x => x.Cinema_Theater_ID,
                        principalSchema: "dbo",
                        principalTable: "Cinema_Theaters",
                        principalColumn: "Cinema_Theater_ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Seats_Seat_Types_Seat_Type_ID",
                        column: x => x.Seat_Type_ID,
                        principalSchema: "dbo",
                        principalTable: "Seat_Types",
                        principalColumn: "Seat_Type_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Show_Times",
                schema: "dbo",
                columns: table => new
                {
                    Show_Time_ID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Movies_ID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Cinema_Theater_ID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    OriginPrice = table.Column<int>(type: "int", nullable: true),
                    Show_Date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Start_Time = table.Column<DateTime>(type: "datetime2", nullable: true),
                    End_Time = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Total_Cinema = table.Column<int>(type: "int", nullable: true),
                    Created_At = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Updated_At = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Show_Times", x => x.Show_Time_ID);
                    table.ForeignKey(
                        name: "FK_Show_Times_Cinema_Theaters_Cinema_Theater_ID",
                        column: x => x.Cinema_Theater_ID,
                        principalSchema: "dbo",
                        principalTable: "Cinema_Theaters",
                        principalColumn: "Cinema_Theater_ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Show_Times_Movies_Movies_ID",
                        column: x => x.Movies_ID,
                        principalSchema: "dbo",
                        principalTable: "Movies",
                        principalColumn: "Movies_ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Detail_Booking_Snacks",
                schema: "dbo",
                columns: table => new
                {
                    Snack_ID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Invoice_ID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Detail_Booking_Snack_ID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Total_Snack = table.Column<int>(type: "int", nullable: true),
                    Total_Price = table.Column<decimal>(type: "money", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Detail_Booking_Snacks", x => new { x.Snack_ID, x.Invoice_ID, x.Detail_Booking_Snack_ID });
                    table.ForeignKey(
                        name: "FK_Detail_Booking_Snacks_Invoices_Invoice_ID",
                        column: x => x.Invoice_ID,
                        principalSchema: "dbo",
                        principalTable: "Invoices",
                        principalColumn: "Invoice_ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Detail_Booking_Snacks_Snacks_Snack_ID",
                        column: x => x.Snack_ID,
                        principalSchema: "dbo",
                        principalTable: "Snacks",
                        principalColumn: "Snack_ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Payment_Transactions",
                schema: "dbo",
                columns: table => new
                {
                    Payment_Transaction_ID = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Invoice_ID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Payment_Method_ID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Amount = table.Column<decimal>(type: "money", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: true),
                    Provider_Txn_ID = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Provider_Order_No = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Failure_Reason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Created_At = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Updated_At = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Paid_At = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Refunded_At = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payment_Transactions", x => x.Payment_Transaction_ID);
                    table.ForeignKey(
                        name: "FK_Payment_Transactions_Invoices_Invoice_ID",
                        column: x => x.Invoice_ID,
                        principalSchema: "dbo",
                        principalTable: "Invoices",
                        principalColumn: "Invoice_ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Payment_Transactions_Payment_Methods_Payment_Method_ID",
                        column: x => x.Payment_Method_ID,
                        principalSchema: "dbo",
                        principalTable: "Payment_Methods",
                        principalColumn: "Payment_Method_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Point_Histories",
                schema: "dbo",
                columns: table => new
                {
                    Point_History_ID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    User_ID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Invoice_ID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Change_Amount = table.Column<decimal>(type: "money", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Created_At = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Updated_At = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Point_Histories", x => x.Point_History_ID);
                    table.ForeignKey(
                        name: "FK_Point_Histories_Invoices_Invoice_ID",
                        column: x => x.Invoice_ID,
                        principalSchema: "dbo",
                        principalTable: "Invoices",
                        principalColumn: "Invoice_ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Point_Histories_Users_User_ID",
                        column: x => x.User_ID,
                        principalSchema: "dbo",
                        principalTable: "Users",
                        principalColumn: "User_ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Tickets",
                schema: "dbo",
                columns: table => new
                {
                    Ticket_ID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Invoice_ID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Ticket_Type_ID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Show_Time_ID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Seat_ID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: true),
                    Price = table.Column<decimal>(type: "money", nullable: true),
                    Created_Booking = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Expire = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tickets", x => x.Ticket_ID);
                    table.ForeignKey(
                        name: "FK_Tickets_Invoices_Invoice_ID",
                        column: x => x.Invoice_ID,
                        principalSchema: "dbo",
                        principalTable: "Invoices",
                        principalColumn: "Invoice_ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Tickets_Seats_Seat_ID",
                        column: x => x.Seat_ID,
                        principalSchema: "dbo",
                        principalTable: "Seats",
                        principalColumn: "Seat_ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tickets_Show_Times_Show_Time_ID",
                        column: x => x.Show_Time_ID,
                        principalSchema: "dbo",
                        principalTable: "Show_Times",
                        principalColumn: "Show_Time_ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Tickets_Ticket_Types_Ticket_Type_ID",
                        column: x => x.Ticket_Type_ID,
                        principalSchema: "dbo",
                        principalTable: "Ticket_Types",
                        principalColumn: "Ticket_Type_ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cinema_Theaters_Cinema_Type_ID",
                schema: "dbo",
                table: "Cinema_Theaters",
                column: "Cinema_Type_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Cinema_Theaters_Movie_Theater_ID",
                schema: "dbo",
                table: "Cinema_Theaters",
                column: "Movie_Theater_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Cinema_Types_Code",
                schema: "dbo",
                table: "Cinema_Types",
                column: "Code",
                unique: true,
                filter: "[Code] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Detail_Booking_Snacks_Invoice_ID",
                schema: "dbo",
                table: "Detail_Booking_Snacks",
                column: "Invoice_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Genres_Name",
                schema: "dbo",
                table: "Genres",
                column: "Name",
                unique: true,
                filter: "[Name] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Customer_ID",
                schema: "dbo",
                table: "Invoices",
                column: "Customer_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Payment_Method_ID",
                schema: "dbo",
                table: "Invoices",
                column: "Payment_Method_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Promotion_ID",
                schema: "dbo",
                table: "Invoices",
                column: "Promotion_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Staff_ID",
                schema: "dbo",
                table: "Invoices",
                column: "Staff_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Movie_Theaters_Province_ID_Name",
                schema: "dbo",
                table: "Movie_Theaters",
                columns: new[] { "Province_ID", "Name" },
                unique: true,
                filter: "[Name] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Movies_Status_ID",
                schema: "dbo",
                table: "Movies",
                column: "Status_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Movies_Title_Release_Date",
                schema: "dbo",
                table: "Movies",
                columns: new[] { "Title", "Release_Date" },
                unique: true,
                filter: "[Title] IS NOT NULL AND [Release_Date] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Movies_Genres_Genres_ID",
                schema: "dbo",
                table: "Movies_Genres",
                column: "Genres_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Movies_Genres_Movies_ID_Genres_ID",
                schema: "dbo",
                table: "Movies_Genres",
                columns: new[] { "Movies_ID", "Genres_ID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Movies_Participants_Movie_Role_ID",
                schema: "dbo",
                table: "Movies_Participants",
                column: "Movie_Role_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Movies_Participants_Movies_ID_Participants_ID_Movie_Role_ID",
                schema: "dbo",
                table: "Movies_Participants",
                columns: new[] { "Movies_ID", "Participants_ID", "Movie_Role_ID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Movies_Participants_Participants_ID",
                schema: "dbo",
                table: "Movies_Participants",
                column: "Participants_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Payment_Methods_Code",
                schema: "dbo",
                table: "Payment_Methods",
                column: "Code",
                unique: true,
                filter: "[Code] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Payment_Transactions_Invoice_ID",
                schema: "dbo",
                table: "Payment_Transactions",
                column: "Invoice_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Payment_Transactions_Payment_Method_ID",
                schema: "dbo",
                table: "Payment_Transactions",
                column: "Payment_Method_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Permission_Method_Url",
                schema: "dbo",
                table: "Permission",
                columns: new[] { "Method", "Url" },
                unique: true,
                filter: "[Method] IS NOT NULL AND [Url] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Point_Histories_Invoice_ID",
                schema: "dbo",
                table: "Point_Histories",
                column: "Invoice_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Point_Histories_User_ID",
                schema: "dbo",
                table: "Point_Histories",
                column: "User_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Promotion_Code",
                schema: "dbo",
                table: "Promotion",
                column: "Code",
                unique: true,
                filter: "[Code] IS NOT NULL AND [Status] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Promotion_User_ID",
                schema: "dbo",
                table: "Promotion",
                column: "User_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Provinces_Name",
                schema: "dbo",
                table: "Provinces",
                column: "Name",
                unique: true,
                filter: "[Name] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Role_Name",
                schema: "dbo",
                table: "Role",
                column: "Name",
                unique: true,
                filter: "[Name] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Role_Permission_Role_ID",
                schema: "dbo",
                table: "Role_Permission",
                column: "Role_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Seats_Cinema_Theater_ID_Label",
                schema: "dbo",
                table: "Seats",
                columns: new[] { "Cinema_Theater_ID", "Label" },
                unique: true,
                filter: "[Label] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Seats_Cinema_Theater_ID_RowIndex_ColumnIndex",
                schema: "dbo",
                table: "Seats",
                columns: new[] { "Cinema_Theater_ID", "RowIndex", "ColumnIndex" },
                unique: true,
                filter: "[RowIndex] IS NOT NULL AND [ColumnIndex] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Seats_Seat_Type_ID",
                schema: "dbo",
                table: "Seats",
                column: "Seat_Type_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Show_Times_Cinema_Theater_ID",
                schema: "dbo",
                table: "Show_Times",
                column: "Cinema_Theater_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Show_Times_Movies_ID",
                schema: "dbo",
                table: "Show_Times",
                column: "Movies_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Snack_Types_Name",
                schema: "dbo",
                table: "Snack_Types",
                column: "Name",
                unique: true,
                filter: "[Name] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Snacks_Snack_Type_ID",
                schema: "dbo",
                table: "Snacks",
                column: "Snack_Type_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Status_Name",
                schema: "dbo",
                table: "Status",
                column: "Name",
                unique: true,
                filter: "[Name] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Ticket_Types_Name",
                schema: "dbo",
                table: "Ticket_Types",
                column: "Name",
                unique: true,
                filter: "[Name] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_Invoice_ID",
                schema: "dbo",
                table: "Tickets",
                column: "Invoice_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_Seat_ID",
                schema: "dbo",
                table: "Tickets",
                column: "Seat_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_Show_Time_ID_Seat_ID",
                schema: "dbo",
                table: "Tickets",
                columns: new[] { "Show_Time_ID", "Seat_ID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_Ticket_Type_ID",
                schema: "dbo",
                table: "Tickets",
                column: "Ticket_Type_ID");

            migrationBuilder.CreateIndex(
                name: "IX_User_Role_User_ID",
                schema: "dbo",
                table: "User_Role",
                column: "User_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                schema: "dbo",
                table: "Users",
                column: "Email",
                unique: true,
                filter: "[Email] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Membership_Rank_ID",
                schema: "dbo",
                table: "Users",
                column: "Membership_Rank_ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Detail_Booking_Snacks",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Movies_Genres",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Movies_Participants",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Payment_Transactions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Point_Histories",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Role_Permission",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Tickets",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "User_Role",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Snacks",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Genres",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Movie_Role",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Participants",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Permission",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Invoices",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Seats",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Show_Times",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Ticket_Types",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Role",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Snack_Types",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Payment_Methods",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Promotion",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Seat_Types",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Cinema_Theaters",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Movies",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Users",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Cinema_Types",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Movie_Theaters",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Status",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Membership_Rank",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Provinces",
                schema: "dbo");
        }
    }
}
