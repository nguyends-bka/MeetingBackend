# PowerShell script to run SQL migration
# This script will add FullName and Email columns to Users table

$connectionString = "Host=bkmeeting.soict.io;Port=5434;Database=meetingdb;Username=navis;Password=navis@123"

# Extract connection details
$host = "bkmeeting.soict.io"
$port = "5434"
$database = "meetingdb"
$username = "navis"
$password = "navis@123"

# SQL script
$sqlScript = @"
DO `$`$
BEGIN
    -- Add FullName column if it doesn't exist
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'Users' AND column_name = 'FullName'
    ) THEN
        ALTER TABLE ""Users"" ADD COLUMN ""FullName"" text;
    END IF;
    
    -- Add Email column if it doesn't exist
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'Users' AND column_name = 'Email'
    ) THEN
        ALTER TABLE ""Users"" ADD COLUMN ""Email"" text;
    END IF;
END `$`$;
"@

Write-Host "Running migration to add FullName and Email columns..."
Write-Host ""

# Try using psql if available
$psqlPath = Get-Command psql -ErrorAction SilentlyContinue

if ($psqlPath) {
    Write-Host "Using psql command..."
    $env:PGPASSWORD = $password
    $sqlScript | & psql -h $host -p $port -U $username -d $database
    Remove-Item Env:\PGPASSWORD
    Write-Host ""
    Write-Host "Migration completed!"
} else {
    Write-Host "psql command not found. Please run the SQL script manually:"
    Write-Host ""
    Write-Host "Connection details:"
    Write-Host "  Host: $host"
    Write-Host "  Port: $port"
    Write-Host "  Database: $database"
    Write-Host "  Username: $username"
    Write-Host ""
    Write-Host "SQL Script:"
    Write-Host $sqlScript
    Write-Host ""
    Write-Host "Or use the SQL file: Migrations\AddFullNameAndEmail.sql"
}
