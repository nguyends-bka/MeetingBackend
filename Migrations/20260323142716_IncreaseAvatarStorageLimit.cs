using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeetingBackend.Migrations
{
    /// <inheritdoc />
    public partial class IncreaseAvatarStorageLimit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_FaceTemplate_Bytes",
                table: "Users");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_FaceTemplate_Bytes",
                table: "Users",
                sql: "\"FaceTemplate\" IS NULL OR octet_length(decode(\"FaceTemplate\", 'base64')) <= 2097152");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_FaceTemplate_Bytes",
                table: "Users");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_FaceTemplate_Bytes",
                table: "Users",
                sql: "\"FaceTemplate\" IS NULL OR octet_length(decode(\"FaceTemplate\", 'base64')) <= 512");
        }
    }
}
