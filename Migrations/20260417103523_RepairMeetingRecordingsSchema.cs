using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeetingBackend.Migrations
{
    /// <inheritdoc />
    public partial class RepairMeetingRecordingsSchema : Migration
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

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'MeetingRecordings' AND column_name = 'MeetingId'
    ) THEN
        ALTER TABLE public.""MeetingRecordings"" ADD COLUMN ""MeetingId"" uuid NULL;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'MeetingRecordings' AND column_name = 'EgressId'
    ) THEN
        ALTER TABLE public.""MeetingRecordings"" ADD COLUMN ""EgressId"" text NULL;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'MeetingRecordings' AND column_name = 'Status'
    ) THEN
        ALTER TABLE public.""MeetingRecordings"" ADD COLUMN ""Status"" text NULL;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'MeetingRecordings' AND column_name = 'OutputFilePath'
    ) THEN
        ALTER TABLE public.""MeetingRecordings"" ADD COLUMN ""OutputFilePath"" text NULL;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'MeetingRecordings' AND column_name = 'StartedAtUtc'
    ) THEN
        ALTER TABLE public.""MeetingRecordings"" ADD COLUMN ""StartedAtUtc"" timestamp with time zone NULL;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'MeetingRecordings' AND column_name = 'EndedAtUtc'
    ) THEN
        ALTER TABLE public.""MeetingRecordings"" ADD COLUMN ""EndedAtUtc"" timestamp with time zone NULL;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'MeetingRecordings' AND column_name = 'StartedByUserId'
    ) THEN
        ALTER TABLE public.""MeetingRecordings"" ADD COLUMN ""StartedByUserId"" text NULL;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'MeetingRecordings' AND column_name = 'StartedByName'
    ) THEN
        ALTER TABLE public.""MeetingRecordings"" ADD COLUMN ""StartedByName"" text NULL;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'MeetingRecordings' AND column_name = 'ErrorMessage'
    ) THEN
        ALTER TABLE public.""MeetingRecordings"" ADD COLUMN ""ErrorMessage"" text NULL;
    END IF;

    IF to_regclass('public.""IX_MeetingRecordings_MeetingId""') IS NULL THEN
        BEGIN
            CREATE INDEX ""IX_MeetingRecordings_MeetingId"" ON public.""MeetingRecordings"" (""MeetingId"");
        EXCEPTION WHEN OTHERS THEN
            NULL;
        END;
    END IF;

    IF to_regclass('public.""IX_MeetingRecordings_EgressId""') IS NULL THEN
        BEGIN
            CREATE UNIQUE INDEX ""IX_MeetingRecordings_EgressId"" ON public.""MeetingRecordings"" (""EgressId"");
        EXCEPTION WHEN OTHERS THEN
            NULL;
        END;
    END IF;
END $$;"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally no-op: this migration repairs production drift and should not remove columns.
        }
    }
}
