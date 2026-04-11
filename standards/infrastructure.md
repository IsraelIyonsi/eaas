# Infrastructure Standards

## Docker

- `docker-compose.yml` — base services
- `docker-compose.override.yml` — local dev (auto-loads)
- `docker-compose.prod.yml` — production
- `docker-compose.prod.env` — production secrets
- Environment variables for ALL secrets — NEVER hardcode

## Database

- PostgreSQL 16 with Npgsql
- Table partitioning for high-volume tables
- PgBouncer for connection pooling in production
- Enum types: dual registration (HasPostgresEnum + MapEnum)
- `EnableRetryOnFailure(maxRetryCount: 3)`
