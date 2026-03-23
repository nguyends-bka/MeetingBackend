using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeetingBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddUserProfessionalFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AcademicDegree",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AcademicRank",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FaceTemplate",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Position",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_AcademicDegree",
                table: "Users",
                sql: "\"AcademicDegree\" IS NULL OR \"AcademicDegree\" IN ('TS','ThS','CN','KS')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_AcademicRank",
                table: "Users",
                sql: "\"AcademicRank\" IS NULL OR \"AcademicRank\" IN ('GS','PGS')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_FaceTemplate_Bytes",
                table: "Users",
                sql: "\"FaceTemplate\" IS NULL OR octet_length(\"FaceTemplate\") <= 512");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_AcademicDegree",
                table: "Users");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_AcademicRank",
                table: "Users");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_FaceTemplate_Bytes",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AcademicDegree",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AcademicRank",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "FaceTemplate",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Position",
                table: "Users");
        }
    }
}
