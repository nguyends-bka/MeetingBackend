using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeetingBackend.Migrations
{
    /// <inheritdoc />
    public partial class RemoveInviteeAndCoHostAuditColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AddedAtUtc",
                table: "MeetingInvitees");

            migrationBuilder.DropColumn(
                name: "AddedBy",
                table: "MeetingInvitees");

            migrationBuilder.DropColumn(
                name: "AddedAtUtc",
                table: "MeetingCoHosts");

            migrationBuilder.DropColumn(
                name: "PromotedByUsername",
                table: "MeetingCoHosts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AddedAtUtc",
                table: "MeetingInvitees",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "AddedBy",
                table: "MeetingInvitees",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "AddedAtUtc",
                table: "MeetingCoHosts",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "PromotedByUsername",
                table: "MeetingCoHosts",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
