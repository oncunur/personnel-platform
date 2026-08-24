# UAT-003 Payroll and Source Reconciliation

## Purpose

UAT-003 proves that payroll uses the approved attendance, leave, overtime, meal and camp values for a company and payroll month without an unexplained difference. It adds a repeatable evidence contract; it does not replace UAT-002 scenario execution or authorize synthetic data as business acceptance.

The committed files are templates and synthetic contract tests only. Real values and evidence must stay under the Git-ignored `uat-results/` directory or another approved restricted workspace.

## Evidence set

Each reconciliation uses two CSV files:

- `uat-reconciliation.csv` contains exactly one `TOTAL` row and at least one `EMPLOYEE_SAMPLE` row for each `reconciliation_id`.
- `uat-reconciliation-signoff.csv` records the business and technical decision for that reconciliation.

Use scenario `PAY-005` for payroll-focused evidence and `E2E-001` for the complete employee-month cross-domain flow. Use non-sensitive employee references such as an approved UAT employee number or alias; never put a real identity number, IBAN, token, MFA secret or password in a row or evidence reference.

## Automatic checks

The validator applies the following rules to both the company/month total and every selected employee sample:

| Check | Rule |
| --- | --- |
| Planned minutes | Attendance source must equal payroll result exactly |
| Worked minutes | Attendance source must equal payroll result exactly |
| Paid leave minutes | Leave/attendance source must equal payroll result exactly |
| Approved overtime minutes | Approved overtime source must equal payroll result exactly |
| Meal employer cost | Meal snapshot total must equal payroll meal cost within `money_tolerance` |
| Accommodation employer cost | Closed-stay snapshot total must equal payroll accommodation cost within tolerance |
| Pay before statutory | `base salary - absence deduction + overtime earning` must equal payroll value within tolerance |
| Employer cost before statutory | `pay before statutory + meal cost + accommodation cost` must equal payroll value within tolerance |

`meal_quantity` and `camp_nights` are required supporting facts even though the current payroll result stores their monetary snapshot rather than their quantity. Minute values cannot use a tolerance. `money_tolerance` must be non-negative and cannot exceed `1.00`; the recommended value is `0.01` unless the business owner documents a stricter currency-specific rule.

Any mismatched row requires a UAT-002 `defect_id`. A mismatch can be recorded as `BLOCKED` or `REJECTED`, but it cannot be marked `READY_FOR_REVIEW` or `APPROVED`.

## Preparing a real run

Create local working files:

```bash
mkdir -p uat-results
cp docs/uat/templates/uat-reconciliation.csv uat-results/uat-reconciliation.csv
cp docs/uat/templates/uat-reconciliation-signoff.csv uat-results/uat-reconciliation-signoff.csv
```

For one company/month:

1. Record the exact tested commit and environment.
2. Aggregate source totals from approved daily attendance, paid leave, approved overtime, meal consumption snapshots and closed accommodation stays.
3. Record the corresponding payroll result totals in the single `TOTAL` row.
4. Select risk-based employee samples and add one `EMPLOYEE_SAMPLE` row per employee. Include, at minimum, an employee with overtime, an employee with meal/accommodation cost, and an employee with absence or paid leave when those cases exist in the period.
5. Reference restricted evidence by opaque ID/path. Do not commit the populated CSV or screenshots.
6. Run validation and generate the sanitized summary.

```bash
python scripts/uat/validate_reconciliation.py \
  --rows uat-results/uat-reconciliation.csv \
  --signoffs uat-results/uat-reconciliation-signoff.csv \
  --strict

python scripts/uat/summarize_reconciliation.py \
  --rows uat-results/uat-reconciliation.csv \
  --signoffs uat-results/uat-reconciliation-signoff.csv \
  --output uat-output/uat-003-summary.json
```

The summary is sanitized: it reports match/mismatch counts and metric names, not employee references or payroll amounts.

## Sign-off states

- `DRAFT` — evidence is still being prepared.
- `READY_FOR_REVIEW` — all automatic checks match and evidence is ready for owners.
- `APPROVED` — allowed only for `REAL_UAT`, zero mismatches, named business and technical owners, both timezone-aware approval timestamps, a decision note and evidence reference.
- `BLOCKED` — an unresolved variance or missing dependency prevents approval.
- `REJECTED` — owners reviewed the evidence and rejected it.

Synthetic CI evidence may produce `PASS_SYNTHETIC_RECONCILIATION`, but can never produce `UAT_003_APPROVED`. Only fully matching `REAL_UAT` data with dual sign-off can produce that verdict.

## Exit criteria

UAT-003 can close only when:

1. each in-scope company/payroll month has a real `TOTAL` reconciliation;
2. risk-based employee samples have been checked and documented;
3. all minute, cost and formula checks match within the approved tolerance;
4. every discovered variance has a UAT-002 defect and a passing rerun after correction;
5. the exact environment and commit are recorded;
6. restricted evidence is complete and contains no exposed secrets or unmasked sensitive values;
7. business and technical owners approve the real reconciliation.

Until those real executions and signatures exist, UAT-003 remains `IN PROGRESS` even when the synthetic CI gate is green.
