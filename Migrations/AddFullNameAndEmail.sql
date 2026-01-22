-- Migration script to add FullName and Email columns to Users table
-- Run this script directly on your PostgreSQL database

DO $$
BEGIN
    -- Add FullName column if it doesn't exist
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'Users' AND column_name = 'FullName'
    ) THEN
        ALTER TABLE "Users" ADD COLUMN "FullName" text;
    END IF;
    
    -- Add Email column if it doesn't exist
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'Users' AND column_name = 'Email'
    ) THEN
        ALTER TABLE "Users" ADD COLUMN "Email" text;
    END IF;
END $$;
