# 0006. Applying migrations at startup is opt-in

- Status: Accepted
- Date: 2026-11-06 (records a decision made when Docker and compose were added)

## Context

A new developer running `docker compose up` should reach a working API without a manual database step. In production,
the same behaviour is risky: several instances start at once and race to alter the schema, a failed migration blocks the
whole fleet from starting, and the runtime database role needs the right to change tables.

## Decision

`Database:MigrateOnStartup` is a setting that defaults to **off**. The launch profiles and `docker-compose.yml` turn it
on, so local runs and compose create or update the tables when the host starts. Anywhere else the setting is absent, so
the host never changes the schema on its own, and migrations are applied deliberately with
`dotnet ef database update --project GrocerEase.Infrastructure --startup-project GrocerEase.Api`.

## Consequences

- Local development and compose need no extra step, and a production deploy cannot change the schema by accident.
- A production database currently has no automated migration step, so a deploy that needs one is manual. A pipeline step
  that runs migrations once, before the new version starts, is open in `TODO.md`.
- In compose the API role (`grocerease_app`) owns the database, so it can migrate, and could also drop tables. Splitting a
  migration role from a limited runtime role is open in `TODO.md`.
- Migrations run while the old version may still be serving, so a change that removes or renames a column needs to be
  split across releases (add, switch the code, then drop). Nothing enforces that yet.
