# SQL Migration Standards

Files go in `scripts/migrate_sprintN.sql`.

## Rules

- Wrap in `BEGIN`/`COMMIT`
- `CREATE TABLE IF NOT EXISTS`, `CREATE INDEX IF NOT EXISTS`
- Enum types: `CREATE TYPE ... AS ENUM` with lowercase values
- PostgreSQL reserved words must be quoted
- Index naming: `idx_tablename_column`
- All primary keys: `UUID DEFAULT gen_random_uuid()`
- All timestamps: `TIMESTAMPTZ NOT NULL DEFAULT NOW()`
