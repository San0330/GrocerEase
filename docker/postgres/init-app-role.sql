-- Creates the role the API connects as and makes it the owner of the application database.
--
-- The grocerease.db-init service runs this as the superuser on every `docker compose up`, so every statement must be
-- safe to repeat. That is also what makes it work on a volume that already holds data: a script in
-- /docker-entrypoint-initdb.d would only run when the volume is empty.
--
-- Expects the psql variable app_password, and to be connected to the application database (PGDATABASE).
--
-- The role is not a superuser, so a leaked API credential cannot read server files, run programs through COPY, or reach
-- other databases the way the postgres login can. It owns this one database, which is what lets the startup
-- migrations create and alter tables, and create the trusted citext extension.

SELECT format('CREATE ROLE grocerease_app LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS PASSWORD %L', :'app_password')
WHERE NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'grocerease_app')
\gexec

-- Runs every time, so changing APP_DB_PASSWORD in .env takes effect on the next start.
SELECT format('ALTER ROLE grocerease_app PASSWORD %L', :'app_password')
\gexec

SELECT format('ALTER DATABASE %I OWNER TO grocerease_app', current_database())
\gexec

-- Tables created earlier by the postgres login (an older volume, or the API run locally with dotnet run) belong to
-- postgres, and the app role could not read or migrate them. Hand them over. Owned sequences follow their table.
SELECT format('ALTER TABLE %I.%I OWNER TO grocerease_app', schemaname, tablename)
FROM pg_tables
WHERE schemaname = 'public' AND tableowner <> 'grocerease_app'
\gexec
