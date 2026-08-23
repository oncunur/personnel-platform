#!/usr/bin/env python3
"""Validate the canonical Sprint 16 UAT scenario catalog."""

from __future__ import annotations

import argparse
import csv
import sys
from collections import Counter
from pathlib import Path

HEADERS = [
    "scenario_id",
    "domain",
    "title",
    "persona",
    "route",
    "priority",
    "test_type",
    "preconditions",
    "steps",
    "expected_result",
    "evidence",
    "status",
]

ALLOWED_PRIORITIES = {"P0", "P1", "P2"}
ALLOWED_TEST_TYPES = {
    "POSITIVE",
    "NEGATIVE",
    "BOUNDARY",
    "AUTHORIZATION",
    "IDEMPOTENCE",
    "SNAPSHOT",
    "RECONCILIATION",
    "END_TO_END",
}
ALLOWED_STATUSES = {"READY", "BLOCKED", "DRAFT"}
REQUIRED_DOMAINS = {
    "Security",
    "Organization",
    "Personnel",
    "Attendance",
    "Overtime",
    "Meal",
    "Camp",
    "Payroll",
    "Assets",
    "Workflow",
    "Authorization",
    "CrossDomain",
}
P0_REQUIRED_DOMAINS = {
    "Security",
    "Organization",
    "Personnel",
    "Attendance",
    "Overtime",
    "Meal",
    "Camp",
    "Payroll",
    "Assets",
    "Workflow",
    "Authorization",
    "CrossDomain",
}
REQUIRED_TEST_TYPES = {"POSITIVE", "NEGATIVE", "AUTHORIZATION", "END_TO_END", "RECONCILIATION"}


def fail(errors: list[str], message: str) -> None:
    errors.append(message)


def validate(path: Path) -> tuple[list[str], list[dict[str, str]]]:
    errors: list[str] = []
    if not path.exists():
        return [f"Catalog not found: {path}"], []

    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        if reader.fieldnames != HEADERS:
            return [f"Unexpected headers: {reader.fieldnames!r}; expected {HEADERS!r}"], []
        rows = list(reader)

    if not rows:
        fail(errors, "Catalog must contain at least one scenario")
        return errors, rows

    ids: list[str] = []
    domains = Counter()
    p0_domains = Counter()
    test_types = Counter()

    for index, row in enumerate(rows, start=2):
        for header in HEADERS:
            if not (row.get(header) or "").strip():
                fail(errors, f"Row {index}: {header} must not be blank")

        scenario_id = (row.get("scenario_id") or "").strip()
        ids.append(scenario_id)
        if scenario_id and "-" not in scenario_id:
            fail(errors, f"Row {index}: scenario_id must use PREFIX-NNN form")

        priority = (row.get("priority") or "").strip()
        if priority not in ALLOWED_PRIORITIES:
            fail(errors, f"Row {index}: invalid priority {priority!r}")

        test_type = (row.get("test_type") or "").strip()
        if test_type not in ALLOWED_TEST_TYPES:
            fail(errors, f"Row {index}: invalid test_type {test_type!r}")
        else:
            test_types[test_type] += 1

        status = (row.get("status") or "").strip()
        if status not in ALLOWED_STATUSES:
            fail(errors, f"Row {index}: invalid status {status!r}")

        domain = (row.get("domain") or "").strip()
        domains[domain] += 1
        if priority == "P0":
            p0_domains[domain] += 1

        routes = [part.strip() for part in (row.get("route") or "").split(";") if part.strip()]
        if not routes:
            fail(errors, f"Row {index}: route must contain at least one web route")
        for route in routes:
            if not route.startswith("/"):
                fail(errors, f"Row {index}: route {route!r} must start with '/'")
            if route.startswith("/api/"):
                fail(errors, f"Row {index}: route must describe the web UAT surface, not an API route: {route!r}")

        if status == "READY" and len((row.get("expected_result") or "").strip()) < 15:
            fail(errors, f"Row {index}: READY scenario expected_result is too short")

    duplicates = sorted(key for key, count in Counter(ids).items() if key and count > 1)
    if duplicates:
        fail(errors, f"Duplicate scenario IDs: {', '.join(duplicates)}")

    missing_domains = sorted(REQUIRED_DOMAINS - set(domains))
    if missing_domains:
        fail(errors, f"Missing required domains: {', '.join(missing_domains)}")

    missing_p0_domains = sorted(P0_REQUIRED_DOMAINS - set(p0_domains))
    if missing_p0_domains:
        fail(errors, f"Missing P0 coverage for domains: {', '.join(missing_p0_domains)}")

    missing_types = sorted(REQUIRED_TEST_TYPES - set(test_types))
    if missing_types:
        fail(errors, f"Missing required test types: {', '.join(missing_types)}")

    if test_types["AUTHORIZATION"] < 2:
        fail(errors, "Catalog must include at least two AUTHORIZATION scenarios")
    if test_types["END_TO_END"] < 3:
        fail(errors, "Catalog must include at least three END_TO_END scenarios")
    if test_types["NEGATIVE"] < 5:
        fail(errors, "Catalog must include at least five NEGATIVE scenarios")

    return errors, rows


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--catalog",
        default="docs/uat/uat-scenario-catalog.csv",
        help="Path to the canonical UAT scenario catalog",
    )
    args = parser.parse_args()

    errors, rows = validate(Path(args.catalog))
    if errors:
        print("UAT catalog validation FAILED", file=sys.stderr)
        for error in errors:
            print(f"- {error}", file=sys.stderr)
        return 1

    counts = Counter(row["priority"] for row in rows)
    domains = len({row["domain"] for row in rows})
    print(
        "UAT catalog valid: "
        f"scenarios={len(rows)} domains={domains} "
        f"P0={counts['P0']} P1={counts['P1']} P2={counts['P2']}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
