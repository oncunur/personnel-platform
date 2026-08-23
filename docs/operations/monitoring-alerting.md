# Monitoring and Alerting Runbook

This runbook defines the Sprint 15 HARD-005 operational health baseline.

## Signals already exposed by the platform

The API exposes three low-cost platform probes:

- `GET /health/live` — process liveness.
- `GET /health/ready` — readiness, including PostgreSQL and Redis checks.
- `GET /api/v1/system/ping` — API request-path smoke probe with trace id.

Integration operations also expose an authorized, company-scoped endpoint:

- `GET /api/v1/integrations/monitoring?companyId={companyId}`

The integration monitor reports system/device health plus total backlog, errors, and dead-letter counts. Access requires `integration.monitor.view` and the caller's company scope.

## Synthetic health watch

Run the public platform probes locally:

```bash
bash scripts/ops/health-watch.sh http://127.0.0.1:8080
```

Enable integration business-health checks with a short-lived token and company id:

```bash
BEARER_TOKEN='short-lived-access-token' \
COMPANY_ID='00000000-0000-0000-0000-000000000000' \
MAX_INTEGRATION_BACKLOG=500 \
MAX_INTEGRATION_ERRORS=50 \
MAX_INTEGRATION_DEAD_LETTERS=0 \
bash scripts/ops/health-watch.sh https://personnel.example.com
```

The script exits non-zero when a required probe fails or an integration threshold is exceeded. It never prints the bearer token.

## Scheduled GitHub monitor

The `operations-health` workflow runs every 15 minutes and can also be dispatched manually. Configure these repository variables/secrets before enabling it for a deployment:

| Name | Type | Purpose |
| --- | --- | --- |
| `MONITOR_BASE_URL` | Variable | Deployment base URL, for example `https://personnel.example.com`. Required for the job to run. |
| `MONITOR_COMPANY_ID` | Variable | Optional company id for integration business-health checks. |
| `MONITOR_BEARER_TOKEN` | Secret | Optional short-lived/service access token with `integration.monitor.view` and only the required company scope. |
| `MAX_INTEGRATION_BACKLOG` | Variable | Backlog alert threshold. Default `500`. |
| `MAX_INTEGRATION_ERRORS` | Variable | Error-count alert threshold. Default `50`. |
| `MAX_INTEGRATION_DEAD_LETTERS` | Variable | Dead-letter alert threshold. Default `0`. |

A failed scheduled workflow is the first alerting channel. Production rollout should route GitHub Actions failure notifications or the equivalent deployment monitor to the named on-call/operations owner.

Do not use an administrator token for monitoring. Prefer a dedicated read-only service identity with the minimum permission and scope required.

## Minimum release dashboard

Before production Go/No-Go, the operating dashboard should make these signals visible together:

- API availability and p95 latency.
- PostgreSQL readiness, connection pressure, storage growth, and slow-query trend.
- Redis readiness and memory/eviction trend.
- Worker execution/heartbeat freshness and recurring worker failures.
- Integration queue backlog, business errors, technical errors, and dead letters.
- Integration device last-seen/last-error state.
- Notification/report export/workflow SLA queue age where applicable.
- Business reconciliation signals for attendance, payroll, imports, and exports.

The current health-watch provides the deployable synthetic and integration-health baseline. Infrastructure dashboards (for example, the hosting provider plus PostgreSQL/Redis metrics) should consume the same alert ownership and severity model rather than creating an independent incident path.

## Severity baseline

- **S1 / Critical:** API unavailable, database unavailable, data-loss indication, restore required, widespread authentication failure.
- **S2 / High:** sustained readiness failure, dead-letter growth, integration processing stopped, critical worker repeatedly failing, payroll/reconciliation blocker.
- **S3 / Medium:** backlog/error thresholds exceeded without service outage, latency regression, stale device/worker signal.
- **S4 / Low:** isolated transient errors and non-urgent capacity warnings.

For S1/S2, alert ownership and escalation must be named before Go-Live. Alert acknowledgement alone does not close an incident; the underlying business reconciliation must also be checked when data processing was interrupted.
