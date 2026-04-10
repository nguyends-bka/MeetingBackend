using Microsoft.EntityFrameworkCore.Migrations;

namespace MeetingBackend.Migrations;

public partial class NormalizeFaceEmbeddingToTwoDimensional : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE "Users"
            SET "FaceEmbedding" = ARRAY[
                "FaceEmbedding"[1:array_length("FaceEmbedding", 1) / 4],
                "FaceEmbedding"[array_length("FaceEmbedding", 1) / 4 + 1:array_length("FaceEmbedding", 1) / 2],
                "FaceEmbedding"[array_length("FaceEmbedding", 1) / 2 + 1:array_length("FaceEmbedding", 1) * 3 / 4],
                "FaceEmbedding"[array_length("FaceEmbedding", 1) * 3 / 4 + 1:array_length("FaceEmbedding", 1)]
            ]
            WHERE "FaceEmbedding" IS NOT NULL
              AND array_ndims("FaceEmbedding") = 1
              AND array_length("FaceEmbedding", 1) > 0
              AND array_length("FaceEmbedding", 1) % 4 = 0;
        """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE "Users"
            SET "FaceEmbedding" = "FaceEmbedding"[1] || "FaceEmbedding"[2] || "FaceEmbedding"[3] || "FaceEmbedding"[4]
            WHERE "FaceEmbedding" IS NOT NULL
              AND array_ndims("FaceEmbedding") = 2
              AND array_length("FaceEmbedding", 1) = 4;
        """);
    }
}
