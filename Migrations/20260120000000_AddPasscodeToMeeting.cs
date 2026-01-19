using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeetingBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddPasscodeToMeeting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
<<<<<<< HEAD
            // Check if column already exists before adding
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns 
                        WHERE table_name = 'Meetings' AND column_name = 'Passcode'
                    ) THEN
                        ALTER TABLE ""Meetings"" ADD COLUMN ""Passcode"" text NOT NULL DEFAULT '';
                    END IF;
                END $$;
            ");
=======
            migrationBuilder.AddColumn<string>(
                name: "Passcode",
                table: "Meetings",
                type: "text",
                nullable: false,
                defaultValue: "");
>>>>>>> d870686181126158e1dca947c8b46b4652d1406e
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Passcode",
                table: "Meetings");
        }
    }
}
