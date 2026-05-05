using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeetingBackend.Migrations
{
    public partial class AddMeetingNotificationTitleColumn : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE \"MeetingNotifications\" ADD COLUMN IF NOT EXISTS \"MeetingTitle\" text NOT NULL DEFAULT '';");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE \"MeetingNotifications\" DROP COLUMN IF EXISTS \"MeetingTitle\";");
        }
    }
}
