using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeetingBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddSeparateSummaryFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActionItems",
                table: "MeetingMinutesSummaries",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Discussions",
                table: "MeetingMinutesSummaries",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Overview",
                table: "MeetingMinutesSummaries",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActionItems",
                table: "MeetingMinutesSummaries");

            migrationBuilder.DropColumn(
                name: "Discussions",
                table: "MeetingMinutesSummaries");

            migrationBuilder.DropColumn(
                name: "Overview",
                table: "MeetingMinutesSummaries");
        }
    }
}
