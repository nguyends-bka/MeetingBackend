using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeetingBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddMeetingStatusAndEstimatedEnd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EstimatedEndAt",
                table: "Meetings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Meetings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("UPDATE \"Meetings\" SET \"EstimatedEndAt\" = \"StartedAt\";");
            migrationBuilder.Sql("UPDATE \"Meetings\" SET \"Status\" = 2 WHERE \"EndedAt\" IS NOT NULL;");
            migrationBuilder.Sql("UPDATE \"Meetings\" SET \"Status\" = 1 WHERE \"StartedAt\" IS NOT NULL AND \"EndedAt\" IS NULL AND EXISTS (SELECT 1 FROM \"MeetingParticipants\" WHERE \"MeetingParticipants\".\"MeetingId\" = \"Meetings\".\"Id\");");
            migrationBuilder.Sql("UPDATE \"Meetings\" SET \"StartedAt\" = NULL WHERE \"Status\" = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EstimatedEndAt",
                table: "Meetings");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Meetings");
        }
    }
}
