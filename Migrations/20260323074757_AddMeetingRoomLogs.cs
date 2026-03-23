using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeetingBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddMeetingRoomLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MeetingChatMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MeetingId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientMessageId = table.Column<string>(type: "text", nullable: true),
                    SenderIdentity = table.Column<string>(type: "text", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingChatMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingChatMessages_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MeetingTranscriptEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MeetingId = table.Column<Guid>(type: "uuid", nullable: false),
                    SpeakerIdentity = table.Column<string>(type: "text", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    AtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingTranscriptEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingTranscriptEntries_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MeetingChatMessages_MeetingId_ClientMessageId",
                table: "MeetingChatMessages",
                columns: new[] { "MeetingId", "ClientMessageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MeetingChatMessages_MeetingId_SentAtUtc",
                table: "MeetingChatMessages",
                columns: new[] { "MeetingId", "SentAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MeetingTranscriptEntries_MeetingId_AtUtc",
                table: "MeetingTranscriptEntries",
                columns: new[] { "MeetingId", "AtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MeetingChatMessages");

            migrationBuilder.DropTable(
                name: "MeetingTranscriptEntries");
        }
    }
}
