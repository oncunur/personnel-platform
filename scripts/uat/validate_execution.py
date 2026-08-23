#!/usr/bin/env python3
"""Validate UAT-002 execution and defect registers against the UAT-001 catalog."""

from __future__ import annotations

import argparse
import csv
import re
import sys
from datetime import datetime
from pathlib import Path

EXECUTION_HEADERS = [
    "execution_id", "scenario_id", "environment", "commit_sha", "tester", "persona",
    "started_at", "completed_at", "result", "observed_result", "evidence_ref",
    "defect_id", "retest_of_execution_id", "notes",
]
DEFECT_HEADERS = [
    "defect_id", "scenario_id", "severity", "title", "status", "owner", "opened_at",
    "fixed_at", "retest_execution_id", "disposition", "notes",
]
RESULTS = {"PASS", "FAIL", "BLOCKED", "NOT_RUN"}
SEVERITIES = {"S1", "S2", "S3", "S4"}
DEFECT_STATUSES = {"OPEN", "IN_PROGRESS", "FIXED", "RETEST_PENDING", "CLOSED", "ACCEPTED_RISK"}
HEX_SHA = re.compile(r"^[0-9a-fA-F]{7,40}$")


def load_csv(path: Path, headers: list[str]) -> tuple[list[str], list[dict[str, str]]]:
    if not path.exists():
        return [f"File not found: {path}"], []
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        if reader.fieldnames != headers:
            return [f"Unexpected headers in {path}: {reader.fieldnames!r}; expected {headers!r}"], []
        return [], list(reader)


def parse_iso(value: str, field: str, row_no: int, errors: list[str]) -> None:
    if not value:
        return
    try:
        datetime.fromisoformat(value.replace("Z", "+00:00"))
    except ValueError:
        errors.append(f"Row {row_no}: {field} must be ISO-8601, got {value!r}")


