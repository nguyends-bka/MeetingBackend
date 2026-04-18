using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeetingBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddMeetingRecordings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"DO $$
BEGIN
    IF to_regclass('public.""MeetingRecordings""') IS NULL THEN
        CREATE TABLE public.""MeetingRecordings"" (
            ""Id"" uuid NOT NULL,
            ""MeetingId"" uuid NOT NULL,
            ""EgressId"" text NOT NULL,
            ""Status"" text NOT NULL,
            ""OutputFilePath"" text NOT NULL,
            ""StartedAtUtc"" timestamp with time zone NOT NULL,
            ""EndedAtUtc"" timestamp with time zone NULL,
            ""StartedByUserId"" text NOT NULL,
            ""StartedByName"" text NOT NULL,
            ""ErrorMessage"" text NULL,
            CONSTRAINT ""PK_MeetingRecordings"" PRIMARY KEY (""Id""),
            CONSTRAINT ""FK_MeetingRecordings_Meetings_MeetingId""
                FOREIGN KEY (""MeetingId"") REFERENCES public.""Meetings"" (""Id"") ON DELETE CASCADE
        );
    END IF;

    IF to_regclass('public.""IX_MeetingRecordings_EgressId""') IS NULL THEN
        CREATE UNIQUE INDEX ""IX_MeetingRecordings_EgressId""
        ON public.""MeetingRecordings"" (""EgressId"");
    END IF;

    IF to_regclass('public.""IX_MeetingRecordings_MeetingId""') IS NULL THEN
        CREATE INDEX ""IX_MeetingRecordings_MeetingId""
        ON public.""MeetingRecordings"" (""MeetingId"");
    END IF;
END $$;"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"DROP TABLE IF EXISTS public.""MeetingRecordings"";"
            );
        }
    }
}
