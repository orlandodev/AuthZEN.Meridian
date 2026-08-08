# Meridian.DataAccess

EF Core `DbContext`s for the four Postgres-backed databases in this solution
(`expensesdb`, `receiptsdb`, `reportingdb`, `policydb`). Each context's seed
data is baked into its `InitialCreate` migration via `HasData` — see
`OnModelCreating` on each `*DbContext` for why the values there are fixed
literals rather than computed at runtime.

## "Failed executing DbCommand" on first startup — expected, not a bug

On a **fresh database**, every service that owns one of these contexts
(`Meridian.Expenses.Api`, `Meridian.Receipts.Api`, `Meridian.Reporting.Api`,
`Meridian.Pdp.Service`) logs something like this at `fail` level during
startup, before it successfully migrates:

```
fail: Microsoft.EntityFrameworkCore.Database.Command[20102]
      Failed executing DbCommand (18ms) [Parameters=[], CommandType='Text', CommandTimeout='30']
      SELECT "MigrationId", "ProductVersion"
      FROM "__EFMigrationsHistory"
      ORDER BY "MigrationId";
```

This is EF Core's own migration bootstrap, not an application error. `Database.MigrateAsync()`
starts by querying `__EFMigrationsHistory` to see which migrations have already
been applied. On a database that has never been migrated, that table doesn't
exist yet, so this first `SELECT` genuinely fails — EF Core catches that
failure internally and interprets it as "no migrations applied, this is a
fresh database," then creates the history table and proceeds normally.
EF's command-logging pipeline logs the failed command at `fail` level
regardless of the fact that the calling code (the migrator) handles it as
an expected case, which is why it looks alarming even though nothing is
actually wrong.

You'll see this exactly once per service, only against a truly fresh
database — e.g. the first run ever, or right after clearing the Postgres
data volume (`podman volume rm meridian.apphost-<hash>-postgres-data`,
found via `podman volume ls`). If you see it on every startup against a
database you know has already been migrated, that's worth investigating;
a one-time appearance against a fresh database is not.