def validate(catalog_path: Path, executions_path: Path, defects_path: Path, strict: bool = False) -> tuple[list[str], dict[str, int]]:
    errors: list[str] = []

    catalog_errors, catalog_rows = load_csv(
        catalog_path,
        ["scenario_id", "domain", "title", "persona", "route", "priority", "test_type", "preconditions", "steps", "expected_result", "evidence", "status"],
    )
    errors.extend(catalog_errors)
    execution_errors, execution_rows = load_csv(executions_path, EXECUTION_HEADERS)
    errors.extend(execution_errors)
    defect_errors, defect_rows = load_csv(defects_path, DEFECT_HEADERS)
    errors.extend(defect_errors)
    if errors:
        return errors, {"executions": len(execution_rows), "defects": len(defect_rows)}

    scenario_ids = {row["scenario_id"].strip() for row in catalog_rows}
    execution_ids: set[str] = set()
    execution_by_id: dict[str, dict[str, str]] = {}
    defect_ids: set[str] = set()
    defect_by_id: dict[str, dict[str, str]] = {}

    for row_no, row in enumerate(execution_rows, start=2):
        execution_id = row["execution_id"].strip()
        scenario_id = row["scenario_id"].strip()
        result = row["result"].strip()

        if not execution_id:
            errors.append(f"Execution row {row_no}: execution_id is required")
        elif execution_id in execution_ids:
            errors.append(f"Execution row {row_no}: duplicate execution_id {execution_id}")
        else:
            execution_ids.add(execution_id)
            execution_by_id[execution_id] = row

        if scenario_id not in scenario_ids:
            errors.append(f"Execution row {row_no}: unknown scenario_id {scenario_id!r}")
        if result not in RESULTS:
            errors.append(f"Execution row {row_no}: invalid result {result!r}")

        if row["commit_sha"].strip() and not HEX_SHA.match(row["commit_sha"].strip()):
            errors.append(f"Execution row {row_no}: commit_sha must be 7-40 hex characters")

        parse_iso(row["started_at"].strip(), "started_at", row_no, errors)
        parse_iso(row["completed_at"].strip(), "completed_at", row_no, errors)

        if result != "NOT_RUN":
            for field in ("environment", "commit_sha", "tester", "persona", "started_at", "completed_at", "observed_result"):
                if not row[field].strip():
                    errors.append(f"Execution row {row_no}: {field} is required for result {result}")
        if result in {"PASS", "FAIL"} and not row["evidence_ref"].strip():
            errors.append(f"Execution row {row_no}: evidence_ref is required for result {result}")
        if result == "FAIL" and not row["defect_id"].strip():
            errors.append(f"Execution row {row_no}: FAIL requires defect_id")
        if result == "PASS" and row["defect_id"].strip() and not row["retest_of_execution_id"].strip():
            errors.append(f"Execution row {row_no}: PASS may reference a defect only when it is a retest")

    for row_no, row in enumerate(defect_rows, start=2):
        defect_id = row["defect_id"].strip()
        scenario_id = row["scenario_id"].strip()
        severity = row["severity"].strip()
        status = row["status"].strip()

        if not defect_id:
            errors.append(f"Defect row {row_no}: defect_id is required")
        elif defect_id in defect_ids:
            errors.append(f"Defect row {row_no}: duplicate defect_id {defect_id}")
        else:
            defect_ids.add(defect_id)
            defect_by_id[defect_id] = row

        if scenario_id not in scenario_ids:
            errors.append(f"Defect row {row_no}: unknown scenario_id {scenario_id!r}")
        if severity not in SEVERITIES:
            errors.append(f"Defect row {row_no}: invalid severity {severity!r}")
        if status not in DEFECT_STATUSES:
            errors.append(f"Defect row {row_no}: invalid status {status!r}")
        for field in ("title", "owner", "opened_at"):
            if not row[field].strip():
                errors.append(f"Defect row {row_no}: {field} is required")
        parse_iso(row["opened_at"].strip(), "opened_at", row_no, errors)
        parse_iso(row["fixed_at"].strip(), "fixed_at", row_no, errors)
        if status in {"FIXED", "RETEST_PENDING", "CLOSED"} and not row["fixed_at"].strip():
            errors.append(f"Defect row {row_no}: status {status} requires fixed_at")
        if status in {"CLOSED", "ACCEPTED_RISK"} and not row["disposition"].strip():
            errors.append(f"Defect row {row_no}: status {status} requires disposition")

    for row_no, row in enumerate(execution_rows, start=2):
        defect_id = row["defect_id"].strip()
        retest_of = row["retest_of_execution_id"].strip()
        if defect_id and defect_id not in defect_ids:
            errors.append(f"Execution row {row_no}: unknown defect_id {defect_id}")
        if retest_of:
            if retest_of not in execution_by_id:
                errors.append(f"Execution row {row_no}: unknown retest_of_execution_id {retest_of}")
            elif execution_by_id[retest_of]["scenario_id"].strip() != row["scenario_id"].strip():
                errors.append(f"Execution row {row_no}: retest must reference the same scenario")

    for row_no, row in enumerate(defect_rows, start=2):
        retest_id = row["retest_execution_id"].strip()
        if retest_id:
            if retest_id not in execution_by_id:
                errors.append(f"Defect row {row_no}: unknown retest_execution_id {retest_id}")
            elif execution_by_id[retest_id]["scenario_id"].strip() != row["scenario_id"].strip():
                errors.append(f"Defect row {row_no}: retest execution must reference the same scenario")
            elif row["status"].strip() == "CLOSED" and execution_by_id[retest_id]["result"].strip() != "PASS":
                errors.append(f"Defect row {row_no}: CLOSED defect retest execution must PASS")

    if strict and not execution_rows:
        errors.append("Strict validation requires at least one execution row")

    return errors, {"executions": len(execution_rows), "defects": len(defect_rows)}


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--catalog", default="docs/uat/uat-scenario-catalog.csv")
    parser.add_argument("--executions", default="docs/uat/templates/uat-execution.csv")
    parser.add_argument("--defects", default="docs/uat/templates/uat-defects.csv")
    parser.add_argument("--strict", action="store_true")
    args = parser.parse_args()

    errors, counts = validate(Path(args.catalog), Path(args.executions), Path(args.defects), strict=args.strict)
    if errors:
        print("UAT execution validation FAILED", file=sys.stderr)
        for error in errors:
            print(f"- {error}", file=sys.stderr)
        return 1

    print(f"UAT execution contracts valid: executions={counts['executions']} defects={counts['defects']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
