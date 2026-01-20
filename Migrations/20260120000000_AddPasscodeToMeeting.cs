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
