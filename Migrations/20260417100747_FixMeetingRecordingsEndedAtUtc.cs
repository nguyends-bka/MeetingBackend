using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeetingBackend.Migrations
{
    /// <inheritdoc />
    public partial class FixMeetingRecordingsEndedAtUtc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"DO $$
BEGIN
    IF to_regclass('public.""MeetingRecordings""') IS NULL THEN
        RETURN;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'MeetingRecordings'
          AND column_name = 'EndedAt'
    )
    AND NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'MeetingRecordings'
          AND column_name = 'EndedAtUtc'
    ) THEN
        ALTER TABLE public.""MeetingRecordings"" RENAME COLUMN ""EndedAt"" TO ""EndedAtUtc"";
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'MeetingRecordings'
          AND column_name = 'EndedAtUtc'
    ) THEN
        ALTER TABLE public.""MeetingRecordings"" ADD COLUMN ""EndedAtUtc"" timestamp with time zone NULL;
    END IF;
END $$;"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"DO $$
BEGIN
    IF to_regclass('public.""MeetingRecordings""') IS NULL THEN
        RETURN;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'MeetingRecordings'
          AND column_name = 'EndedAtUtc'
    )
    AND NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'MeetingRecordings'
          AND column_name = 'EndedAt'
    ) THEN
        ALTER TABLE public.""MeetingRecordings"" RENAME COLUMN ""EndedAtUtc"" TO ""EndedAt"";
    END IF;
END $$;"
            );
        }
    }
}
