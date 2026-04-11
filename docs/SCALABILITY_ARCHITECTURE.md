# EaaS - Scalability Architecture

**Version:** 1.0
**Date:** 2026-04-01
**Author:** Senior Architect
**Target:** 100 customers, 100 million emails/month
**Status:** Architecture Specification

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Load Profile Analysis](#2-load-profile-analysis)
3. [Architecture Diagram](#3-architecture-diagram)
4. [Database Scaling Strategy](#4-database-scaling-strategy)
5. [Message Queue Scaling Strategy](#5-message-queue-scaling-strategy)
6. [API Scaling Strategy](#6-api-scaling-strategy)
7. [AWS SES Scaling](#7-aws-ses-scaling)
8. [S3 Scaling](#8-s3-scaling)
9. [Monitoring & Alerting](#9-monitoring--alerting)
10. [Cost Estimation](#10-cost-estimation)
11. [Deployment Strategies](#11-deployment-strategies)
12. [Capacity Planning Checklist](#12-capacity-planning-checklist)

---

## 1. Executive Summary

### Current State vs Target State

| Dimension | Current (Sprint 3) | Target (Scale) |
|-----------|-------------------|----------------|
| Customers | 1 (single-tenant) | 100 (multi-tenant) |
| Monthly volume | < 5,000 emails | 100,000,000 emails |
| API instances | 1 | 3 (auto-scale to 10) |
| Worker instances | 1 | 3 (auto-scale to 6) |
| Database | Single PostgreSQL | Partitioned PostgreSQL + PgBouncer |
| Queue | Single RabbitMQ | 3-node RabbitMQ cluster (quorum queues) |
| Cache | Single Redis | Redis Sentinel (master + replica + 3 sentinels) |
| Peak throughput | ~1 email/sec | ~170 operations/sec |

### Key Architectural Changes

1. **Database partitioning** -- Monthly range partitions on `emails`, `inbound_emails`, and `email_events` tables to keep query performance sub-second at 100M+ rows.
2. **Connection pooling** -- PgBouncer in transaction mode to multiplex 200+ application connections into 50 PostgreSQL server connections.
3. **RabbitMQ cluster** -- 3-node cluster with quorum queues for message durability and automatic failover.
4. **Redis Sentinel** -- Master/replica with 3 sentinels for automatic failover of rate limiting, caching, and session data.
5. **Horizontal API scaling** -- Stateless API behind Nginx load balancer, scaling from 3 to 10 instances based on CPU and request rate.
6. **SES warm-up and multi-region** -- Structured warm-up plan to reach 100M/month sending capacity with failover across AWS regions.

### Estimated Infrastructure Cost

**~$10,730/month** at full scale (100M emails/month). AWS SES sending fees dominate at ~$10,000/month. Infrastructure (compute, database, cache, queue) totals ~$730/month.

---

## 2. Load Profile Analysis

### Email Volume Distribution

```
Total monthly volume:           100,000,000 emails
Total customers:                100

Average per customer:           1,000,000 emails/month
Top 10% of customers (Pareto):  80% of volume = 80,000,000 emails
Top customer estimate:           ~10,000,000 emails/month
Bottom 50% of customers:         ~5% of volume = 50,000 each
```

### Throughput Calculations

```
Daily average:                  100M / 30 = 3,333,333 emails/day
Hourly average:                 3,333,333 / 24 = 138,889 emails/hour
Hourly peak (3x average):      ~416,667 emails/hour
Per-second average:             138,889 / 3,600 = ~39 emails/sec
Per-second peak (3x):          ~140 emails/sec

Inbound estimate (20% of outbound):
  Daily:                        666,667 inbound/day
  Per-second peak:              ~28 inbound/sec

Total peak throughput:          ~170 operations/sec (outbound + inbound)
```

### Storage Projections

```
Per 1,000,000 emails:
  PostgreSQL metadata:          ~2 GB (email records, events, headers)
  S3 raw MIME (inbound):        ~500 MB average (text-heavy emails)
  S3 attachments:               ~2 GB average (PDFs, images, docs)

At 100,000,000 emails/month:
  PostgreSQL:                   ~200 GB (growing ~200 GB/month without archival)
  S3 inbound MIME:              ~10 GB/month (20M inbound x ~500 bytes avg)
  S3 attachments:               ~200 GB/month
  S3 total (cumulative 12mo):   ~2.5 TB

PostgreSQL with archival:
  Hot (current + last month):   ~400 GB
  Warm (3-12 months):           Partitions on slower storage
  Cold (12+ months):            Archived to S3 / dropped
```

### API Request Volume

```
Send requests:                  100,000,000/month
List/Get requests (2x read):    200,000,000/month
Analytics queries:              10,000,000/month
Webhook dispatches:             ~150,000,000/month (bounces, deliveries, opens)
Total API requests:             ~310,000,000/month

Per-second average:             310M / (30 * 24 * 3600) = ~120 req/sec
Per-second peak (3x):          ~360 req/sec
```

---

## 3. Architecture Diagram

```
                                 Internet
                                    |
                    +---------------+---------------+
                    |               |               |
               DNS (Route53)   CloudFront       AWS SES
               A/AAAA/MX      (CDN for         (Outbound sending +
               records         attachments)      Inbound receiving)
                    |               |               |
                    +-------+-------+       +-------+-------+
                            |               |               |
                      [Nginx / ALB]     SNS Topics      S3 Buckets
                       Port 80/443     (bounce,         eaas-attachments
                            |           delivery,       eaas-inbound-emails
               +------------+------------+ complaint,
               |            |            | inbound)
          [API-1]      [API-2]      [API-3]     |
          (.NET 8)     (.NET 8)     (.NET 8)    |
               |            |            |      |
               +------------+------------+      |
                            |                   |
                            |           [Nginx / ALB]
                            |            Port 8081
                            |               |
                            |        +------+------+
                            |        |             |
                            |   [Webhook-1]   [Webhook-2]
                            |   (.NET 8)      (.NET 8)
                            |        |             |
               +------------+--------+-------------+
               |                     |
          [PgBouncer]         [RabbitMQ Cluster]
          Port 6432            (Quorum Queues)
          500 client conn      +------+------+
          50 server conn       |      |      |
               |            [Node1][Node2][Node3]
               |                     |
          [PostgreSQL]               |
          (Partitioned)     +--------+--------+
          Monthly ranges    |        |        |
          on emails,     [Worker-1][Worker-2][Worker-3]
          inbound_emails, (.NET 8)  (.NET 8)  (.NET 8)
          email_events    email-send email-send webhook-
                          + inbound  + inbound  dispatch
                                     |
                              [Redis Sentinel]
                              +------+------+
                              |      |      |
                           [Master][Replica][Sentinels x3]
                           Rate limits, API key cache,
                           template cache, analytics cache
```

### Network Zones

```
+--------------------------------------------------+
|  Frontend Network (public-facing)                 |
|  Nginx, API instances, Webhook Processors         |
+--------------------------------------------------+
        |                       |
+--------------------------------------------------+
|  Backend Network (internal)                       |
|  PostgreSQL, PgBouncer, RabbitMQ, Redis,          |
|  Workers, API instances, Webhook Processors       |
+--------------------------------------------------+
        |
+--------------------------------------------------+
|  Monitoring Network (isolated)                    |
|  Prometheus, Grafana                              |
+--------------------------------------------------+
```

---

## 4. Database Scaling Strategy

### 4.1 Table Partitioning

**Why:** The `emails` table at 100M rows makes sequential scans unusable. Index bloat causes write amplification. VACUUM operations block writes for extended periods. Deleting old data requires expensive row-by-row DELETE operations.

**How:** Monthly range partitions on timestamp columns. Each partition is a separate physical table with its own indexes, enabling partition pruning on date-filtered queries.

| Table | Partition Key | Strategy | Naming Convention |
|-------|--------------|----------|-------------------|
| `emails` | `created_at` | Monthly RANGE | `emails_2026_04` |
| `inbound_emails` | `received_at` | Monthly RANGE | `inbound_emails_2026_04` |
| `email_events` | `created_at` | Monthly RANGE | `email_events_2026_04` |

**Partition management:** Automated creation via cron job or scheduled SQL function.

```sql
-- Create partitions for the next 3 months
SELECT create_future_partitions(3);

-- Each partition covers one calendar month
CREATE TABLE emails_2026_04 PARTITION OF emails
  FOR VALUES FROM ('2026-04-01') TO ('2026-05-01');
```

A `DEFAULT` partition catches rows that fall outside defined ranges, preventing insert failures.

**Performance impact at 100M rows:**

| Query Pattern | Without Partitions | With Partitions |
|--------------|-------------------|-----------------|
| Emails by tenant + date range | Full table scan (~45s) | Single partition scan (~200ms) |
| Count emails this month | Seq scan 100M rows | Seq scan ~3.3M rows |
| Delete emails older than 12mo | DELETE ~70M rows (~hours) | DROP PARTITION (~instant) |
| VACUUM | Locks entire 100M row table | Per-partition, minimal lock time |

### 4.2 Connection Pooling (PgBouncer)

**Why:** Without pooling, each service instance opens its own connection pool.

```
3 API instances      x 20 connections = 60
3 Worker instances   x 20 connections = 60
2 Webhook Processors x 20 connections = 40
                                Total = 160 connections

PostgreSQL default max_connections = 100 --> EXCEEDED
```

PgBouncer sits between all application instances and PostgreSQL, multiplexing many client connections into a smaller pool of server connections.

**Configuration (from docker-compose.prod.yml):**

| Setting | Value | Purpose |
|---------|-------|---------|
| `POOL_MODE` | `transaction` | Connection returned after each transaction (max reuse) |
| `DEFAULT_POOL_SIZE` | 40 | Server connections per database |
| `MAX_CLIENT_CONN` | 500 | Maximum client connections accepted |
| `MAX_DB_CONNECTIONS` | 100 | Hard cap on PostgreSQL connections |
| `RESERVE_POOL_SIZE` | 10 | Extra connections for burst traffic |
| `RESERVE_POOL_TIMEOUT` | 3s | How long before reserve pool activates |

**Connection math with PgBouncer:**

```
App instances: 8 services x 20 client conns = 160 client connections to PgBouncer
PgBouncer: 40-50 server connections to PostgreSQL
Multiplexing ratio: ~3:1 (160 client : 50 server)

Transaction mode: connection held only during transaction (~5ms avg)
Effective throughput: 50 conns x (1000ms / 5ms) = 10,000 transactions/sec
Required at peak: ~360 req/sec --> headroom: 27x
```

### 4.3 Read Replicas (Future Enhancement)

When query load exceeds single-node capacity:

- **Streaming replication** to 1-2 read replicas (async, ~10ms lag)
- **Read routing:** Analytics queries, list endpoints, and dashboard queries routed to replicas
- **Write routing:** All INSERT/UPDATE/DELETE operations routed to primary
- **Implementation:** Connection string switching at the application layer via EF Core interceptor or separate `DbContext` for reads

**When to implement:** When PostgreSQL primary CPU consistently exceeds 70% during peak hours, or P99 read latency exceeds 500ms.

### 4.4 Archival Strategy

Data lifecycle tiers:

| Tier | Age | Storage | Access Pattern | Query Performance |
|------|-----|---------|---------------|-------------------|
| **Hot** | Current month + last month | Primary PostgreSQL (SSD) | Real-time queries, API access | < 200ms |
| **Warm** | 3-12 months | Primary PostgreSQL (partitioned) | Occasional queries, reporting | < 2s |
| **Cold** | 12+ months | Archived to S3 (Parquet export) | Compliance, audit, rare access | Minutes (requires import) |

**Archival process:**

1. Monthly cron job exports cold partitions to S3 as Parquet files
2. Cold partitions detached from parent table (`ALTER TABLE ... DETACH PARTITION`)
3. Detached partition dropped after successful S3 export verification
4. Per-tenant `retention_days` setting controls when data becomes archivable

**Storage savings:**

```
Without archival (12 months): 200 GB/month x 12 = 2.4 TB PostgreSQL
With archival (2 months hot):  200 GB/month x 2  = 400 GB PostgreSQL + S3
S3 cost for 2 TB Parquet:     ~$46/month (Standard) → ~$4/month (Glacier)
```

---

## 5. Message Queue Scaling Strategy

### 5.1 RabbitMQ Cluster

**Topology:** 3-node cluster with quorum queues (Raft consensus protocol).

| Property | Value |
|----------|-------|
| Nodes | 3 (`rabbitmq-1`, `rabbitmq-2`, `rabbitmq-3`) |
| Queue type | Quorum queues (replicated across all 3 nodes) |
| Consensus | Raft (majority of nodes must acknowledge each message) |
| Failover | Automatic -- if 1 node dies, 2 remaining form quorum |
| Max tolerable failures | 1 node (quorum = 2 of 3) |
| Memory per node | 1 GB limit |
| Erlang cookie | Shared secret for cluster formation |

**Why quorum queues over classic mirrored queues:**

- Quorum queues use Raft consensus -- stronger consistency guarantees
- Classic mirrored queues are deprecated in RabbitMQ 3.13+
- Quorum queues handle network partitions more predictably
- Better performance under concurrent producers/consumers

### 5.2 Queue Design

| Queue | Purpose | Prefetch | Concurrency | DLQ |
|-------|---------|----------|-------------|-----|
| `eaas-emails-send` | Main outbound email dispatch | 50 | 25 | `eaas-emails-send-dlq` |
| `eaas-emails-send-priority` | Priority outbound (OTP, password resets, transactional) | 20 | 10 | `eaas-emails-send-priority-dlq` |
| `eaas-webhook-dispatch` | Webhook delivery to customer endpoints | 30 | 15 | `eaas-webhook-dispatch-dlq` |
| `eaas-inbound-process` | Inbound email MIME parsing and storage | 20 | 10 | `eaas-inbound-process-dlq` |

**Priority queue rationale:** A customer sending a 500K batch should not block password reset emails for another customer. The priority queue is processed by dedicated consumers with lower concurrency but guaranteed capacity.

**Dead letter queue (DLQ) policy:**

- Messages moved to DLQ after 3 delivery attempts
- DLQ messages retained for 7 days
- Monitoring alert triggers when DLQ depth > 100
- Manual replay via management UI or API

### 5.3 Consumer Tuning

**Outbound email throughput math:**

```
Target throughput:              140 emails/sec (peak)

Worker instances:               3
Consumers per worker:           25 (concurrency on eaas-emails-send)
Total concurrent consumers:     3 x 25 = 75

Average processing time per email:
  - Template rendering:         ~20ms
  - SES API call:               ~150ms
  - DB status update:           ~30ms
  Total:                        ~200ms

Theoretical throughput:         75 x (1000ms / 200ms) = 375 emails/sec
Required throughput:            140 emails/sec
Headroom ratio:                 375 / 140 = 2.7x

Verdict: HEALTHY -- 2.7x headroom accommodates SES latency spikes
         and garbage collection pauses without degradation.
```

**Inbound email throughput math:**

```
Target throughput:              28 inbound/sec (peak)

Consumer concurrency:           10 (across 3 workers)
Average processing time:
  - S3 download (raw MIME):     ~100ms
  - MimeKit parsing:            ~50ms
  - Attachment extraction:      ~100ms (varies by count/size)
  - DB insert:                  ~30ms
  - Webhook dispatch:           ~20ms (enqueue only)
  Total:                        ~300ms

Throughput:                     10 x (1000ms / 300ms) = 33 inbound/sec
Headroom:                       33 / 28 = 1.2x

Verdict: TIGHT -- scale to 15 concurrency if inbound exceeds 20%
         of outbound, or add a 4th worker instance.
```

**Webhook dispatch throughput math:**

```
Target throughput:              ~50 webhooks/sec (bounces + deliveries + opens)

Consumer concurrency:           15 (dedicated worker-3)
Average processing time:
  - HTTP POST to customer:      ~500ms (network-bound, highly variable)
  - Retry logic:                exponential backoff (not counted in avg)
  Total:                        ~500ms avg

Throughput:                     15 x (1000ms / 500ms) = 30 webhooks/sec

Verdict: MAY NEED TUNING -- if webhook endpoints are slow (>1s avg),
         throughput drops. Mitigation: increase concurrency to 30,
         implement circuit breaker per endpoint.
```

---

## 6. API Scaling Strategy

### 6.1 Horizontal Scaling

The EaaS API is fully stateless -- no server-side sessions, no local file storage, no in-memory state beyond request scope. This enables clean horizontal scaling.

**Load balancing configuration (Nginx):**

```nginx
upstream eaas_api {
    least_conn;          # Route to instance with fewest active connections
    server api-1:8080;
    server api-2:8080;
    server api-3:8080;
}

upstream eaas_webhooks {
    least_conn;
    server webhook-processor-1:8081;
    server webhook-processor-2:8081;
}
```

**Scaling policy (Kubernetes HPA):**

| Metric | Target | Min Replicas | Max Replicas |
|--------|--------|-------------|-------------|
| CPU utilization | 70% | 3 | 10 |
| Requests per second | 120/instance | 3 | 10 |
| Memory utilization | 80% | 3 | 10 |

**Scaling math:**

```
Peak: 360 req/sec
Per-instance capacity: ~120 req/sec (based on t3.medium benchmarks)
Minimum instances: 360 / 120 = 3
With N+1 redundancy: 4 instances
Max scale (10 instances): 1,200 req/sec capacity
```

### 6.2 Rate Limiting

Rate limiting is enforced via Redis sliding window counters, ensuring consistent limits across all API instances.

**Tier structure:**

| Plan | Requests/sec | Emails/month | Burst (10s) |
|------|-------------|-------------|-------------|
| Starter | 10 req/sec | 100,000 | 20 req/sec |
| Growth | 50 req/sec | 1,000,000 | 100 req/sec |
| Enterprise | 200 req/sec | 10,000,000 | 400 req/sec |
| Custom | Configurable | Configurable | 2x sustained |

**Implementation:**

```
Key format:   ratelimit:{tenant_id}:{window_start}
Window size:  1 second (sliding)
Storage:      Redis INCR + EXPIRE
Response:     429 Too Many Requests with Retry-After header
Headers:      X-RateLimit-Limit, X-RateLimit-Remaining, X-RateLimit-Reset
```

**Burst handling:** Tenants may exceed their sustained rate by 2x for up to 10 seconds. This accommodates legitimate spikes (e.g., batch send start) without penalizing customers. After 10 seconds, strict enforcement resumes.

### 6.3 Caching Strategy

All caching uses Redis with per-key TTLs. Cache invalidation is event-driven where possible.

| Data | Cache Key | TTL | Invalidation | Impact |
|------|-----------|-----|-------------|--------|
| API key authentication | `auth:{api_key_hash}` | 5 min | On key rotation/revocation | Eliminates DB query on every request |
| Template rendering | `template:{template_id}:{hash(variables)}` | 10 min | On template update | Avoids re-parsing Liquid on repeated sends |
| Inbound rules | `inbound_rules:{tenant_id}` | 10 min | On rule update | Avoids DB query per inbound email |
| Domain verification | `domain:{domain}:verified` | 30 min | On DNS verification change | Avoids DNS/DB lookup on every send |
| Analytics aggregates | `analytics:{tenant_id}:{period}` | 1 min | Time-based expiry | Prevents repeated heavy aggregation queries |
| Tenant configuration | `tenant:{tenant_id}:config` | 15 min | On tenant settings update | Plan limits, features, preferences |

**Cache hit rate targets:**

```
API key auth:        > 95% hit rate (keys rarely change)
Templates:           > 80% hit rate (popular templates reused)
Inbound rules:       > 90% hit rate (rules rarely change)
Domain verification: > 99% hit rate (verified once, cached long)
Analytics:           > 70% hit rate (dashboards polling same data)
```

**Estimated DB load reduction from caching:**

```
Without caching: 360 req/sec x 2-3 DB queries/req = 720-1080 queries/sec
With caching:    360 req/sec x 0.5 DB queries/req = ~180 queries/sec
Reduction:       ~75% fewer database queries
```

---

## 7. AWS SES Scaling

### 7.1 Sending Quotas

SES has account-level sending limits that must be proactively increased.

| Level | Daily Limit | Sending Rate | How to Get |
|-------|------------|-------------|------------|
| Sandbox | 200 emails/day | 1 email/sec | Default (new accounts) |
| Production (initial) | 50,000 emails/day | 14 emails/sec | Request via AWS console |
| Production (increased) | 1,000,000 emails/day | 200 emails/sec | Support ticket after warm-up |
| Production (high volume) | 10,000,000+ emails/day | 1,000+ emails/sec | TAM engagement, track record |

**Required for 100M emails/month:**

```
Daily target:     100M / 30 = 3,333,333 emails/day
Sending rate:     140 emails/sec (peak)
SES quota needed: 5,000,000 emails/day (1.5x headroom)
Rate needed:      200 emails/sec minimum
```

**Getting there:** AWS SES quota increases are manual and require demonstrated sending reputation. You cannot jump from sandbox to 5M/day. The warm-up plan below is mandatory.

### 7.2 Multi-Region Strategy

| Region | Role | Use Case |
|--------|------|----------|
| `eu-west-1` (Ireland) | Primary | All outbound sending, inbound receiving |
| `us-east-1` (Virginia) | Failover | Activated when primary region is degraded |

**Failover trigger conditions:**

- SES API error rate > 5% for 2 consecutive minutes
- SES API latency P99 > 5 seconds for 5 minutes
- AWS health dashboard reports `eu-west-1` SES degradation

**Failover implementation:**

```
1. Health check service monitors SES API from workers
2. On failure detection: update Redis flag `ses:active_region`
3. Workers read active region on each send
4. Switch SES client to failover region
5. Alert operations team
6. Manual switchback after primary recovery confirmed
```

**Considerations:**

- Each region has its own sending quota (must warm up both)
- Domain verification (DKIM, SPF) is per-region -- verify in both regions
- SES inbound receiving is only available in select regions

### 7.3 Warm-up Plan

Sending reputation is critical. ISPs (Gmail, Outlook, Yahoo) throttle or block senders that ramp too quickly. This plan builds reputation over 6-8 weeks.

| Week | Daily Volume | Cumulative Monthly | Notes |
|------|-------------|-------------------|-------|
| 1 | 10,000/day | 70,000 | Transactional only (high engagement) |
| 2 | 25,000/day | 245,000 | Add low-volume marketing |
| 3 | 50,000/day | 595,000 | Monitor bounce rate (must stay < 3%) |
| 4 | 100,000/day | 1,295,000 | Request SES quota increase to 500K/day |
| 5 | 250,000/day | 3,045,000 | Onboard first batch of customers |
| 6 | 500,000/day | 6,545,000 | Request quota increase to 2M/day |
| 7 | 1,000,000/day | 13,545,000 | Scale workers to handle volume |
| 8 | 3,000,000/day | 34,545,000 | Request final quota increase |
| 9+ | Ramp to 3.3M/day | 100,000,000 | Full capacity, all customers onboarded |

**Reputation metrics to maintain during warm-up:**

| Metric | Maximum Allowed | Target |
|--------|----------------|--------|
| Bounce rate | < 5% (SES suspends at 10%) | < 2% |
| Complaint rate | < 0.1% (SES suspends at 0.5%) | < 0.05% |
| Delivery rate | > 95% | > 98% |

**Actions if metrics degrade:**

1. Immediately pause warm-up ramp
2. Identify and suppress bouncing addresses
3. Review sending content for spam signals
4. Wait 48 hours for metrics to recover
5. Resume at previous week's volume

---

## 8. S3 Scaling

### Capacity and Performance

S3 scales infinitely -- it is not a bottleneck. However, proper key design and lifecycle management are critical for cost control.

**Request rate limits (per prefix):**

| Operation | Limit |
|-----------|-------|
| GET/HEAD | 5,500 requests/sec |
| PUT/POST/DELETE | 3,500 requests/sec |

**Key design for load distribution:**

```
Outbound attachments:
  s3://eaas-attachments/{tenant_id}/{email_id}/{filename}

Inbound raw MIME:
  s3://eaas-inbound-emails/{tenant_id}/{year}/{month}/{day}/{message_id}.eml

Inbound extracted attachments:
  s3://eaas-inbound-emails/{tenant_id}/{year}/{month}/{day}/{message_id}/attachments/{filename}
```

Using `{tenant_id}` as the first path component distributes load across S3 internal partitions (S3 partitions by key prefix). With 100 tenants, requests naturally spread across 100+ prefixes -- well within S3 limits.

### Lifecycle Policies

| Bucket | Object Type | Standard | Infrequent Access | Glacier | Delete |
|--------|------------|----------|-------------------|---------|--------|
| `eaas-inbound-emails` | Raw MIME (.eml) | 0-30 days | 30-90 days | 90-365 days | 365 days |
| `eaas-inbound-emails` | Extracted attachments | 0-60 days | 60-180 days | 180-365 days | Per tenant policy |
| `eaas-attachments` | Outbound attachments | 0-60 days | 60-180 days | -- | 180 days |
| `eaas-db-archives` | PostgreSQL Parquet exports | 0-30 days | 30-90 days | 90+ days | Per retention policy |

**Cost projection at scale:**

```
Monthly new data:
  Inbound MIME:         ~10 GB
  Inbound attachments:  ~50 GB
  Outbound attachments: ~200 GB
  DB archives:          ~200 GB (compressed Parquet)
  Total:                ~460 GB/month new

After 12 months with lifecycle:
  Standard (0-60 days):         ~920 GB   @ $0.023/GB = ~$21/month
  Infrequent Access (60-180d):  ~1.4 TB   @ $0.0125/GB = ~$18/month
  Glacier (180-365d):           ~1.0 TB   @ $0.004/GB  = ~$4/month
  Total S3 storage:             ~$43/month

  GET/PUT requests:             ~10M/month @ $0.0004/1K = ~$4/month
  Data transfer (CloudFront):   ~50 GB/month             = ~$5/month

  Total S3 cost:                ~$52/month
```

### CloudFront CDN for Attachments

Attachment downloads served via CloudFront reduce S3 request costs and improve latency for global customers.

```
CloudFront distribution:
  Origin:           s3://eaas-attachments
  Cache behavior:   Cache on full URL (immutable attachments)
  TTL:              24 hours (attachments don't change)
  Access control:   Signed URLs (per-tenant, time-limited)
  Price class:      PriceClass_100 (US, Europe, Asia)
```

---

## 9. Monitoring & Alerting

### Metrics and Thresholds

| Metric | Source | Warning | Critical | Action |
|--------|--------|---------|----------|--------|
| API latency P99 | Prometheus | > 500ms | > 2s | Scale API instances |
| API error rate (5xx) | Prometheus | > 1% | > 5% | Page on-call, investigate |
| Queue depth (any) | RabbitMQ exporter | > 5,000 | > 50,000 | Scale workers |
| DLQ depth (any) | RabbitMQ exporter | > 100 | > 1,000 | Investigate failed messages |
| DB connection usage | PgBouncer stats | > 80% pool | > 95% pool | Increase pool size or scale |
| DB query latency P99 | Prometheus (EF Core) | > 200ms | > 1s | Check partition pruning, indexes |
| Disk usage (PostgreSQL) | Node exporter | > 70% | > 90% | Archive old partitions, expand disk |
| SES bounce rate | SES/SNS metrics | > 3% | > 5% | Pause sending, clean lists |
| SES complaint rate | SES/SNS metrics | > 0.05% | > 0.1% | Pause sending, review content |
| Worker consumer lag | Custom metric | > 30s | > 5min | Scale workers |
| Redis memory usage | Redis INFO | > 70% maxmemory | > 90% maxmemory | Review TTLs, increase memory |
| Redis replication lag | Redis INFO | > 1s | > 10s | Check replica health |
| Certificate expiry | Certbot/Prometheus | < 14 days | < 3 days | Renew certificates |

### Dashboards (Grafana)

**1. System Overview**

```
+---------------------------+---------------------------+
| Throughput (req/sec)      | Error Rate (%)            |
| [time series graph]       | [time series graph]       |
+---------------------------+---------------------------+
| API Latency P50/P95/P99   | Active Connections        |
| [time series graph]       | [gauge per service]       |
+---------------------------+---------------------------+
| Emails Sent (today)       | Emails Received (today)   |
| [single stat, big number] | [single stat, big number] |
+---------------------------+---------------------------+
```

**2. Queue Health**

```
+---------------------------+---------------------------+
| Queue Depth (all queues)  | Consumer Rate (msg/sec)   |
| [stacked area chart]      | [time series per queue]   |
+---------------------------+---------------------------+
| DLQ Size (all queues)     | Message Age (oldest)      |
| [bar chart]               | [gauge per queue]         |
+---------------------------+---------------------------+
| Publish Rate vs Consume   | Queue Memory Usage        |
| [dual-axis time series]   | [per-node breakdown]      |
+---------------------------+---------------------------+
```

**3. Database Health**

```
+---------------------------+---------------------------+
| Active Connections        | Query Latency P50/P99     |
| [gauge: used/total]       | [time series graph]       |
+---------------------------+---------------------------+
| Partition Sizes (top 10)  | Transactions/sec          |
| [bar chart, GB]           | [time series graph]       |
+---------------------------+---------------------------+
| PgBouncer Pool Usage      | Slow Queries (>100ms)     |
| [gauge per pool]          | [count, time series]      |
+---------------------------+---------------------------+
```

**4. SES & Delivery**

```
+---------------------------+---------------------------+
| Sending Rate (emails/sec) | Bounce Rate (%)           |
| [time series graph]       | [time series + threshold] |
+---------------------------+---------------------------+
| Delivery Rate (%)         | Complaint Rate (%)        |
| [single stat]             | [single stat + threshold] |
+---------------------------+---------------------------+
| SES Quota Used/Available  | Reputation Score          |
| [gauge]                   | [single stat]             |
+---------------------------+---------------------------+
```

**5. Per-Tenant Dashboard**

```
+---------------------------+---------------------------+
| Tenant Selector [dropdown]                            |
+---------------------------+---------------------------+
| Volume (sent/received)    | Error Rate (per tenant)   |
| [time series graph]       | [time series graph]       |
+---------------------------+---------------------------+
| Rate Limit Hits           | Bounce Rate (per tenant)  |
| [count, time series]      | [time series + threshold] |
+---------------------------+---------------------------+
| Top Templates (by volume) | Webhook Delivery Rate     |
| [table]                   | [single stat]             |
+---------------------------+---------------------------+
```

### Alerting Channels

| Severity | Channel | Response Time |
|----------|---------|--------------|
| Critical | PagerDuty / SMS | < 15 minutes |
| Warning | Slack #eaas-alerts | < 1 hour |
| Info | Grafana annotation | Next business day |

---

## 10. Cost Estimation

### Infrastructure Costs at 100M Emails/Month

| Resource | Specification | Instances | Monthly Cost |
|----------|--------------|-----------|-------------|
| API servers | t3.medium (2 vCPU, 4 GB) | 3 | ~$90 |
| Worker servers | t3.medium (2 vCPU, 4 GB) | 3 | ~$90 |
| Webhook Processors | t3.small (2 vCPU, 2 GB) | 2 | ~$30 |
| PostgreSQL | r6g.xlarge (4 vCPU, 32 GB) | 1 | ~$300 |
| PgBouncer | t3.micro (sidecar or dedicated) | 1 | ~$8 |
| RabbitMQ cluster | t3.medium (2 vCPU, 4 GB) | 3 | ~$90 |
| Redis master | t3.small (2 vCPU, 2 GB) | 1 | ~$15 |
| Redis replica | t3.small (2 vCPU, 2 GB) | 1 | ~$15 |
| Redis sentinels | t3.micro | 3 | ~$15 |
| S3 storage | Standard + IA + Glacier (~2.5 TB) | -- | ~$52 |
| CloudFront | Attachment CDN transfer | -- | ~$50 |
| ALB / Nginx | Load balancer | 1 | ~$25 |
| Prometheus + Grafana | t3.small | 1 | ~$15 |
| **Subtotal (infrastructure)** | | | **~$795** |

### AWS SES Costs

| Component | Unit Cost | Volume | Monthly Cost |
|-----------|----------|--------|-------------|
| Email sending | $0.10 per 1,000 emails | 100,000,000 | $10,000 |
| Dedicated IPs (optional) | $24.95/IP/month | 4 IPs | $100 |
| Inbound receiving | $0.10 per 1,000 emails | 20,000,000 | $2,000 |
| **Subtotal (SES)** | | | **~$12,100** |

### Total Monthly Cost

| Category | Cost |
|----------|------|
| Infrastructure | ~$795 |
| AWS SES (outbound) | ~$10,000 |
| AWS SES (inbound) | ~$2,000 |
| AWS SES (dedicated IPs) | ~$100 |
| **Total** | **~$12,895/month** |

### Cost per Email

```
Total cost:         $12,895/month
Total emails:       120,000,000 (100M outbound + 20M inbound)
Cost per email:     $0.000107 (~$0.107 per 1,000 emails)

Comparison:
  Resend:           $0.40 per 1,000 (3.7x more expensive)
  SendGrid:         $0.50 per 1,000 (4.7x more expensive)
  Postmark:         $1.00 per 1,000 (9.3x more expensive)
  EaaS self-hosted: $0.107 per 1,000
```

### Cost Scaling Curve

| Customers | Monthly Emails | SES Cost | Infra Cost | Total | Per 1K Emails |
|-----------|---------------|----------|-----------|-------|---------------|
| 10 | 10M | $1,200 | $400 | $1,600 | $0.16 |
| 25 | 25M | $3,000 | $500 | $3,500 | $0.14 |
| 50 | 50M | $6,000 | $650 | $6,650 | $0.13 |
| 100 | 100M | $12,100 | $795 | $12,895 | $0.11 |
| 200 | 200M | $24,100 | $1,200 | $25,300 | $0.10 |

Infrastructure costs scale sub-linearly. SES is the dominant and linear cost.

---

## 11. Deployment Strategies

### Docker Compose (Self-Hosted / VPS)

**Best for:** Small-to-medium deployments (< 10M emails/month), single-server or 2-3 server setups.

EaaS ships with `docker-compose.prod.yml` that includes the full production stack:

```
docker compose -f docker-compose.prod.yml --env-file docker-compose.prod.env up -d
```

**What's included:**

- Nginx load balancer (TLS termination, Let's Encrypt via Certbot)
- 3 API instances (round-robin)
- 3 Worker instances (competing consumers)
- 2 Webhook Processors
- PostgreSQL 16 + PgBouncer (transaction pooling)
- RabbitMQ 3-node cluster (quorum queues)
- Redis Sentinel (master + replica + 3 sentinels)
- Prometheus + Grafana (monitoring)

**Server sizing for Docker Compose:**

| Volume | Server(s) | Spec | Estimated Cost |
|--------|-----------|------|---------------|
| < 1M/month | 1 server | 4 vCPU, 16 GB, 200 GB SSD | ~$80/month |
| 1-10M/month | 1 server | 8 vCPU, 32 GB, 500 GB SSD | ~$160/month |
| 10-50M/month | 2-3 servers (Docker Swarm) | 8 vCPU, 32 GB each | ~$480/month |
| 50M+/month | Kubernetes recommended | See below | See below |

**Scaling limitations of Docker Compose:**

- No auto-scaling (must manually add instances)
- Single-host failure takes down everything (unless using Docker Swarm)
- No rolling updates without downtime (unless using Swarm mode)
- Manual partition management for PostgreSQL

### Kubernetes (Cloud / Enterprise)

**Best for:** Large deployments (10M+ emails/month), teams requiring auto-scaling, zero-downtime deployments, and managed infrastructure.

**Recommended managed services:**

| Component | Self-managed (K8s pods) | Managed Service |
|-----------|------------------------|-----------------|
| PostgreSQL | Not recommended | AWS RDS, Google Cloud SQL, Azure Database |
| Redis | Acceptable | AWS ElastiCache, Google Memorystore |
| RabbitMQ | Acceptable | CloudAMQP, Amazon MQ |
| Monitoring | Prometheus + Grafana (in-cluster) | Datadog, New Relic, Grafana Cloud |

**Kubernetes resource definitions:**

```yaml
# API Deployment
apiVersion: apps/v1
kind: Deployment
metadata:
  name: eaas-api
spec:
  replicas: 3
  strategy:
    type: RollingUpdate
    rollingUpdate:
      maxUnavailable: 1
      maxSurge: 1
  template:
    spec:
      containers:
      - name: api
        resources:
          requests:
            cpu: 250m
            memory: 128Mi
          limits:
            cpu: "1"
            memory: 512Mi
---
# Horizontal Pod Autoscaler
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: eaas-api-hpa
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: eaas-api
  minReplicas: 3
  maxReplicas: 10
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 70
```

**Zero-downtime deployment:**

1. Rolling update strategy (1 pod at a time)
2. Readiness probes prevent traffic to starting pods
3. Liveness probes restart unhealthy pods
4. PodDisruptionBudget ensures minimum 2 API pods always running

---

## 12. Capacity Planning Checklist

### Pre-Production (Must Complete)

- [ ] **SES production access** -- Move out of sandbox, verify sending domain in production SES
- [ ] **SES sending rate increase** -- Request increase to target rate (200+ emails/sec)
- [ ] **SES warm-up executed** -- Complete 6-8 week warm-up plan, bounce rate < 2%
- [ ] **SES multi-region setup** -- Verify domain in failover region (`us-east-1`), test failover
- [ ] **Database partitions created** -- Generate partitions for next 12 months via `create_future_partitions(12)`
- [ ] **Partition automation** -- Cron job to create partitions 3 months ahead, alert if missing
- [ ] **PgBouncer deployed** -- All application connection strings point to PgBouncer (port 6432)
- [ ] **PgBouncer load tested** -- Verify 160+ client connections with transaction pooling
- [ ] **RabbitMQ cluster formed** -- 3 nodes joined, quorum queues created on all
- [ ] **RabbitMQ failover tested** -- Kill node 1, verify messages still flow on nodes 2+3
- [ ] **Redis Sentinel configured** -- Master, replica, 3 sentinels running
- [ ] **Redis failover tested** -- Kill master, verify sentinel promotes replica within 30s

### Monitoring & Alerting (Must Complete)

- [ ] **Prometheus deployed** -- Scraping all services (API, Worker, Webhook, PgBouncer, RabbitMQ, Redis)
- [ ] **Grafana dashboards deployed** -- System Overview, Queue Health, Database, SES, Per-Tenant
- [ ] **Alert rules configured** -- All thresholds from Section 9 configured with correct channels
- [ ] **PagerDuty integration** -- Critical alerts trigger PagerDuty (or equivalent)
- [ ] **Runbooks written** -- Documented response procedure for each critical alert

### Load Testing (Must Complete)

- [ ] **Sustained throughput test** -- 140 emails/sec for 1 hour, no errors, P99 < 2s
- [ ] **Peak burst test** -- 300 emails/sec for 5 minutes, graceful degradation (queuing, not errors)
- [ ] **API stress test** -- 360 req/sec for 30 minutes, no 5xx errors
- [ ] **Database query test** -- Verify partition pruning on 100M row dataset (EXPLAIN ANALYZE)
- [ ] **Connection exhaustion test** -- 500 concurrent API connections, PgBouncer handles gracefully

### Failover Testing (Must Complete)

- [ ] **Kill API instance** -- Nginx routes to remaining instances, no client errors
- [ ] **Kill Worker instance** -- Messages rebalance to remaining workers, no message loss
- [ ] **Kill RabbitMQ node** -- Quorum maintained on 2 nodes, consumers reconnect
- [ ] **Kill Redis master** -- Sentinel promotes replica, application reconnects
- [ ] **Kill PgBouncer** -- Application reconnects on restart (or deploy PgBouncer HA)
- [ ] **SES region failover** -- Simulate `eu-west-1` degradation, verify switch to `us-east-1`

### Backup & Recovery (Must Complete)

- [ ] **PostgreSQL backup** -- Automated daily backups (pg_dump or WAL archiving)
- [ ] **Backup restore tested** -- Restore from backup to separate instance, verify data integrity
- [ ] **S3 versioning enabled** -- Protect against accidental deletion
- [ ] **S3 cross-region replication** -- Critical buckets replicated to failover region
- [ ] **RabbitMQ definitions exported** -- Queue/exchange/binding configuration backed up
- [ ] **Recovery time tested** -- Full system recovery from scratch < 2 hours

### Security (Must Complete)

- [ ] **TLS everywhere** -- All public endpoints use TLS 1.2+ (Nginx terminates)
- [ ] **Internal network isolated** -- Backend network not accessible from internet
- [ ] **Secrets management** -- All credentials in environment variables or secrets manager (not in code)
- [ ] **API key hashing** -- API keys stored as SHA-256 hashes, never plaintext
- [ ] **Rate limiting active** -- Per-tenant rate limits enforced, tested under load
- [ ] **SNS signature validation** -- Webhook processor validates AWS SNS message signatures

---

## Appendix A: Quick Reference

### Key Ports

| Service | Port | Network |
|---------|------|---------|
| Nginx (HTTP) | 80 | Frontend |
| Nginx (HTTPS) | 443 | Frontend |
| API | 8080 | Frontend + Backend |
| Webhook Processor | 8081 | Frontend + Backend |
| PostgreSQL | 5432 | Backend |
| PgBouncer | 6432 | Backend |
| RabbitMQ (AMQP) | 5672 | Backend |
| RabbitMQ (Management) | 15672 | Backend (localhost only) |
| Redis | 6379 | Backend |
| Redis Sentinel | 26379 | Backend |
| Prometheus | 9090 | Monitoring |
| Grafana | 3000 | Monitoring |

### Key Environment Variables

| Variable | Purpose |
|----------|---------|
| `POSTGRES_PASSWORD` | PostgreSQL and PgBouncer authentication |
| `REDIS_PASSWORD` | Redis master and replica authentication |
| `RABBITMQ_USER` | RabbitMQ cluster authentication |
| `RABBITMQ_PASSWORD` | RabbitMQ cluster authentication |
| `RABBITMQ_ERLANG_COOKIE` | RabbitMQ cluster formation secret |
| `AWS_REGION` | Primary SES region |
| `AWS_ACCESS_KEY_ID` | SES API authentication |
| `AWS_SECRET_ACCESS_KEY` | SES API authentication |
| `DOMAIN` | Public domain for webhook URLs and tracking |
| `GRAFANA_ADMIN_USER` | Grafana admin login |
| `GRAFANA_ADMIN_PASSWORD` | Grafana admin login |

### Scaling Decision Tree

```
Problem: API latency increasing
  └── Check: CPU > 70%?
      ├── Yes → Scale API instances (HPA or manual)
      └── No → Check: DB query latency > 200ms?
          ├── Yes → Check partition pruning (EXPLAIN ANALYZE)
          │   ├── Pruning works → Add read replica
          │   └── Not pruning → Fix query to include date filter
          └── No → Check: Redis latency > 10ms?
              ├── Yes → Check Redis memory, eviction policy
              └── No → Profile application code

Problem: Queue depth growing
  └── Check: Consumer count healthy?
      ├── No → Restart workers, check connectivity
      └── Yes → Check: Processing time increased?
          ├── Yes → Check SES latency (outbound) or S3 latency (inbound)
          │   └── SES throttling? → Back off, check quota
          └── No → Scale worker instances

Problem: SES bounce rate spiking
  └── Check: Which tenant?
      ├── Single tenant → Suppress bad addresses, notify tenant
      └── Multiple tenants → Check SES reputation dashboard
          └── Pause sending if > 5%, investigate list hygiene
```
