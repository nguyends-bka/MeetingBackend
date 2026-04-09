using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeetingBackend.Migrations
{
    /// <inheritdoc />
    public partial class RestructureMeetingInviteesLikePollManagers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MeetingInvitees_MeetingId_UserId",
                table: "MeetingInvitees");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "MeetingInvitees");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "MeetingInvitees",
                newName: "AddedAtUtc");

            migrationBuilder.AddColumn<string>(
                name: "AddedBy",
                table: "MeetingInvitees",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Username",
                table: "MeetingInvitees",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingInvitees_MeetingId_Username",
                table: "MeetingInvitees",
                columns: new[] { "MeetingId", "Username" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MeetingInvitees_MeetingId_Username",
                table: "MeetingInvitees");

            migrationBuilder.DropColumn(
                name: "AddedBy",
                table: "MeetingInvitees");

            migrationBuilder.DropColumn(
                name: "Username",
                table: "MeetingInvitees");

            migrationBuilder.RenameColumn(
                name: "AddedAtUtc",
                table: "MeetingInvitees",
                newName: "CreatedAt");

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "MeetingInvitees",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_MeetingInvitees_MeetingId_UserId",
                table: "MeetingInvitees",
                columns: new[] { "MeetingId", "UserId" },
                unique: true);
        }
    }
}
