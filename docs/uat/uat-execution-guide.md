# UAT Execution Guide

## Purpose

This guide defines how Sprint 16 UAT scenarios are prepared, executed, evidenced and triaged for the Personnel & Administrative Affairs Platform.

The canonical scenario register is `docs/uat/uat-scenario-catalog.csv`.

## Environment

The preferred UAT baseline is the local Docker Compose environment unless a dedicated UAT deployment is available.

- Web: `http://localhost:3000`
- API readiness: `http://localhost:8080/health/ready`
- API base: `http://localhost:8080`

Before a test session:

1. Pull the current `main` branch.
2. Start the platform with `docker compose up -d --build`.
3. Confirm `/health/ready` succeeds.
4. Complete MFA for the test administrator.
5. Create or confirm isolated UAT company/reference data.
6. Do not use production identities, real national IDs, real IBANs or real payroll values in screenshots or fixtures.

## Scenario states

Catalog definition states:

- `READY` — executable from the current product surface when prerequisites exist.
- `BLOCKED` — scenario definition is valid, but a required dependency is unavailable.
- `DRAFT` — definition still needs clarification before execution.

Execution result states used during UAT-002:

- `PASS`
- `FAIL`
- `BLOCKED`
- `NOT_RUN`

Do not change the catalog's definition `status` to store a test result. UAT-002 will keep execution results separately so the reusable scenario definition remains stable.

## Priority

- `P0` — release-critical. Any unresolved failure is a Go/No-Go blocker unless explicitly accepted by named business/technical approvers.
- `P1` — important operational coverage. Failures require triage and disposition before Go-Live.
- `P2` — lower-risk/extended coverage.

## Test types

- `POSITIVE` — expected successful business path.
- `NEGATIVE` — invalid data or rejected business path.
- `BOUNDARY` — date/time/range/state edge behavior.
- `AUTHORIZATION` — permission/scope separation.
- `IDEMPOTENCE` — safe repeated input/replay behavior.
- `SNAPSHOT` — historical value/configuration must remain stable after later changes.
- `RECONCILIATION` — source/prepared totals must agree with target/calculated totals.
- `END_TO_END` — crosses more than one lifecycle step or domain.

## Test personas

UAT should use separate accounts where role separation matters. Do not simulate every persona with one all-powerful administrator when the scenario is specifically about authorization or approval separation.

Suggested personas:

- Platform Admin
- HR Specialist / HR Operations
- Payroll Specialist / Reviewer / Approver
- Manager
- Camp / Meal Operator
- Asset Custodian / Admin Affairs
- Workflow Admin / Requester / Approver
- Restricted or company-scoped user

Exact role codes may differ from persona names. The tester must record the user/role used in the execution evidence.

## Evidence rules

Each execution should capture enough evidence to reproduce the result without leaking sensitive information.

Minimum evidence:

1. scenario ID;
2. test date/time and environment commit SHA;
3. tester/persona;
4. test data business keys (for example employee number, company code, request number) rather than database internals where possible;
5. observed result;
6. screenshot or response/result reference;
7. defect ID if failed;
8. retest result if a defect was fixed.

Never include in screenshots, GitHub issues, CI logs or shared evidence:

- MFA/TOTP secrets;
- passwords or refresh/access tokens;
- real national identity numbers;
- real IBANs;
- unmasked real salary values;
- raw sensitive migration payloads.

## Execution order

Use this order for a clean UAT workspace:

1. `AUTH-*` and `ORG-*`
2. `PER-*`
3. `ATT-*`
4. `OT-*`
5. `MEAL-*` and `CAMP-*`
6. `AST-*`
7. `WF-*`
8. `PAY-*`
9. `RBAC-*`
10. `E2E-*`

This sequence creates reference and operational data before payroll and cross-domain reconciliation.

## Defect severity

Use these severities in UAT-002:

- `S1` — security/privacy breach, data corruption/loss, platform unavailable, or payroll-critical financial corruption with no safe workaround.
- `S2` — critical business flow blocked, approval/authorization bypass, major reconciliation variance, or incorrect state transition with no acceptable workaround.
- `S3` — important defect with a safe workaround; limited scope and no data-integrity/security breach.
- `S4` — cosmetic/usability/documentation issue with no material business-impact risk.

Go/No-Go requires zero unresolved S1/S2 defects.

## Reconciliation discipline

For payroll, attendance, overtime, meal and camp scenarios, a screenshot alone is not sufficient. Record the prepared source values and compare them with calculated target values.

At minimum, E2E/payroll evidence should account for:

- planned minutes;
- worked minutes;
- paid leave minutes where applicable;
- approved overtime minutes;
- meal quantity and employer cost;
- camp nights and accommodation employer cost;
- compensation effective date and currency;
- calculated payroll result/revision.

Any unexplained difference is a failure until reconciled or explicitly dispositioned.

## UAT-001 exit criteria

UAT-001 is complete when:

- the catalog passes automated schema validation;
- all release-critical domains have P0 coverage;
- positive, negative and authorization coverage is present;
- cross-domain payroll reconciliation is represented;
- scenario routes reflect the current web product surface;
- execution/evidence/defect rules are documented.

Actual execution results, defects and retest evidence belong to UAT-002.
