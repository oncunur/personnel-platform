# MIG-002 — Field Mapping and Transformation Rules

## Purpose

MIG-002 converts the source inventory from MIG-001 into an explicit, reviewable field contract. No production legacy record may be loaded until its source object has approved field mappings, lookup dictionaries, validation rules and reconciliation ownership.

The target model is anchored by `docs/migration/target-field-catalog.csv`, which reflects the current domain constraints for organization, personnel, sensitive employee data, assignments and compensation. The catalog is a target contract, not a guess about legacy column names.

## Mapping lifecycle

Each row in `docs/migration/templates/field-mapping.csv` progresses through:

- `DRAFT` — discovered but incomplete.
- `REVIEW` — transformation and business meaning proposed; may be dry-run with `--allow-review`.
- `APPROVED` — accepted for controlled dry runs.
- `BLOCKED` — unresolved meaning, missing reference data, legal/retention question or unsafe transformation.

Production migration requires all required mappings for the selected source object to be `APPROVED` and the migration contract validator to pass in strict mode.

## Canonical transformations

The transformation pipeline uses `|` between steps.

| Step | Behavior |
| --- | --- |
| `TRIM` | Remove surrounding whitespace. |
| `UPPER` | Convert text to upper case. |
| `LOWER` | Convert text to lower case. |
| `DIGITS` | Keep numeric characters only. |
| `DATE_AUTO` | Accept supported ISO/Turkish date formats and emit `YYYY-MM-DD`. |
| `MONTH_START` | Parse date and normalize it to the first day of the month. |
| `DECIMAL_TR` | Accept Turkish/international decimal separators and emit canonical decimal text. |
| `PHONE_TR` | Normalize Turkish mobile/phone numbers to `+90...` when unambiguous. |
| `IBAN_TR` | Remove spaces, uppercase and require `TR` + 24 digits. |
| `STATUS_EMPLOYEE` | Convert approved Turkish/English employment-state values to `ACTIVE`, `SUSPENDED`, `TERMINATED`. |
| `BOOL_TR` | Convert common Turkish/English yes/no values to `TRUE` / `FALSE`. |
| `CURRENCY` | Uppercase a currency code; target validation still requires three letters. |
| `LOOKUP` | Keep the normalized source lookup key; `preview_transform.py` then resolves `lookup_dictionary`. |

Unknown transformation steps fail closed instead of passing the original value through.

## Defaults

`default_rule` is intentionally narrow. The preview engine currently accepts only:

`VALUE:<literal>`

Defaults must represent a business-approved invariant. They must not be used to hide missing required legacy data. A required target value that remains empty after transformation/defaulting is an error.

## Lookup dictionaries

Use `lookup_dictionary` for controlled code/value translation such as:

- legacy company code -> target company migration key/GUID;
- legacy department code -> target department migration key/GUID;
- legacy worker type -> seeded `EmployeeType`;
- legacy project/cost-center identifiers -> target reference entities;
- source-specific status codes that are not safe to normalize generically.

Dictionaries are JSON objects and are resolved case-insensitively by the dry-run tool. Missing dictionary files or missing source values are blocking errors.

For actual migration runs, lookup outputs should normally resolve to migration lineage keys first and to target GUIDs only after the corresponding parent entity has been loaded and reconciled.

## Protected data rules

Fields classified as `PERSONAL`, `SENSITIVE-HR` or `FINANCIAL` are masked in console preview output. Clear transformed data is written only when `preview_transform.py --output ...` is explicitly supplied.

Generated clear-text migration files must stay inside an approved secure workspace. The repository ignores `migration-output/` and `*.migration-output.csv` to reduce accidental commits.

National ID, IBAN and salary values must never be inserted directly into ciphertext database columns. The future MIG-003 loader must call the same application protection/encryption paths used by normal platform writes.

Legacy password hashes, refresh tokens and MFA secrets are outside this mapping path and are not migrated.

## Dry-run usage

A real source object can be previewed after its mapping rows are approved:

```bash
python scripts/migration/preview_transform.py \
  --source secure-input/personnel.csv \
  --mapping docs/migration/templates/field-mapping.csv \
  --source-system LEGACY_HR \
  --source-object PERSONNEL \
  --max-rows 20
```

`REVIEW` mappings can be exercised before approval only with:

```bash
--allow-review
```

The preview returns non-zero when a row has a missing source field, invalid transformation, missing lookup value or empty required target value.

## Target-domain constraints already encoded

Examples reflected in the canonical catalog:

- Organization code/name values are mandatory and codes normalize to uppercase.
- `Employee.EmployeeNo`, first name, last name, organization references, employee type and hire date are required.
- Employee manager references are a second-pass relationship because the referenced employee may not yet exist during the base employee load.
- Termination date may not precede hire date.
- Project-assignment allocation is greater than zero and at most 100 percent.
- Compensation periods start on month boundaries.
- Monthly base salary must be positive.
- Overtime multiplier is constrained to the platform domain range.
- Compensation currency is a three-letter code.

Cross-field and foreign-key checks are documented in the catalog but become executable in MIG-003 when staging has access to reference lineage and target state.

## MIG-002 exit evidence

MIG-002 is complete only when, for every source object selected as `MIGRATE`:

1. Real source headers/types/nullability are captured.
2. Every required target field is covered or explicitly defaulted with approval.
3. Lookup dictionaries are approved and versioned.
4. Transformation rules pass representative source samples including edge cases.
5. Sensitive fields are classified correctly.
6. Duplicate/natural-key behavior is agreed.
7. Cross-field validation rules are documented.
8. Business owner and migration owner approve the mapping.
9. Strict migration contract validation passes.
10. A masked dry-run completes without unresolved mapping errors.

Until real legacy exports are supplied, the repository provides the target catalog, transformation engine, safety controls and synthetic end-to-end validation, but does not claim MIG-002 completion.
