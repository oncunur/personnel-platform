# Sprint 16 — UAT & Migration

## Goal

Prepare the Personnel Platform for controlled legacy-data migration, business UAT, cutover rehearsal, and Go/No-Go evidence without bypassing authorization, encryption, reconciliation, or operational controls introduced in earlier sprints.

## Workstream

| ID | Item | Status | Exit evidence |
| --- | --- | --- | --- |
| MIG-001 | Legacy data inventory & target mapping | IN PROGRESS | Inventory register, target-domain matrix, migration decisions, source owners |
| MIG-002 | Field mapping & transformation rules | IN PROGRESS | Canonical target catalog, transformation engine, field-level mappings, code/value dictionaries, validation rules |
| MIG-003 | Migration staging & validation harness | DONE | Persistent encrypted staging, row errors, lineage/idempotence, reconciliation output, PostgreSQL smoke evidence |
| MIG-004 | Migration dry run #1 | IN PROGRESS | Synthetic technical baseline, counts, duration, idempotence replay and sanitized evidence; real-source totals/defects still required |
| MIG-005 | Migration dry run #2 / cutover rehearsal | PLANNED | Repeatable clean run, measured RTO/RPO-adjacent cutover timing, sign-off |
| UAT-001 | UAT scenario catalog | PLANNED | Role-based and end-to-end business scenarios |
| UAT-002 | UAT execution & defect triage | PLANNED | Test results, severity, retest evidence |
| UAT-003 | Payroll/attendance/meal/camp reconciliation | PLANNED | Approved cross-system totals and sample-level reconciliation |
| CUT-001 | Cutover runbook | PLANNED | Freeze, extract, load, validate, switch, rollback steps and owners |
| CUT-002 | Go/No-Go checklist | PLANNED | Named approvers, blockers, rollback criteria, operational readiness evidence |

## Migration principles

1. **No blind database copy.** Legacy records are classified, mapped, transformed, validated, and reconciled before acceptance.
2. **Stable lineage.** Every migrated business record must remain traceable to its source system/object/key during migration and reconciliation.
3. **Idempotent loading.** A repeated dry run must not create duplicate target records.
4. **Reference data first.** Companies, branches, departments, positions, projects, cost centers, employee types, calendars, camps, meal types, document types and equivalent reference objects are loaded before dependent transactions.
5. **Sensitive data stays protected.** Identity, IBAN and salary values must only enter the target through paths that preserve the platform's encryption-at-rest controls.
6. **Authorization is rebuilt, not copied.** Legacy users/roles/permissions are not trusted as target authorization state. Required user access is explicitly provisioned and scope-reviewed.
7. **Audit history is evidence, not target authorization state.** Historical audit trails that must be retained are archived/read-only unless a specific regulatory/business need requires structured migration.
8. **Business totals decide acceptance.** Technical row counts alone are insufficient for attendance, overtime, meals, camp stays, payroll, finance and ERP-related history.
9. **Cutover must be reversible.** Source freeze, backup, migration, validation, traffic switch and rollback criteria are documented before production migration.

## Initial target-domain sequence

1. Organization and master/reference data.
2. Personnel core and sensitive profile data.
3. Employee-project and other active assignments.
4. Documents and file references/content.
5. Leave opening balances and active/history records.
6. Attendance raw events, daily attendance and overtime history.
7. Camp and meal history.
8. Compensation/payroll history and opening state.
9. Assets, stock, vehicles and active assignments.
10. Administrative/finance records required for continuity.
11. Integration mappings/open queues only when explicitly needed.
12. New target users/roles/scopes and employee-user links after access review.

## MIG-002 implementation baseline

The repository contains:

- `docs/migration/target-field-catalog.csv` — target-side field contract derived from the current domain model.
- `docs/migration/field-mapping-rules.md` — transformation, lookup, masking and approval rules.
- `scripts/migration/transform_values.py` — deterministic canonical transformation functions.
- `scripts/migration/preview_transform.py` — masked source-to-target preview tool.
- synthetic employee source/mapping fixtures for CI validation.
- target catalog validation and transformation unit tests in the `migration-contracts` workflow.

The current baseline deliberately does not invent legacy field names or source-system semantics. Real source exports are still required to complete MIG-001 and approve source-specific MIG-002 mappings.

## MIG-003 implementation baseline

The staging harness provides:

- a dedicated `migration` PostgreSQL schema for runs, immutable staged rows, lineage records and reconciliation metrics.
- encrypted persistence of source and transformed JSON payloads using the existing sensitive-data protector.
- source-key and SHA-256 based `NEW` / `CHANGED` / `UNCHANGED` idempotence classification across runs.
- row outcomes `VALID` / `WARNING` / `ERROR` / `DUPLICATE` and run-level blocking rules.
- reconciliation metrics with explicit source value, target value, tolerance and mismatch evidence.
- separate `migration.view`, `migration.manage` and `migration.reconcile` permissions plus company-scope enforcement.
- version-based concurrency protection and audit events for state-changing API operations.
- domain unit tests and a PostgreSQL migration smoke workflow that verifies the schema, permissions, immutable triggers and EF migration history in an isolated Docker stack.

MIG-003 is technically complete. Real-source execution evidence is tracked under MIG-004 rather than changing the staging-harness boundary.

## MIG-004 synthetic technical baseline

The first dry-run baseline adds `scripts/migration/run_dry_run.py` and the `migration-dry-run-1` workflow.

It runs the complete control path against an isolated live API and PostgreSQL instance:

1. approved synthetic mapping and source transformation.
2. real authentication including bootstrap-admin MFA enrollment/completion.
3. dedicated test-company creation.
4. Run A creation, encrypted staging, validation and zero-tolerance technical reconciliation.
5. Run B replay of the exact same source and mapping.
6. proof that Run A classifies rows as `NEW` while Run B classifies the same rows `UNCHANGED` / `DUPLICATE`.
7. direct PostgreSQL checks for lineage `seen_count`, reconciliation matches and absence of known clear source values in ciphertext columns.
8. generation of a sanitized JSON evidence artifact containing hashes, counts, durations, run IDs and gate results only.

The expected verdict is `PASS_SYNTHETIC_TECHNICAL_BASELINE`. It intentionally sets `realSourceReady=false` and records blockers for real legacy inventory, source-specific mapping approval, business-total reconciliation and live target loading.

MIG-004 therefore remains `IN PROGRESS` until a representative real legacy extract is available and its counts, domain totals, defects, unresolved mappings and measured duration are reviewed by the business owner.

## Current boundary

MIG-001 defines what exists and where it should land. MIG-002 defines how approved source fields are normalized and previewed. MIG-003 provides controlled staging, lineage, idempotence, row-level validation and reconciliation evidence. MIG-004 now proves that those controls can run end-to-end and repeatably in a synthetic isolated environment, but it still does **not** authorize writes into live business tables or replace real-source/business reconciliation. Real inventory, approved mappings, representative volumes and business totals remain required before MIG-004 can close and MIG-005 cutover rehearsal can begin.
