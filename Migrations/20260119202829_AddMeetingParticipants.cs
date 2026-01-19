using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeetingBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddMeetingParticipants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Check if table already exists before creating
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.tables 
                        WHERE table_name = 'MeetingParticipants'
                    ) THEN
                        CREATE TABLE ""MeetingParticipants"" (
                            ""Id"" uuid NOT NULL,
                            ""MeetingId"" uuid NOT NULL,
                            ""UserId"" text NOT NULL,
                            ""Username"" text NOT NULL,
                            ""JoinedAt"" timestamp with time zone NOT NULL,
                            ""LeftAt"" timestamp with time zone NULL,
                            CONSTRAINT ""PK_MeetingParticipants"" PRIMARY KEY (""Id""),
                            CONSTRAINT ""FK_MeetingParticipants_Meetings_MeetingId"" 
                                FOREIGN KEY (""MeetingId"") 
                                REFERENCES ""Meetings"" (""Id"") 
                                ON DELETE CASCADE
                        );

                        CREATE INDEX ""IX_MeetingParticipants_MeetingId"" 
                            ON ""MeetingParticipants"" (""MeetingId"");
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MeetingParticipants");
        }
    }
}
