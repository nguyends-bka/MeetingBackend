using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeetingBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddHostNameToMeeting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "HostIdentity",
                table: "Meetings",
                newName: "Title");

            migrationBuilder.AddColumn<string>(
                name: "HostName",
                table: "Meetings",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HostName",
                table: "Meetings");

            migrationBuilder.RenameColumn(
                name: "Title",
                table: "Meetings",
                newName: "HostIdentity");
        }
    }
}
