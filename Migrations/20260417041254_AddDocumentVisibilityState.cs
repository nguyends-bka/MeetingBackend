using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeetingBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentVisibilityState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsShared",
                table: "MeetingDocuments",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsShared",
                table: "MeetingDocuments");
        }
    }
}
