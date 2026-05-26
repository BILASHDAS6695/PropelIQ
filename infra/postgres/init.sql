-- Initialisation script — runs once on first postgres container boot.
-- All .sql files placed in /docker-entrypoint-initdb.d/ are executed in
-- alphabetical order the first time the data directory is initialised.

-- Ensure the database exists. The official image already creates it via
-- POSTGRES_DB, so this SELECT is a safe no-op if it already exists.
SELECT 'CREATE DATABASE healthplatform'
WHERE NOT EXISTS (
    SELECT FROM pg_database WHERE datname = 'healthplatform'
)\gexec

-- Switch to the application database
\connect healthplatform

-- Create application schema
CREATE SCHEMA IF NOT EXISTS app;

-- Grant the application user privileges on the schema
GRANT USAGE  ON SCHEMA app TO CURRENT_USER;
GRANT CREATE ON SCHEMA app TO CURRENT_USER;
