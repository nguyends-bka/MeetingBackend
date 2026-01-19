START TRANSACTION;

                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.tables 
                        WHERE table_name = 'MeetingParticipants'
                    ) THEN
                        CREATE TABLE "MeetingParticipants" (
                            "Id" uuid NOT NULL,
                            "MeetingId" uuid NOT NULL,
                            "UserId" text NOT NULL,
                            "Username" text NOT NULL,
                            "JoinedAt" timestamp with time zone NOT NULL,
                            "LeftAt" timestamp with time zone NULL,
                            CONSTRAINT "PK_MeetingParticipants" PRIMARY KEY ("Id"),
                            CONSTRAINT "FK_MeetingParticipants_Meetings_MeetingId" 
                                FOREIGN KEY ("MeetingId") 
                                REFERENCES "Meetings" ("Id") 
                                ON DELETE CASCADE
                        );

                        CREATE INDEX "IX_MeetingParticipants_MeetingId" 
                            ON "MeetingParticipants" ("MeetingId");
                    END IF;
                END $$;
            

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260119202829_AddMeetingParticipants', '10.0.2');

COMMIT;

