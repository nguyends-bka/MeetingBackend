using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeetingBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddMeetingPolls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MeetingPolls",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MeetingId = table.Column<Guid>(type: "uuid", nullable: false),
                    PollId = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    OptionsJson = table.Column<string>(type: "text", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    CreatedByName = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SelectionMode = table.Column<string>(type: "text", nullable: false),
                    EndAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ClosedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingPolls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingPolls_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MeetingPollVotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MeetingPollId = table.Column<Guid>(type: "uuid", nullable: false),
                    VoterIdentity = table.Column<string>(type: "text", nullable: false),
                    VoterName = table.Column<string>(type: "text", nullable: false),
                    OptionIndicesJson = table.Column<string>(type: "text", nullable: false),
                    VotedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingPollVotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingPollVotes_MeetingPolls_MeetingPollId",
                        column: x => x.MeetingPollId,
                        principalTable: "MeetingPolls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MeetingPolls_MeetingId_PollId",
                table: "MeetingPolls",
                columns: new[] { "MeetingId", "PollId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MeetingPollVotes_MeetingPollId_VoterIdentity",
                table: "MeetingPollVotes",
                columns: new[] { "MeetingPollId", "VoterIdentity" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MeetingPollVotes");

            migrationBuilder.DropTable(
                name: "MeetingPolls");
        }
    }
}
