#!/usr/bin/env python3
from __future__ import annotations

import argparse
import csv
import sys
from pathlib import Path

INVENTORY_COLUMNS = [
    "source_system", "source_object", "business_owner", "extract_owner", "format",
    "source_key", "estimated_volume", "date_from", "date_to", "sensitivity",
    "decision", "target_domain", "target_entities", "reconciliation_method",
    "retention_note", "notes",
]

MAPPING_COLUMNS = [
    "mapping_id", "source_system", "source_object", "source_field", "source_type",
    "source_nullable", "target_domain", "target_entity", "target_field", "target_type",
    "target_required", "transformation", "lookup_dictionary", "default_rule",
    "validation_rule", "sensitivity", "lineage_key", "owner", "status", "notes",
]

ALLOWED_DECISIONS = {"MIGRATE", "REBUILD", "ARCHIVE", "IGNORE"}
ALLOWED_SENSITIVITY = {"PUBLIC", "INTERNAL", "PERSONAL", "SENSITIVE-HR", "FINANCIAL"}
ALLOWED_MAPPING_STATUS = {"DRAFT", "REVIEW", "APPROVED", "BLOCKED"}
BOOL_VALUES = {"TRUE", "FALSE", "YES", "NO", "1", "0"}
TBD_VALUES = {"", "TBD", "UNKNOWN", "?"}


def read_csv(path: Path, expected_columns: list[str]) -> list[dict[str, str]]:
    if not path.exists():
        raise ValueError(f"Missing file: {path}")
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        actual = reader.fieldnames or []
        if actual != expected_columns:
            raise ValueError(
                f"{path}: invalid header.\nExpected: {','.join(expected_columns)}\nActual:   {','.join(actual)}"
            )
        return [dict(row) for row in reader]


def require(row: dict[str, str], keys: tuple[str, ...], label: str, row_number: int, strict: bool, errors: list[str]) -> None:
    for key in keys:
        value = (row.get(key) or "").strip()
        if not value:
            errors.append(f"{label} row {row_number}: '{key}' is required")
        elif strict and value.upper() in TBD_VALUES:
            errors.append(f"{label} row {row_number}: '{key}' is unresolved ({value})")


def validate_inventory(rows: list[dict[str, str]], strict: bool) -> list[str]:
    errors: list[str] = []
    seen: set[tuple[str, str]] = set()
    for index, row in enumerate(rows, start=2):
        require(row, ("source_system", "source_object", "source_key", "decision", "target_domain", "reconciliation_method"), "inventory", index, strict, errors)
        decision = (row.get("decision") or "").strip().upper()
        sensitivity = (row.get("sensitivity") or "").strip().upper()
        if decision and decision not in ALLOWED_DECISIONS:
            errors.append(f"inventory row {index}: invalid decision '{decision}'")
        if sensitivity and sensitivity not in ALLOWED_SENSITIVITY:
            errors.append(f"inventory row {index}: invalid sensitivity '{sensitivity}'")
        key = ((row.get("source_system") or "").strip().upper(), (row.get("source_object") or "").strip().upper())
        if all(key):
            if key in seen:
                errors.append(f"inventory row {index}: duplicate source object {key[0]} / {key[1]}")
            seen.add(key)
    if strict and not rows:
        errors.append("inventory: strict validation requires at least one source object")
    return errors


def validate_mapping(rows: list[dict[str, str]], strict: bool) -> list[str]:
    errors: list[str] = []
    seen_ids: set[str] = set()
    for index, row in enumerate(rows, start=2):
        require(row, ("mapping_id", "source_system", "source_object", "source_field", "target_domain", "target_entity", "target_field", "status"), "mapping", index, strict, errors)
        mapping_id = (row.get("mapping_id") or "").strip().upper()
        status = (row.get("status") or "").strip().upper()
        sensitivity = (row.get("sensitivity") or "").strip().upper()
        source_nullable = (row.get("source_nullable") or "").strip().upper()
        target_required = (row.get("target_required") or "").strip().upper()
        if mapping_id:
            if mapping_id in seen_ids:
                errors.append(f"mapping row {index}: duplicate mapping_id '{mapping_id}'")
            seen_ids.add(mapping_id)
        if status and status not in ALLOWED_MAPPING_STATUS:
            errors.append(f"mapping row {index}: invalid status '{status}'")
        if sensitivity and sensitivity not in ALLOWED_SENSITIVITY:
            errors.append(f"mapping row {index}: invalid sensitivity '{sensitivity}'")
        if source_nullable and source_nullable not in BOOL_VALUES:
            errors.append(f"mapping row {index}: invalid source_nullable '{source_nullable}'")
        if target_required and target_required not in BOOL_VALUES:
            errors.append(f"mapping row {index}: invalid target_required '{target_required}'")
    return errors


def main() -> int:
    parser = argparse.ArgumentParser(description="Validate Sprint 16 migration inventory/mapping CSV contracts.")
    parser.add_argument("--inventory", default="docs/migration/templates/source-inventory.csv")
    parser.add_argument("--mapping", default="docs/migration/templates/field-mapping.csv")
    parser.add_argument("--strict", action="store_true", help="Reject unresolved/TBD values and require inventory rows.")
    args = parser.parse_args()

    try:
        inventory_rows = read_csv(Path(args.inventory), INVENTORY_COLUMNS)
        mapping_rows = read_csv(Path(args.mapping), MAPPING_COLUMNS)
    except ValueError as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 1

    errors = validate_inventory(inventory_rows, args.strict) + validate_mapping(mapping_rows, args.strict)
    if errors:
        print("Migration contract validation failed:", file=sys.stderr)
        for error in errors:
            print(f"- {error}", file=sys.stderr)
        return 1

    mode = "strict" if args.strict else "schema"
    print(f"Migration contracts valid ({mode} mode): inventory_rows={len(inventory_rows)}, mapping_rows={len(mapping_rows)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
