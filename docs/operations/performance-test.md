# Performance Smoke Test

This runbook defines the Sprint 15 HARD-004 regression-oriented API performance smoke test.

## Purpose

The smoke test is intended to catch obvious latency or availability regressions before release. It is not a production capacity certification and must not be used to infer maximum supported users, terminal volume, payroll throughput, or infrastructure sizing.

The default target is the lightweight public endpoint:

`GET /api/v1/system/ping`

Default acceptance thresholds:

- 500 requests
- concurrency 25
- HTTP error rate <= 1%
- p95 response time <= 500 ms

These thresholds are intentionally conservative for CI/dev infrastructure. Production-like performance acceptance must be measured separately against representative data, realistic authenticated business flows, and production-like compute/database sizing.

## Local execution

Start the local platform:

```bash
./scripts/dev-up.sh
```

Run the baseline test:

```bash
bash scripts/perf/http-load.sh http://127.0.0.1:8080/api/v1/system/ping
```

Override the test profile with environment variables:

```bash
REQUESTS=1000 \
CONCURRENCY=50 \
MAX_ERROR_RATE=0.01 \
MAX_P95_MS=500 \
bash scripts/perf/http-load.sh http://127.0.0.1:8080/api/v1/system/ping
```

For an authenticated endpoint, provide a short-lived access token through `BEARER_TOKEN`. The script never prints the token:

```bash
BEARER_TOKEN='short-lived-access-token' \
REQUESTS=200 \
CONCURRENCY=10 \
bash scripts/perf/http-load.sh http://127.0.0.1:8080/api/v1/auth/me
```

Do not store access tokens in source control, workflow files, shell history, test output, or build artifacts.

## Result interpretation

The script prints total requests, success/failure counts, error rate, mean latency, p95 latency, throughput, and elapsed time. It exits non-zero when the configured error-rate or p95 threshold is exceeded, allowing it to act as a release gate.

A failure should be investigated together with API logs, PostgreSQL/Redis readiness, worker backlog, integration queue health, and recent schema/query changes. A single successful smoke run does not replace soak, spike, concurrency, or business-flow testing.

## GitHub Actions

The `performance-smoke` workflow is manual (`workflow_dispatch`) so routine pull requests are not slowed by Docker image builds and environment-dependent timing. Run it after material API/database changes and before a release candidate is promoted.

## HARD-004 completion path

The smoke harness establishes the repeatable baseline. Full HARD-004 closure should additionally record representative authenticated scenarios for the highest-risk flows (attendance ingestion, integration staging, reporting/export, payroll read/calculation boundaries), execute them against a production-like dataset, and document agreed service-level objectives and measured headroom.
