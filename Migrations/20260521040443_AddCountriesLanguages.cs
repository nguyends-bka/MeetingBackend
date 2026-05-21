using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeetingBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddCountriesLanguages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Countries",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CountryName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Countries", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "Languages",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    LanguageName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Languages", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "UserCountries",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCountries", x => new { x.UserId, x.CountryCode });
                    table.ForeignKey(
                        name: "FK_UserCountries_Countries_CountryCode",
                        column: x => x.CountryCode,
                        principalTable: "Countries",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserCountries_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserLanguages",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LanguageCode = table.Column<string>(type: "character varying(10)", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLanguages", x => new { x.UserId, x.LanguageCode });
                    table.ForeignKey(
                        name: "FK_UserLanguages_Languages_LanguageCode",
                        column: x => x.LanguageCode,
                        principalTable: "Languages",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserLanguages_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Countries_IsActive",
                table: "Countries",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Languages_IsActive",
                table: "Languages",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_UserCountries_CountryCode",
                table: "UserCountries",
                column: "CountryCode");

            migrationBuilder.CreateIndex(
                name: "IX_UserLanguages_LanguageCode",
                table: "UserLanguages",
                column: "LanguageCode");

            // ── Seed Countries ────────────────────────────────────────────────
            migrationBuilder.InsertData(
                table: "Countries",
                columns: new[] { "Code", "CountryName" },
                values: new object[,]
                {
                    { "VN", "Việt Nam" },
                    { "US", "United States" },
                    { "JP", "Japan" },
                    { "KR", "South Korea" },
                    { "CN", "China" },
                    { "FR", "France" },
                    { "DE", "Germany" },
                    { "GB", "United Kingdom" },
                    { "AU", "Australia" },
                    { "CA", "Canada" },
                    { "SG", "Singapore" },
                    { "TH", "Thailand" },
                    { "IN", "India" },
                    { "RU", "Russia" },
                    { "BR", "Brazil" }
                });

            // ── Seed Languages ────────────────────────────────────────────────
            migrationBuilder.InsertData(
                table: "Languages",
                columns: new[] { "Code", "LanguageName" },
                values: new object[,]
                {
                    { "vi", "Tiếng Việt" },
                    { "en", "English" },
                    { "ja", "Japanese" },
                    { "ko", "Korean" },
                    { "zh", "Chinese" },
                    { "fr", "French" },
                    { "de", "German" },
                    { "es", "Spanish" },
                    { "pt", "Portuguese" },
                    { "ru", "Russian" },
                    { "th", "Thai" },
                    { "ar", "Arabic" }
                });
        }


        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserCountries");

            migrationBuilder.DropTable(
                name: "UserLanguages");

            migrationBuilder.DropTable(
                name: "Countries");

            migrationBuilder.DropTable(
                name: "Languages");
        }
    }
}
