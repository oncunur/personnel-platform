#!/usr/bin/env python3
from __future__ import annotations

import argparse
import csv
import json
import sys
from pathlib import Path

from transform_values import apply_transform, mask_value

MAPPING_COLUMNS = [
    "mapping_id", "source_system", "source_object", "source_field", "source_type",
    "source_nullable", "target_domain", "target_entity", "target_field", "target_type",
    "target_required", "transformation", "lookup_dictionary", "default_rule",
    "validation_rule", "sensitivity", "lineage_key", "owner", "status", "notes",
]


def truthy(value: str) -> bool:
    return value.strip().upper() in {"TRUE", "YES", "1"}


def load_mapping(path: Path, source_system: str, source_object: str, allow_review: bool) -> list[dict[str, str]]:
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        if (reader.fieldnames or [] != MAPPING_COLUMNS:
            raise ValueError("field-mapping.csv header does not match the migration contract")
        allowed_status = {"APPROVED"}
        if allow_review:
            allowed_status.add("REVIEW")
        result = []
        for row in reader:
            if row["source_system"].strip().casefold() != source_system.casefold():
                continue
            if row["source_object"].strip().casefold() != source_object.casefold():
                continue
            if row["status"].strip().upper() not in allowed_status:
                continue
            result.append(dict(row))
        return result


def load_lookup(value: str, base_dir: Path) -> dict[str, str]:
    if not value.strip():
        return {}
    candidate = Path(value)
    if not candidate.is_absolute():
        candidate = base_dir / candidate
    if not candidate.exists():
        raise ValueError(f"lookup dictionary not found: {candidate}")
    data = json.loads(candidate.read_text(encoding="utf-8"))
    if not isinstance(data, dict):
        raise ValueError(f"lookup dictionary must be a JSON object: {candidate}")
    return {str(k).strip().casefold(): str(v).strip() for k, v in data.items()}


def apply_default(value: str, rule: str) -> str:
    if value:
        return value
    rule = rule.strip()
    if not rule:
        return value
    if rule.upper().startswith("VALUE:"):
        return rule.split(":", 1)[1]
    raise ValueError(f"unsupported default rule: {rule}")


def transform_row(
    source_row: dict[str, str],
    mappings: list[dict[str, str]],
    lookup_base: Path,
) -> tuple[dict[str, str], dict[str, str], list[str]]:
    clear: dict[str, str] = {}
    masked: dict[str, str] = {}
    errors: list[str] = []

    for mapping in mappings:
        source_field = mapping["source_field"]
        target_key = f"{mapping['target_entity']}.{mapping['target_field']}"
        if source_field not in source_row:
            errors.append(f"{mapping['mapping_id']}: source field '{source_field}' is missing")
            continue

        try:
            value = apply_transform(source_row.get(source_field), mapping["transformation"])
            value = apply_default(value, mapping["default_rule"])

            if mapping["lookup_dictionary"].strip():
                lookup = load_lookup(mapping["lookup_dictionary"], lookup_base)
                lookup_key = value.strip().casefold()
                if value and lookup_key not in lookup:
                    raise ValueError(f"lookup value not found: {value}")
                value = lookup.get(lookup_key, "") if value else ""

            if truthy(mapping["target_required"]) and not value:
                raise ValueError("required target value is empty")

            clear[target_key] = value
            masked[target_key] = mask_value(value, mapping["sensitivity"])
        except ValueError as exc:
            errors.append(f"{mapping['mapping_id']}: {exc}")

    return clear, masked, errors


def write_output(path: Path, rows: list[dict[str, str]]) -> None:
    columns = sorted({key for row in rows for key in row})
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=columns)
        writer.writeheader()
        writer.writerows(rows)


def main() -> int:
    parser = argparse.ArgumentParser(description="Dry-run a legacy CSV through an approved migration field mapping.")
    parser.add_argument("--source", required=True)
    parser.add_argument("--mapping", default="docs/migration/templates/field-mapping.csv")
    parser.add_argument("--source-system", required=True)
    parser.add_argument("--source-object", required=True)
    parser.add_argument("--lookup-base", default=".")
    parser.add_argument("--max-rows", type=int, default=20)
    parser.add_argument("--allow-review", action="store_true", help="Allow REVIEW mappings in addition to APPROVED mappings.")
    parser.add_argument("--output", help="Optional clear-text transformed CSV. Use only in an approved secure workspace.")
    args = parser.parse_args()

    try:
        mappings = load_mapping(Path(args.mapping), args.source_system, args.source_object, args.allow_review)
        if not mappings:
            raise ValueError("no APPROVED mapping rows found for the selected source object")

        with Path(args.source).open("r", encoding="utf-8-sig", newline="") as handle:
            reader = csv.DictReader(handle)
            source_rows = list(reader)
    except (OSError, ValueError) as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 2

    preview_limit = max(1, min(args.max_rows, 200))
    transformed_rows: list[dict[str, str]] = []
    total_errors = 0

    for index, source_row in enumerate(source_rows, start=2):
        clear, masked, errors = transform_row(source_row, mappings, Path(args.lookup_base))
        transformed_rows.append(clear)
        if errors:
            total_errors += len(errors)
        if index - 1 <= preview_limit:
            print(json.dumps({"source_row": index, "values": masked, "errors": errors}, ensure_ascii=False))

    if args.output:
        output = Path(args.output)
        write_output(output, transformed_rows)
        print(
            f"WARNING: clear-text transformed data written to {output}. This file may contain personal/financial data and must not be committed.",
            file=sys.stderr,
        )

    print(
        f"Migration preview complete: source_rows={len(source_rows)}, mappings={len(mappings)}, errors={total_errors}",
        file=sys.stderr,
    )
    return 1 if total_errors else 0


if __name__ == "__main__":
    raise SystemExit(main())
