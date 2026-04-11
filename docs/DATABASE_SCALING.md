# Database Scaling Strategy

Target: **100M+ emails** with sub-second query performance.

## Why Partitioning

PostgreSQL performance degrades significantly when tables exceed 50-100M rows. Without partitioning:
- Sequential scans on 100M+ rows take minutes
- Index bloat causes write amplification
- VACUUM operations block writes for extended periods
- Deleting old data requires expensive DELETE operations

Monthly range partitioning on timestamp columns solves all of these:
- Queries with date filters only scan relevant partitions (partition pruning)
- Each partition has its own indexes, keeping them small and fast
- VACUUM operates per-partition, reducing lock contention
- Dropping an entire partition is O(1), no row-by-row deletion needed

## Partitioned Tables

| Table | Partition Key | Strategy |
|---|---|---|
| `emails` | `created_at` | Monthly RANGE |
| `inbound_emails` | `received_at` | Monthly RANGE |
| `email_events` | `created_at` | Monthly RANGE |

Partition naming: `{table}_{YYYY}_{MM}` (e.g., `emails_2026_04`).

A `DEFAULT` partition catches any rows that fall outside defined ranges.

## Managing Partitions

### Automatic Creation (Recommended)

Use the SQL function to create partitions for the next N months:

```sql
-- Create partitions for next 3 months
SELECT create_future_partitions(3);

-- Create a specific partition
SELECT create_monthly_partition('emails', 2027, 7);
```

### Shell Script

```bash
# Create partitions for next 6 months
./scripts/create_partitions.sh 6

# With custom database URL
./scripts/create_partitions.sh 3 "postgresql://user:pass@host:5432/eaas"
```

Run this monthly via cron:

```cron
# First day of each month at 00:00, create next 3 months of partitions
0 0 1 * * /opt/eaas/scripts/create_partitions.sh 3 >> /var/log/eaas/partitions.log 2>&1
```

### Manually Creating a Partition

```sql
CREATE TABLE emails_2027_07 PARTITION OF emails
    FOR VALUES FROM ('2027-07-01') TO ('2027-08-01');
```

### Dropping Old Partitions

Dropping a partition is instant and reclaims disk space immediately:

```sql
-- Detach first (keeps data accessible if needed)
ALTER TABLE emails DETACH PARTITION emails_2026_01;

-- Then drop when confirmed safe
DROP TABLE emails_2026_01;
```

## PgBouncer Connection Pooling

PgBouncer sits between the application and PostgreSQL, multiplexing hundreds of application connections into a small pool of database connections.

### Why It Helps

- PostgreSQL forks a process per connection (~10MB each). 500 direct connections = 5GB overhead.
- PgBouncer maintains 50 actual connections, serving 500 application connections.
- Transaction-mode pooling releases connections between transactions, maximizing throughput.

### Configuration

Config files: `infrastructure/pgbouncer/pgbouncer.ini`

Key settings:
- `pool_mode = transaction` -- connections returned to pool after each transaction
- `default_pool_size = 50` -- max connections to PostgreSQL per database
- `max_client_conn = 500` -- max application connections to PgBouncer
- `reserve_pool_size = 10` -- extra connections for burst traffic

### Connection String Changes

Update your application connection string to point to PgBouncer:

```
# Before (direct PostgreSQL)
Host=postgres;Port=5432;Database=eaas;Username=eaas;Password=...

# After (through PgBouncer)
Host=pgbouncer;Port=6432;Database=eaas;Username=eaas;Password=...
```

In `appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=pgbouncer;Port=6432;Database=eaas;Username=eaas;Password=secret"
  }
}
```

**Important:** When using PgBouncer in transaction mode, avoid:
- `SET` commands (use `SET LOCAL` inside transactions instead)
- `LISTEN/NOTIFY`
- Prepared statements (set `No Reset On Close=true` in Npgsql, or use `server_reset_query` in PgBouncer)

## Archival Strategy

The `archive_old_emails` function moves old emails to a separate `emails_archive` table.

```sql
-- Archive emails older than 90 days
SELECT archive_old_emails(90);

-- Archive emails older than 365 days
SELECT archive_old_emails(365);
```

Per-tenant retention can be implemented at the application level:

```sql
-- Custom retention per tenant (application logic)
-- Tenant A: 30 days, Tenant B: 365 days
DELETE FROM emails WHERE tenant_id = 'tenant-a-id' AND created_at < NOW() - INTERVAL '30 days';
```

For large-scale archival, prefer dropping entire partitions over row-by-row deletion.

## Monitoring Queries

### Partition Health

```sql
-- List all partitions with sizes and row counts
SELECT
    parent.relname AS table_name,
    child.relname AS partition_name,
    pg_size_pretty(pg_total_relation_size(child.oid)) AS total_size,
    pg_size_pretty(pg_relation_size(child.oid)) AS data_size,
    pg_stat_get_live_tuples(child.oid) AS live_rows,
    pg_stat_get_dead_tuples(child.oid) AS dead_rows
FROM pg_inherits
JOIN pg_class parent ON pg_inherits.inhparent = parent.oid
JOIN pg_class child ON pg_inherits.inhrelid = child.oid
WHERE parent.relname IN ('emails', 'inbound_emails', 'email_events')
ORDER BY parent.relname, child.relname;
```

### Check Partition Pruning Is Working

```sql
-- Should show "Append" with only relevant partitions
EXPLAIN (ANALYZE, BUFFERS)
SELECT * FROM emails
WHERE tenant_id = '...' AND created_at >= '2026-04-01' AND created_at < '2026-05-01';
```

### PgBouncer Stats

```sql
-- Connect to PgBouncer admin console
-- psql -h pgbouncer -p 6432 -U eaas pgbouncer

SHOW POOLS;
SHOW STATS;
SHOW CLIENTS;
```

### Table Bloat Detection

```sql
SELECT
    schemaname, tablename,
    pg_size_pretty(pg_total_relation_size(schemaname || '.' || tablename)) AS total_size,
    n_live_tup,
    n_dead_tup,
    ROUND(100.0 * n_dead_tup / NULLIF(n_live_tup + n_dead_tup, 0), 2) AS dead_pct
FROM pg_stat_user_tables
WHERE schemaname = 'public'
ORDER BY pg_total_relation_size(schemaname || '.' || tablename) DESC
LIMIT 20;
```

## Storage Estimates

| Metric | Estimate |
|---|---|
| Average email row size | ~2 KB (metadata, no body) |
| Average email row size with body | ~8 KB |
| 1M emails (data only) | ~2 GB |
| 1M emails (data + indexes) | ~3.5 GB |
| 100M emails (data + indexes) | ~350 GB |
| email_events per email (avg 3) | ~0.5 KB each |
| 100M emails events overhead | ~150 GB |

**Monthly partition sizing** (assuming 10M emails/month):
- Each `emails` partition: ~35 GB
- Each `email_events` partition: ~15 GB
- Each `inbound_emails` partition: varies by inbound volume

Plan for approximately **500 GB total** for 100M emails with events and indexes.

## Runbook: Adding Capacity

1. **Monitor partition sizes** weekly using the monitoring queries above.
2. **Create partitions 3 months ahead** -- the cron job handles this automatically.
3. **Archive old data** monthly: `SELECT archive_old_emails(90);`
4. **Drop old partitions** after archival is confirmed.
5. **Increase PgBouncer pool** if connection wait times exceed 100ms.
6. **Add read replicas** for analytics queries when write throughput exceeds 50K/min.
