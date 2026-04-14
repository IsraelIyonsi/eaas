# Postgres backup & restore runbook

## Targets

| Metric | Target |
| --- | --- |
| RPO (data loss window) | 6 hours |
| RTO (restore to service) | 1 hour |
| Local retention | 30 days |
| Off-host retention (S3) | 7 days (bucket lifecycle) |
| Restore-drill frequency | Monthly |

## Backup flow

`cron` on the VPS invokes `/opt/eaas/scripts/backup-postgres.sh` every 6
hours. The script:

1. Runs `pg_dump` against the `eaas-postgres` container.
2. Writes a gzip'd dump to `/root/backups/eaas-YYYYMMDD_HHMMSS.sql.gz`.
3. Verifies gzip integrity.
4. If `BACKUP_S3_BUCKET` is set and `aws` is installed, uploads the dump to S3.
5. Prunes local backups older than 30 days.

Source of truth is the repo. To install/update on the VPS:

```bash
scp scripts/backup-postgres.sh     root@178.104.141.21:/opt/eaas/scripts/
scp scripts/test-restore.sh        root@178.104.141.21:/opt/eaas/scripts/
scp infrastructure/cron/eaas-backup root@178.104.141.21:/etc/cron.d/eaas-backup
ssh root@178.104.141.21 'chmod 755 /opt/eaas/scripts/backup-postgres.sh /opt/eaas/scripts/test-restore.sh && chown root:root /etc/cron.d/eaas-backup && chmod 644 /etc/cron.d/eaas-backup'
```

## Off-host storage (optional but recommended)

Set the bucket in `/opt/eaas/.env`:

```
BACKUP_S3_BUCKET=eaas-backups-prod
```

Apply a 7-day lifecycle policy at the bucket level (AWS console, Terraform,
or `aws s3api put-bucket-lifecycle-configuration`). The backup script does
not manage lifecycle itself.

## Manual backup

```bash
ssh root@178.104.141.21 '/opt/eaas/scripts/backup-postgres.sh'
```

## Restoring to production (RTO drill)

1. Stop writer traffic (scale API + worker to 0 in docker compose).
2. Identify the target backup: `ls -lt /root/backups/eaas-*.sql.gz | head`.
3. Restore into the live container (DESTRUCTIVE — creates a snapshot first):
   ```bash
   docker exec eaas-postgres pg_dump -U postgres eaas | gzip > /root/backups/pre-restore-$(date +%Y%m%d_%H%M%S).sql.gz
   gunzip -c /root/backups/eaas-YYYYMMDD_HHMMSS.sql.gz \
     | docker exec -i eaas-postgres psql -U postgres -d eaas -v ON_ERROR_STOP=1
   ```
4. Run a smoke query: `docker exec eaas-postgres psql -U postgres -d eaas -c 'SELECT count(*) FROM tenants;'`
5. Bring API + worker back up.
6. Verify with the production health check.

Budget: step 2 to step 5 should complete in under 60 minutes for the current
database size (~few hundred MB).

## Restore drill (non-destructive)

Runs automatically on the 1st of every month via cron, and can be invoked by
hand at any time:

```bash
ssh root@178.104.141.21 '/opt/eaas/scripts/test-restore.sh "$(ls -t /root/backups/eaas-*.sql.gz | head -n1)"'
```

The drill spins up a throwaway `postgres:17-alpine` container, restores the
dump into it, asserts the schema has at least 5 public tables, then tears the
container down. Exit 0 means the backup is good; non-zero means the backup
chain is broken — page on-call immediately.

## Monitoring

- File-age alert: `/root/backups/` newest file older than 7 hours -> PAGE.
- `/var/log/eaas-backup.log` tailed into Loki; alert on `ERROR`.
- `/var/log/eaas-restore-drill.log` — alert if the monthly drill fails.
