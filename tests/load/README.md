# EaaS Load Tests

Load testing scripts using [k6](https://k6.io/).

## Prerequisites

```bash
# Install k6
# Windows: choco install k6
# macOS: brew install k6
# Linux: https://grafana.com/docs/k6/latest/set-up/install-k6/
```

## Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `EAAS_BASE_URL` | `http://localhost:5000` | API base URL |
| `EAAS_API_KEY` | `test-api-key-for-load-testing` | API key for auth |

## Scenarios

```bash
# Smoke test (1 VU, 30s) — sanity check
k6 run scenarios/smoke.js

# Load test (50 VUs, 17min) — steady state
k6 run scenarios/load.js

# Stress test (200 VUs, 13min) — find breaking point
k6 run scenarios/stress.js

# Throughput target (140 req/s, 5min) — prove API ingestion rate
k6 run scenarios/throughput-target.js
```

## Thresholds

| Scenario | p95 Latency | p99 Latency | Error Rate |
|----------|-------------|-------------|------------|
| Smoke | < 1000ms | — | < 5% |
| Load | < 500ms | < 2000ms | < 1% |
| Stress | < 2000ms | — | < 10% |
| Throughput | < 500ms | < 2000ms | < 1% |

## Note on 140 emails/sec Target

The throughput target measures **API ingestion rate** (HTTP → message queue), not end-to-end SES delivery rate which is governed by `MaxSendRate` in appsettings.json.
