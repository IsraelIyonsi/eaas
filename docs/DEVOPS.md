# EaaS DevOps & Operations Guide

## Table of Contents

1. [Branching Strategy](#branching-strategy)
2. [Commit Convention](#commit-convention)
3. [PR Process](#pr-process)
4. [Release Process](#release-process)
5. [Deployment Pipeline](#deployment-pipeline)
6. [Infrastructure Setup](#infrastructure-setup)
7. [Monitoring](#monitoring)
8. [Backup Strategy](#backup-strategy)
9. [Disaster Recovery](#disaster-recovery)
10. [Security](#security)

---

## Branching Strategy

```
feature/api/US-1.1-tenant-registration
         \
          \──→ PR ──→ dev ──→ release/v1.0.0 ──→ tag: v1.0.0 (prod)
         /              ↑
        /               │
feature/dashboard/US-2.1-login
                        │
              hotfix/fix-ses-timeout ──→ PR ──→ dev (+ cherry-pick to release if needed)
```

### Branch Types

| Branch | Pattern | Purpose | Base | Merges Into |
|--------|---------|---------|------|-------------|
| **dev** | `dev` | Default integration branch | - | `release/*` |
| **Feature** | `feature/<epic>/<story-id>-<short-description>` | New features | `dev` | `dev` via PR |
| **Hotfix** | `hotfix/<description>` | Urgent production fixes | `dev` | `dev` via PR |
| **Release** | `release/v<version>` | Release candidates | `dev` | Tagged for prod |

### Rules

- `dev` is the **default branch** (not main/master).
- All work happens on feature branches; never commit directly to `dev`.
- Feature branches must be up to date with `dev` before merging (rebase or merge from dev).
- Delete feature branches after merge.
- Release branches are created when `dev` is stable and ready to ship.

---

## Commit Convention

We use [Conventional Commits](https://www.conventionalcommits.org/) with the following format:

```
<type>(<scope>): <short description>

<optional body>

<optional footer>
```

### Types

| Type | Description | Example |
|------|-------------|---------|
| `feat` | New feature | `feat(api): add tenant registration endpoint` |
| `fix` | Bug fix | `fix(worker): handle SES rate limit exceeded` |
| `chore` | Maintenance, deps | `chore: update NuGet packages` |
| `docs` | Documentation | `docs: add API rate limiting guide` |
| `test` | Tests | `test(api): add webhook signature verification tests` |
| `ci` | CI/CD changes | `ci: add Docker build caching` |
| `refactor` | Code restructuring | `refactor(core): extract email validation to shared lib` |

### Scope (optional)

Use the service or module name: `api`, `dashboard`, `worker`, `webhook`, `core`, `infra`, `db`.

### Rules

- Subject line: imperative mood, lowercase, no period, max 72 characters.
- Body: explain **why**, not what. Wrap at 80 characters.
- Footer: reference story IDs (e.g., `Ref: US-1.1`).
- Commits authored by Israel only -- no co-author tags.

### Examples

```
feat(api): add API key generation with SHA-256 hashing

Tenants can now generate API keys with configurable rate limits.
Keys are hashed before storage for security.

Ref: US-1.3
```

```
fix(worker): retry SES sends on transient throttle errors

AWS SES returns ThrottlingException under burst load.
Added exponential backoff with 3 retries before dead-lettering.

Ref: US-3.2
```

---

## PR Process

### Creating a PR

1. Push your feature branch to origin.
2. Open a PR targeting `dev`.
3. Fill in the PR template (`.github/PULL_REQUEST_TEMPLATE.md`).
4. Ensure CI passes (build, test, lint, Docker).

### Requirements

- At least 1 approval required.
- All CI checks must pass.
- Branch must be up to date with `dev`.
- No merge conflicts.

### Merge Strategy

- **Squash merge** into `dev` (keeps history clean).
- PR title becomes the squash commit message -- use conventional commit format.
- Delete source branch after merge.

### Review Checklist

- [ ] Code compiles with zero warnings.
- [ ] All tests pass (unit + integration).
- [ ] No secrets or credentials in code.
- [ ] Database migrations are reversible.
- [ ] API changes are backward-compatible.
- [ ] Error handling covers edge cases.
- [ ] Performance: no N+1 queries, proper caching.

---

## Release Process

### Versioning

We follow [Semantic Versioning](https://semver.org/):

- **MAJOR** (1.0.0): Breaking API changes.
- **MINOR** (0.1.0): New features, backward-compatible.
- **PATCH** (0.0.1): Bug fixes, backward-compatible.

### Release Steps

1. **Create release branch** from `dev`:
   ```bash
   git checkout dev && git pull
   git checkout -b release/v1.0.0
   ```

2. **Update version** in project files:
   - `Directory.Build.props` or `.csproj` files.
   - `docker-compose.yml` image tags.

3. **Generate changelog** from commit history:
   ```bash
   git log --oneline v0.9.0..HEAD --pretty=format:"- %s"
   ```

4. **Push release branch** -- triggers deployment pipeline:
   ```bash
   git push -u origin release/v1.0.0
   ```

5. **Tag after successful deployment**:
   ```bash
   git tag -a v1.0.0 -m "Release v1.0.0"
   git push origin v1.0.0
   ```

6. **Merge release branch back to dev** (if hotfixes were applied).

---

## Deployment Pipeline

### Flow: Code Push to Running on VPS

```
1. Developer pushes to release/* branch
       ↓
2. GitHub Actions triggers deploy.yml
       ↓
3. Build Docker images (API, Dashboard, Worker, Webhook Processor)
       ↓
4. Push images to ghcr.io/israeliyonsi/eaas-*
       ↓
5. SSH into Hetzner VPS
       ↓
6. Pull latest images from ghcr.io
       ↓
7. Run database migrations (dotnet ef database update)
       ↓
8. Docker Compose: pull + recreate changed containers (zero-downtime)
       ↓
9. Health check: curl http://localhost:5000/health
       ↓
10. If health check fails → automatic rollback to previous images
       ↓
11. Notify via webhook (success/failure)
```

### Environments

| Environment | Branch | URL | Purpose |
|------------|--------|-----|---------|
| Development | `dev` | localhost | Local Docker Compose |
| Production | `release/*` | mail.yourdomain.com | Hetzner VPS |

---

## Infrastructure Setup

### Hetzner VPS Specs (CX22)

- 2 vCPU (Intel)
- 4 GB RAM
- 40 GB SSD (NVMe)
- 20 TB traffic
- Location: Nuremberg or Helsinki

### Provisioning Checklist

- [ ] Create VPS via Hetzner Cloud Console.
- [ ] Assign static IPv4 address.
- [ ] Set up DNS: A record for `mail.yourdomain.com` -> VPS IP.
- [ ] Run `scripts/setup-vps.sh` on fresh VPS.
- [ ] Verify firewall rules (UFW: 22, 80, 443 only).
- [ ] Verify SSH key auth works, password auth disabled.
- [ ] Copy `.env` with production secrets to `/opt/eaas/.env`.
- [ ] Run `docker compose up -d` to start all services.
- [ ] Verify TLS certificate via Certbot.
- [ ] Test health endpoint: `curl https://mail.yourdomain.com/health`.

### Directory Structure on VPS

```
/opt/eaas/
  ├── docker-compose.yml
  ├── .env
  ├── nginx/
  │   └── nginx.conf
  ├── certbot/
  │   ├── conf/
  │   └── www/
  ├── backups/
  │   ├── daily/
  │   └── weekly/
  └── logs/
```

### Resource Allocation

| Service | Memory Limit | CPU Limit |
|---------|-------------|-----------|
| PostgreSQL 16 | 1 GB | 0.5 CPU |
| Redis 7 | 256 MB | 0.25 CPU |
| RabbitMQ 3.13 | 512 MB | 0.25 CPU |
| EaaS API | 512 MB | 0.5 CPU |
| EaaS Dashboard | 256 MB | 0.25 CPU |
| EaaS Worker | 256 MB | 0.25 CPU |
| Webhook Processor | 128 MB | 0.15 CPU |
| Nginx | 64 MB | 0.1 CPU |

Total: ~3 GB RAM, well within 4 GB VPS limit (leaves headroom for OS).

---

## Monitoring

### Health Checks

Each service exposes a `/health` endpoint:

| Service | Endpoint | Checks |
|---------|----------|--------|
| API | `GET /health` | DB connection, Redis ping, RabbitMQ connection |
| Dashboard | `GET /health` | Server responsive |
| Worker | `GET /health` | RabbitMQ consumer active |
| Webhook | `GET /health` | Listener active |

Docker Compose health checks restart unhealthy containers automatically:

```yaml
healthcheck:
  test: ["CMD", "curl", "-f", "http://localhost:5000/health"]
  interval: 30s
  timeout: 10s
  retries: 3
  start_period: 40s
```

### Uptime Monitoring

- Use [Uptime Kuma](https://github.com/louislam/uptime-kuma) (self-hosted on VPS) or free tier of [UptimeRobot](https://uptimerobot.com/).
- Monitor: API health, Dashboard availability, TLS certificate expiry.
- Alert via email + webhook on downtime.

### Log Aggregation

- Docker logs with JSON driver, rotated by Docker daemon.
- Application logs written to stdout/stderr (12-factor app).
- Docker log rotation config (`/etc/docker/daemon.json`):
  ```json
  {
    "log-driver": "json-file",
    "log-opts": {
      "max-size": "10m",
      "max-file": "3"
    }
  }
  ```
- View logs: `docker compose logs -f --tail=100 <service>`.
- For structured log search, pipe to a local Loki + Grafana stack (optional, post-MVP).

### Key Metrics to Watch

- API response time (p50, p95, p99).
- Email send success/failure rate.
- Queue depth (RabbitMQ management UI on port 15672).
- Database connection pool usage.
- Disk usage (especially backups directory).
- Memory usage per container.

---

## Backup Strategy

### PostgreSQL Backups

| Type | Frequency | Retention | Method |
|------|-----------|-----------|--------|
| Daily | Every day at 02:00 UTC | 7 days | `pg_dump` compressed |
| Weekly | Every Sunday at 03:00 UTC | 4 weeks | `pg_dump` compressed |

### Backup Script

Located at `scripts/backup-db.sh`. Runs via cron:

```cron
0 2 * * * /opt/eaas/scripts/backup-db.sh daily >> /opt/eaas/logs/backup.log 2>&1
0 3 * * 0 /opt/eaas/scripts/backup-db.sh weekly >> /opt/eaas/logs/backup.log 2>&1
```

### Backup Storage

- Primary: Local filesystem at `/opt/eaas/backups/`.
- Secondary (recommended): Upload to S3-compatible storage (e.g., Hetzner Object Storage, Backblaze B2).

### Restore Procedure

```bash
# Stop the API to prevent writes
docker compose stop api worker webhook

# Restore from backup
gunzip -c /opt/eaas/backups/daily/eaas_2026-03-27.sql.gz | \
  docker exec -i eaas-postgres psql -U eaas -d eaas

# Restart services
docker compose start api worker webhook
```

---

## Disaster Recovery

### Scenario: VPS Dies Completely

**RTO (Recovery Time Objective): < 1 hour**
**RPO (Recovery Point Objective): < 24 hours** (last daily backup)

### Recovery Steps

1. **Provision new VPS** on Hetzner Cloud (5 minutes).
2. **Run setup script**: `bash scripts/setup-vps.sh` (10 minutes).
3. **Copy production `.env`** from secure storage (password manager or encrypted backup).
4. **Restore database** from latest backup:
   - Download from S3/backup storage.
   - Run restore procedure above.
5. **Pull Docker images**: `docker compose pull` (5 minutes).
6. **Start services**: `docker compose up -d` (2 minutes).
7. **Update DNS** to point to new VPS IP (if changed).
8. **Verify health checks** and test key flows.
9. **Re-issue TLS certificate** via Certbot.

### What to Keep Off the VPS

Store these in a secure location (password manager, encrypted cloud storage):

- Production `.env` file with all secrets.
- AWS SES credentials.
- API key encryption key.
- Database backup files (replicate to S3).
- SSH private keys.

### Regular DR Testing

- Monthly: verify backup restores successfully to a test database.
- Quarterly: full DR drill on a fresh VPS.

---

## Security

### Firewall Rules (UFW)

```
Status: active

To                         Action      From
--                         ------      ----
22/tcp                     ALLOW       Anywhere      (SSH)
80/tcp                     ALLOW       Anywhere      (HTTP - redirect to HTTPS)
443/tcp                    ALLOW       Anywhere      (HTTPS)
```

All other ports are blocked. Internal services (PostgreSQL 5432, Redis 6379, RabbitMQ 5672/15672) are only accessible within the Docker network.

### SSH Hardening

Applied by `scripts/setup-vps.sh`:

- Password authentication disabled (`PasswordAuthentication no`).
- Root login disabled (`PermitRootLogin no`).
- Only SSH key authentication allowed.
- Non-default SSH port (optional, consider changing from 22).
- Fail2Ban installed to block brute-force attempts.

### Docker Security

- Run containers as non-root user where possible.
- Use `read_only: true` filesystem for stateless services.
- Set memory and CPU limits on all containers.
- Pin image versions (no `latest` tags in production).
- Scan images for vulnerabilities: `docker scout cves <image>`.
- Never store secrets in Docker images or Dockerfiles.

### Application Security

- All API keys hashed with SHA-256 before database storage.
- Rate limiting per tenant (configurable).
- Webhook signatures verified with HMAC-SHA256.
- HTTPS enforced via Nginx + Let's Encrypt.
- CORS restricted to known dashboard origin.
- Input validation on all API endpoints.
- SQL injection prevention via parameterized queries (EF Core).

### Secrets Management

- Production secrets stored in `.env` file on VPS (not in Git).
- `.env` file has `600` permissions (owner read/write only).
- GitHub Actions secrets used for CI/CD credentials.
- Rotate secrets quarterly (AWS keys, database passwords, API encryption key).

### Dependency Security

- Run `dotnet list package --vulnerable` regularly.
- Enable GitHub Dependabot alerts on the repository.
- Update dependencies at least monthly.
