using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeetingBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddFullNameAndEmailToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Check if columns already exist before adding
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns 
                        WHERE table_name = 'Users' AND column_name = 'FullName'
                    ) THEN
                        ALTER TABLE ""Users"" ADD COLUMN ""FullName"" text;
                    END IF;
                    
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns 
                        WHERE table_name = 'Users' AND column_name = 'Email'
                    ) THEN
                        ALTER TABLE ""Users"" ADD COLUMN ""Email"" text;
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Email",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "FullName",
                table: "Users");
        }
    }
}
