# MIG-004 — Migration Dry Run #1

## Purpose

MIG-004 proves that the migration controls built in MIG-001 through MIG-003 can be executed as one repeatable flow before real legacy extracts are introduced.

The first repository baseline is deliberately **synthetic**. It is a technical readiness gate, not business migration sign-off.

## What the automated dry run executes

The `migration-dry-run-1` workflow starts an isolated PostgreSQL, Redis and API stack and then executes `scripts/migration/run_dry_run.py`.

The runner:

1. loads the approved synthetic employee field mapping.
2. transforms the synthetic legacy CSV using the MIG-002 transformation functions.
3. authenticates through the real API, including MFA enrollment/completion for the disposable bootstrap administrator.
4. creates or resolves a dedicated dry-run company.
5. creates migration Run A through `/api/v1/migrations/runs`.
6. stages source and transformed payloads through the real MIG-003 API; the application encrypts both payloads before persistence.
7. validates the run and records technical reconciliation metrics.
8. repeats the exact same source and mapping as Run B.
9. proves that Run B is classified `UNCHANGED` and the staged rows are `DUPLICATE`, rather than being treated as new records.
10. emits a sanitized evidence JSON artifact.

## Technical reconciliation metrics

Each run records three zero-tolerance metrics:

| Metric | Source side | Staged side |
| --- | --- | --- |
| `SOURCE_ROW_COUNT` | CSV data-row count | migration run total row count |
| `UNIQUE_SOURCE_KEY_COUNT` | distinct derived lineage keys | distinct staged source keys |
| `TRANSFORM_SUCCESS_COUNT` | rows without transform errors | staged non-error rows |

These metrics prove transport/transformation/staging consistency only. Payroll, attendance, meal, camp, finance and other business totals require a real legacy extract and remain separate acceptance gates.

## Idempotence acceptance

For the current two-row synthetic fixture:

- Run A must report `NEW=2`, `CHANGED=0`, `UNCHANGED=0`.
- Run B must report `NEW=0`, `CHANGED=0`, `UNCHANGED=2`.
- Run B must contain two `DUPLICATE` rows.
- both runs must finish `RECONCILED` with zero reconciliation mismatches.
- lineage records must have `seen_count=2` after the replay.

The workflow verifies these conditions both through the API response and directly in PostgreSQL.

## Sensitive-data rules

The runner needs clear source/transformed values in memory to call the staging API, but it does not print those payloads or place them in the evidence artifact.

The evidence safety guard rejects keys for access tokens, refresh tokens, MFA challenges/secrets, passwords and migration payloads.

The PostgreSQL CI check also verifies that known synthetic source values are not present inside the persisted staging ciphertext columns.

`migration-output/` remains Git-ignored. The CI artifact contains only sanitized evidence and is retained temporarily.

## Evidence output

Default local evidence path:

```text
migration-output/dry-run-1-evidence.json
```

The evidence contains:

- source and mapping SHA-256 hashes.
- row and mapping counts.
- migration run IDs and final states.
- execution duration.
- row-status and idempotence counts.
- reconciliation metrics and mismatches.
- technical gate results.
- explicit blockers that prevent synthetic evidence from being treated as real-source migration approval.

It never contains source rows, transformed rows, authentication credentials or protected payloads.

## Local execution

Start the API stack from the repository root:

```bash
docker compose up -d --build postgres redis api
```

Then execute:

```bash
python scripts/migration/run_dry_run.py \
  --source scripts/migration/fixtures/legacy-employees.csv \
  --mapping scripts/migration/fixtures/employee-field-mapping.csv \
  --source-system SYNTHETIC_HR \
  --source-object EMPLOYEE_EXPORT \
  --target-entity Employee
```

A fresh disposable local database can complete bootstrap-admin MFA enrollment automatically. If the administrator already has MFA enrolled, set `MIGRATION_TOTP_SECRET` in the local shell for this controlled test; do not commit it or put it in an evidence file.

## What is still required for real MIG-004 sign-off

The synthetic baseline does **not** close MIG-004. Real-source sign-off still requires:

- the actual legacy source inventory and extract owner from MIG-001.
- source-specific field mappings and lookup dictionaries approved under MIG-002.
- an agreed legacy extraction window and source freeze rules.
- real row counts and business totals.
- payroll/attendance/meal/camp or other domain-specific reconciliation as applicable.
- defect and unresolved-mapping logs with owners.
- measured execution duration on a representative data volume.
- business approval of all unexplained variances.

Until those inputs exist, the expected evidence verdict is `PASS_SYNTHETIC_TECHNICAL_BASELINE`, never `REAL_SOURCE_READY`.
