# Sprint 16 — UAT & Migration

## Goal

Prepare the Personnel Platform for controlled legacy-data migration, business UAT, cutover rehearsal, and Go/No-Go evidence without bypassing authorization, encryption, reconciliation, or operational controls introduced in earlier sprints.

## Workstream

| ID | Item | Status | Exit evidence |
| --- | --- | --- | --- |
| MIG-001 | Legacy data inventory & target mapping | IN PROGRESS | Inventory register, target-domain matrix, migration decisions, source owners |
| MIG-002 | Field mapping & transformation rules | IN PROGRESS | Canonical target catalog, transformation engine, field-level mappings, code/value dictionaries, validation rules |
| MIG-003 | Migration staging & validation harness | PLANNED | Repeatable import/staging run, row errors, idempotence, reconciliation output |
| MIG-004 | Migration dry run #1 | PLANNED | Counts/totals, defects, duration, unresolved mapping log |
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

The repository now contains:

- `docs/migration/target-field-catalog.csv` — target-side field contract derived from the current domain model.
- `docs/migration/field-mapping-rules.md` — transformation, lookup, masking and approval rules.
- `scripts/migration/transform_values.py` — deterministic canonical transformation functions.
- `scripts/migration/preview_transform.py` — masked source-to-target dry-run tool.
- synthetic employee source/mapping fixtures for end-to-end CI validation.
- target catalog validation and transformation unit tests in the `migration-contracts` workflow.

The current baseline deliberately does not invent legacy field names or source-system semantics. Real source exports are still required to complete MIG-001 and approve MIG-002 mappings.

## Current boundary

MIG-001 defines what exists and where it should land. MIG-002 now defines how approved source fields are normalized and previewed, but it does **not** yet authorize loading production data. Actual source inventory, source-specific mappings/lookups, migration staging, idempotent persistence, dry runs and business reconciliation remain required before cutover.
