using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeetingBackend.Migrations
{
    /// <inheritdoc />
    public partial class UpdateFaceEmbeddingType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                                DO $$
                                BEGIN
                                    IF EXISTS (
                                        SELECT 1
                                        FROM information_schema.columns
                                        WHERE table_schema = 'public'
                                            AND table_name = 'Users'
                                            AND column_name = 'FaceEmbedding'
                                            AND udt_name = 'bytea'
                                    ) THEN
                                        ALTER TABLE "Users" ADD COLUMN "FaceEmbedding_tmp" smallint[];

                                        UPDATE "Users"
                                        SET "FaceEmbedding_tmp" = (
                                            SELECT ARRAY(
                                                SELECT CASE
                                                    WHEN b > 127 THEN (b - 256)::smallint
                                                    ELSE b::smallint
                                                END
                                                FROM (
                                                    SELECT get_byte("FaceEmbedding", i) AS b
                                                    FROM generate_series(0, octet_length("FaceEmbedding") - 1) AS i
                                                ) AS bytes
                                            )
                                        )
                                        WHERE "FaceEmbedding" IS NOT NULL;

                                        ALTER TABLE "Users" DROP COLUMN "FaceEmbedding";
                                        ALTER TABLE "Users" RENAME COLUMN "FaceEmbedding_tmp" TO "FaceEmbedding";
                                    ELSIF EXISTS (
                                        SELECT 1
                                        FROM information_schema.columns
                                        WHERE table_schema = 'public'
                                            AND table_name = 'Users'
                                            AND column_name = 'FaceEmbedding'
                                            AND udt_name = '_float4'
                                    ) THEN
                                        ALTER TABLE "Users" ADD COLUMN "FaceEmbedding_tmp" smallint[];

                                        UPDATE "Users"
                                        SET "FaceEmbedding_tmp" = (
                                            SELECT ARRAY(
                                                SELECT v::smallint
                                                FROM unnest("FaceEmbedding") AS v
                                            )
                                        )
                                        WHERE "FaceEmbedding" IS NOT NULL;

                                        ALTER TABLE "Users" DROP COLUMN "FaceEmbedding";
                                        ALTER TABLE "Users" RENAME COLUMN "FaceEmbedding_tmp" TO "FaceEmbedding";
                                    END IF;
                                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "Users" ADD COLUMN "FaceEmbedding_tmp" real[];

                UPDATE "Users"
                SET "FaceEmbedding_tmp" = (
                    SELECT ARRAY(
                        SELECT v::real
                        FROM unnest("FaceEmbedding") AS v
                    )
                )
                WHERE "FaceEmbedding" IS NOT NULL;

                ALTER TABLE "Users" DROP COLUMN "FaceEmbedding";
                ALTER TABLE "Users" RENAME COLUMN "FaceEmbedding_tmp" TO "FaceEmbedding";
                """);
        }
    }
}
