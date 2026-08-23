#!/usr/bin/env python3
from __future__ import annotations

import csv
import sys
from pathlib import Path

COLUMNS = [
    "target_domain", "target_entity", "target_field", "target_type", "target_required",
    "reference_entity", "sensitivity", "normalization", "validation", "notes",
]
ALLOWED_SENSITIVITY = {"PUBLIC", "INTERNAL", "PERSONAL", "SENSITIVE-HR", "FINANCIAL"}
ALLOWED_BOOL = {"TRUE", "FALSE"}
ALLOWED_TRANSFORMS = {
    "TRIM", "UPPER", "LOWER", "DIGITS", "PHONE_TR", "IBAN_TR", "DATE_AUTO",
    "MONTH_START", "DECIMAL_TR", "STATUS_EMPLOYEE", "BOOL_TR", "CURRENCY", "LOOKUP",
}


def main() -> int:
    path = Path("docs/migration/target-field-catalog.csv")
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        if (reader.fieldnames or []) != COLUMNS:
            print("ERROR: target-field-catalog.csv header is invalid", file=sys.stderr)
            return 1
        rows = list(reader)

    errors: list[str] = []
    seen: set[tuple[str, str, str]] = set()
    for line, row in enumerate(rows, start=2):
        key = (row["target_domain"].strip(), row["target_entity"].strip(), row["target_field"].strip())
        if not all(key):
            errors.append(f"row {line}: target domain/entity/field are required")
        elif key in seen:
            errors.append(f"row {line}: duplicate target field {'.'.join(key)}")
        seen.add(key)

        required = row["target_required"].strip().upper()
        if required not in ALLOWED_BOOL:
            errors.append(f"row {line}: invalid target_required '{required}'")

        sensitivity = row["sensitivity"].strip().upper()
        if sensitivity not in ALLOWED_SENSITIVITY:
            errors.append(f"row {line}: invalid sensitivity '{sensitivity}'")

        for transform in row["normalization"].split("|"):
            transform = transform.strip().upper()
            if transform and transform not in ALLOWED_TRANSFORMS:
                errors.append(f"row {line}: unknown normalization '{transform}'")

    if not rows:
        errors.append("target catalog must contain at least one target field")

    if errors:
        print("Target field catalog validation failed:", file=sys.stderr)
        for error in errors:
            print(f"- {error}", file=sys.stderr)
        return 1

    print(f"Target field catalog valid: rows={len(rows)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
