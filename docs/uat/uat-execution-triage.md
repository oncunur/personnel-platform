# UAT-002 Execution & Defect Triage

## Purpose

UAT-002 records real user acceptance executions against the reusable UAT-001 scenario catalog and turns results into traceable defect/retest and Go/No-Go evidence.

The repository provides contracts and synthetic validation fixtures. Synthetic CI evidence must never be counted as real business UAT completion.

## Files

Use the committed templates as schemas:

- `docs/uat/templates/uat-execution.csv`
- `docs/uat/templates/uat-defects.csv`

For real UAT, copy these templates into an approved UAT workspace. Do not commit real execution registers or screenshots if they contain personal, financial or environment-sensitive information.

Recommended local workspace:

```text
uat-results/
  executions.csv
  defects.csv
  evidence/
```

`uat-results/` and `uat-output/` are ignored by Git.

## Execution IDs and defect IDs

Recommended conventions:

- execution: `UAT-YYYYMMDD-001`
- defect: `UAT-DEF-001`

IDs are immutable once referenced by another row.

## Execution result semantics

- `PASS` — observed behavior satisfies the scenario expected result and evidence is available.
- `FAIL` — expected behavior is not met. A defect ID is mandatory.
- `BLOCKED` — scenario cannot be completed because a prerequisite/environment/business dependency is unavailable. It is not a pass.
- `NOT_RUN` — explicitly deferred/not executed.

A `PASS` or `FAIL` requires an `evidence_ref`. Evidence references should point to an approved evidence location or ticket/reference, not embed secrets or raw sensitive payloads.

## Required execution fields

For any result other than `NOT_RUN`, record:

- environment;
- exact tested commit SHA;
- tester or approved tester alias;
- persona/role used;
- start and completion timestamps in ISO-8601;
- observed result.

For `PASS` and `FAIL`, also record evidence reference.

## Defect lifecycle

Allowed states:

`OPEN → IN_PROGRESS → FIXED → RETEST_PENDING → CLOSED`

`ACCEPTED_RISK` is a separate disposition and requires explicit written disposition. It must not be used to hide an unresolved S1/S2 release blocker.

Severity contract:

- `S1` — security/privacy breach, data corruption/loss, platform unavailable, or payroll-critical financial corruption without a safe workaround.
- `S2` — release-critical business flow blocked, authorization bypass, major reconciliation variance, or incorrect state transition without an acceptable workaround.
- `S3` — important defect with a safe workaround and limited risk.
- `S4` — cosmetic/usability/documentation issue without material business risk.

## Retest contract

A failed execution creates or references a defect. After a fix:

1. create a new execution row for the same scenario;
2. populate `retest_of_execution_id` with the original failed execution ID;
3. populate the same `defect_id`;
4. execute the scenario again against the exact fixed commit;
5. record fresh evidence;
6. only set the defect to `CLOSED` when the retest execution is `PASS`;
7. write the passing retest ID into `retest_execution_id` on the defect row.

The validator rejects a `CLOSED` defect without a passing same-scenario retest.

## Validation

Schema-only validation of committed empty templates:

```bash
python scripts/uat/validate_execution.py
```

Validate real UAT registers:

```bash
python scripts/uat/validate_execution.py \
  --executions uat-results/executions.csv \
  --defects uat-results/defects.csv \
  --strict
```

`--strict` requires at least one execution row.

## Readiness summary

After validation succeeds:

```bash
python scripts/uat/summarize_execution.py \
  --executions uat-results/executions.csv \
  --defects uat-results/defects.csv \
  --output uat-output/uat-summary.json
```

The summary uses the latest execution for each scenario by absolute ISO-8601 timestamp and emits only aggregate counts/gates. It does not copy screenshot contents or raw test payloads.

Verdicts:

- `NO_GO` — at least one open S1/S2 defect or latest P0 failure exists.
- `NOT_READY` — no current S1/S2/P0 failure, but at least one P0 is blocked or not yet executed.
- `UAT_READY_FOR_SIGNOFF` — every P0 scenario's latest execution is PASS and there are zero open S1/S2 defects.

`UAT_READY_FOR_SIGNOFF` is evidence for business sign-off; it is not itself an automatic production deployment approval. Migration, reconciliation, operational readiness and cutover gates still apply.

## Evidence safety

Do not place the following in CSV rows, evidence names, GitHub issues, CI logs or committed screenshots:

- MFA/TOTP secrets;
- passwords;
- access or refresh tokens;
- real national identity numbers;
- real IBAN values;
- unmasked real salary values;
- raw sensitive migration payloads.

Use business-safe keys such as synthetic employee numbers, request numbers or approved aliases. If real UAT data is required, keep the evidence in the approved restricted workspace and reference it by an opaque evidence ID.

## Synthetic CI fixture

`scripts/uat/fixtures/` contains synthetic execution/defect rows used only to prove the contract logic. The fixture deliberately demonstrates:

- a PASS;
- an S2 FAIL;
- a fixed defect with same-scenario PASS retest;
- a BLOCKED cross-domain scenario;
- a resulting `NOT_READY` verdict rather than a false UAT completion.

No synthetic execution may be copied into the real UAT register as evidence of business acceptance.

## UAT-002 exit criteria

UAT-002 can close only when:

1. all P0 scenarios have real executions;
2. latest result for every P0 is PASS;
3. there are zero unresolved S1/S2 defects;
4. failed scenarios have traceable defect and retest evidence;
5. blocked scenarios are resolved and rerun;
6. tested commit SHA/environment/persona are recorded;
7. UAT summary is generated from validated real registers;
8. named business/technical owners complete sign-off.

Detailed payroll/attendance/meal/camp numeric reconciliation is additionally tracked under UAT-003.
