# Migration staging & validation harness (MIG-003)

## Purpose

MIG-003 turns the MIG-001 inventory and MIG-002 field transformation contracts into a persistent, repeatable staging process. It deliberately stops before writing transformed business data into production target entities.

## Lifecycle

1. Create a migration run with company, source system/object, target entity, source file SHA-256 and mapping SHA-256.
2. Submit the complete transformed source batch to staging.
3. Each row receives a stable source key, lineage key and source-row SHA-256.
4. Source and transformed JSON payloads are encrypted with the platform `ISensitiveDataProtector` before persistence.
5. Existing lineage classifies the row as `NEW`, `CHANGED` or `UNCHANGED`.
6. Row-level transformation/validation issues produce `VALID`, `WARNING`, `ERROR` or `DUPLICATE` staging outcomes.
7. Validate the run. Any row error blocks the run.
8. Record business reconciliation metrics with source value, target value and tolerance.
9. The run becomes `RECONCILED` only when there are no blocking row errors and no reconciliation mismatches. Otherwise it remains `BLOCKED`.

## Security boundaries

- Clear-text source or transformed payloads are never returned by the staging API.
- Clear-text payloads are not stored in migration tables; only encrypted ciphertext is persisted.
- Hashes and lineage keys remain searchable to support idempotence without decrypting data.
- Migration stage rows and reconciliation evidence are immutable at the database layer.
- Migration permissions are separate from integration/import permissions:
  - `migration.view`
  - `migration.manage`
  - `migration.reconcile`
- Initial permissions are assigned only to the platform administrator role.
- All state-changing API operations write audit events.

## Idempotence contract

The lineage identity is scoped by:

`company + source system + source object + source key + target entity`

Classification:

| Prior lineage | Source hash | Classification | Staging outcome |
| --- | --- | --- | --- |
| none | any valid SHA-256 | `NEW` | valid/warning/error according to row issues |
| exists | same as last hash | `UNCHANGED` | `DUPLICATE` unless an explicit error exists |
| exists | different from last hash | `CHANGED` | valid/warning/error according to row issues |

A repeated run therefore does not silently look like new data. MIG-004 will use this evidence during dry-run reconciliation.

## Reconciliation examples

Recommended metric codes depend on the source object. Examples:

- `EMPLOYEE_COUNT`
- `ACTIVE_EMPLOYEE_COUNT`
- `ATTENDANCE_WORKED_MINUTES`
- `OVERTIME_APPROVED_MINUTES`
- `MEAL_CONSUMPTION_COUNT`
- `CAMP_STAY_DAYS`
- `PAYROLL_BASE_TOTAL`
- `PAYROLL_OVERTIME_TOTAL`
- `ASSET_ACTIVE_ASSIGNMENT_COUNT`

Counts normally use tolerance `0`. Financial tolerances must be explicitly approved and documented; unexplained payroll variance remains a Go/No-Go blocker.

## API surface

- `GET /api/v1/migrations/runs`
- `GET /api/v1/migrations/runs/{runId}`
- `GET /api/v1/migrations/runs/{runId}/rows`
- `POST /api/v1/migrations/runs`
- `POST /api/v1/migrations/runs/{runId}/stage`
- `POST /api/v1/migrations/runs/{runId}/validate`
- `POST /api/v1/migrations/runs/{runId}/reconcile`

Run `Version` is required for state-changing operations after creation to prevent concurrent migration operators from silently overwriting one another.

## Current boundary

MIG-003 provides staging, validation, lineage, idempotence and reconciliation evidence. It does **not** bind migrated rows to live target records or perform cutover. Target binding and production writes must be introduced only after source-specific mappings have been approved and a dry run has passed.