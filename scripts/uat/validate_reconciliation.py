#!/usr/bin/env python3
"""Validate UAT-003 payroll/source reconciliation rows and sign-off records."""

from __future__ import annotations

import argparse
import csv
import re
import sys
from collections import defaultdict
from datetime import datetime
from decimal import Decimal, InvalidOperation
from pathlib import Path

ROW_HEADERS = [
    "reconciliation_id", "scenario_id", "data_classification", "environment", "commit_sha",
    "company_code", "period", "scope", "employee_ref", "planned_minutes_source",
    "planned_minutes_payroll", "worked_minutes_source", "worked_minutes_payroll",
    "paid_leave_minutes_source", "paid_leave_minutes_payroll", "approved_overtime_minutes_source",
    "approved_overtime_minutes_payroll", "meal_quantity", "meal_cost_source", "meal_cost_payroll",
    "camp_nights", "accommodation_cost_source", "accommodation_cost_payroll", "base_salary_amount",
    "absence_deduction_amount", "overtime_earning_amount", "pay_before_statutory_payroll",
    "employer_cost_before_statutory_payroll", "currency", "money_tolerance", "evidence_ref",
    "defect_id", "notes",
]
SIGNOFF_HEADERS = [
    "reconciliation_id", "status", "business_owner", "technical_owner", "business_approved_at",
    "technical_approved_at", "decision_note", "evidence_ref",
]

SCENARIOS = {"PAY-005", "E2E-001"}
DATA_CLASSIFICATIONS = {"SYNTHETIC_CI", "REAL_UAT"}
SCOPES = {"TOTAL", "EMPLOYEE_SAMPLE"}
SIGNOFF_STATUSES = {"DRAFT", "READY_FOR_REVIEW", "APPROVED", "BLOCKED", "REJECTED"}
HEX_SHA = re.compile(r"^[0-9a-fA-F]{7,40}$")
CURRENCY = re.compile(r"^[A-Z]{3}$")
PERIOD = re.compile(r"^(\d{4})-(\d{2})$")

INTEGER_FIELDS = [
    "planned_minutes_source", "planned_minutes_payroll", "worked_minutes_source",
    "worked_minutes_payroll", "paid_leave_minutes_source", "paid_leave_minutes_payroll",
    "approved_overtime_minutes_source", "approved_overtime_minutes_payroll", "camp_nights",
]
DECIMAL_FIELDS = [
    "meal_quantity", "meal_cost_source", "meal_cost_payroll", "accommodation_cost_source",
    "accommodation_cost_payroll", "base_salary_amount", "absence_deduction_amount",
    "overtime_earning_amount", "pay_before_statutory_payroll",
    "employer_cost_before_statutory_payroll", "money_tolerance",
]


def load_csv(path: Path, headers: list[str]) -> tuple[list[str], list[dict[str, str]]]:
    if not path.exists():
        return [f"File not found: {path}"], []
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        if reader.fieldnames != headers:
            return [f"Unexpected headers in {path}: {reader.fieldnames!r}; expected {headers!r}"], []
        return [], list(reader)


def parse_iso(value: str, field: str, label: str, errors: list[str]) -> None:
    if not value:
        return
    try:
        parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
        if parsed.tzinfo is None:
            errors.append(f"{label}: {field} must include a timezone")
    except ValueError:
        errors.append(f"{label}: {field} must be ISO-8601, got {value!r}")


def parse_numeric_row(row: dict[str, str], label: str, errors: list[str]) -> dict[str, Decimal | int] | None:
    parsed: dict[str, Decimal | int] = {}
    valid = True
    for field in INTEGER_FIELDS:
        try:
            value = int(row[field].strip())
            if value < 0:
                raise ValueError
            parsed[field] = value
        except ValueError:
            errors.append(f"{label}: {field} must be a non-negative integer")
            valid = False
    for field in DECIMAL_FIELDS:
        try:
            value = Decimal(row[field].strip())
            if not value.is_finite() or value < 0:
                raise InvalidOperation
            parsed[field] = value
        except (InvalidOperation, ValueError):
            errors.append(f"{label}: {field} must be a non-negative finite decimal")
            valid = False
    if valid and parsed["money_tolerance"] > Decimal("1.00"):
        errors.append(f"{label}: money_tolerance cannot exceed 1.00")
        valid = False
    return parsed if valid else None


def evaluate_row(parsed: dict[str, Decimal | int]) -> dict[str, Decimal]:
    """Return metric variances as payroll/actual minus source/derived expectation."""
    tolerance = Decimal(parsed["money_tolerance"])
    variances: dict[str, Decimal] = {
        "planned_minutes": Decimal(parsed["planned_minutes_payroll"] - parsed["planned_minutes_source"]),
        "worked_minutes": Decimal(parsed["worked_minutes_payroll"] - parsed["worked_minutes_source"]),
        "paid_leave_minutes": Decimal(parsed["paid_leave_minutes_payroll"] - parsed["paid_leave_minutes_source"]),
        "approved_overtime_minutes": Decimal(
            parsed["approved_overtime_minutes_payroll"] - parsed["approved_overtime_minutes_source"]
        ),
        "meal_employer_cost": Decimal(parsed["meal_cost_payroll"]) - Decimal(parsed["meal_cost_source"]),
        "accommodation_employer_cost": (
            Decimal(parsed["accommodation_cost_payroll"]) - Decimal(parsed["accommodation_cost_source"])
        ),
    }
    expected_pay = (
        Decimal(parsed["base_salary_amount"])
        - Decimal(parsed["absence_deduction_amount"])
        + Decimal(parsed["overtime_earning_amount"])
    )
    variances["pay_before_statutory_formula"] = Decimal(parsed["pay_before_statutory_payroll"]) - expected_pay
    expected_employer_cost = (
        Decimal(parsed["pay_before_statutory_payroll"])
        + Decimal(parsed["meal_cost_payroll"])
        + Decimal(parsed["accommodation_cost_payroll"])
    )
    variances["employer_cost_before_statutory_formula"] = (
        Decimal(parsed["employer_cost_before_statutory_payroll"]) - expected_employer_cost
    )
    return {
        metric: variance
        for metric, variance in variances.items()
        if variance != 0 and (metric.endswith("_minutes") or abs(variance) > tolerance)
    }


def validate(
    rows_path: Path,
    signoffs_path: Path,
    strict: bool = False,
) -> tuple[list[str], dict[str, int]]:
    errors: list[str] = []
    row_errors, rows = load_csv(rows_path, ROW_HEADERS)
    signoff_errors, signoffs = load_csv(signoffs_path, SIGNOFF_HEADERS)
    errors.extend(row_errors)
    errors.extend(signoff_errors)
    if errors:
        return errors, {"rows": len(rows), "reconciliations": 0, "signoffs": len(signoffs), "mismatchedRows": 0}

    grouped: dict[str, list[tuple[dict[str, str], dict[str, Decimal | int], list[str]]]] = defaultdict(list)
    seen_keys: set[tuple[str, str, str]] = set()
    mismatched_rows = 0

    for row_no, row in enumerate(rows, start=2):
        label = f"Reconciliation row {row_no}"
        reconciliation_id = row["reconciliation_id"].strip()
        scope = row["scope"].strip()
        employee_ref = row["employee_ref"].strip()

        for field in (
            "reconciliation_id", "scenario_id", "data_classification", "environment", "commit_sha",
            "company_code", "period", "scope", "currency", "money_tolerance", "evidence_ref",
        ):
            if not row[field].strip():
                errors.append(f"{label}: {field} is required")
        if row["scenario_id"].strip() not in SCENARIOS:
            errors.append(f"{label}: scenario_id must be PAY-005 or E2E-001")
        if row["data_classification"].strip() not in DATA_CLASSIFICATIONS:
            errors.append(f"{label}: invalid data_classification {row['data_classification']!r}")
        if scope not in SCOPES:
            errors.append(f"{label}: invalid scope {scope!r}")
        if scope == "TOTAL" and employee_ref:
            errors.append(f"{label}: TOTAL rows must not include employee_ref")
        if scope == "EMPLOYEE_SAMPLE" and not employee_ref:
            errors.append(f"{label}: EMPLOYEE_SAMPLE requires employee_ref")
        if not HEX_SHA.match(row["commit_sha"].strip()):
            errors.append(f"{label}: commit_sha must be 7-40 hex characters")
        if not CURRENCY.match(row["currency"].strip()):
            errors.append(f"{label}: currency must be an uppercase ISO-style 3-letter code")
        period_match = PERIOD.match(row["period"].strip())
        if not period_match or not 1 <= int(period_match.group(2)) <= 12:
            errors.append(f"{label}: period must be YYYY-MM")

        key = (reconciliation_id, scope, employee_ref)
        if key in seen_keys:
            errors.append(f"{label}: duplicate reconciliation/scope/employee key {key!r}")
        seen_keys.add(key)

        parsed = parse_numeric_row(row, label, errors)
        if parsed is None or not reconciliation_id:
            continue
        mismatches = list(evaluate_row(parsed))
        if mismatches:
            mismatched_rows += 1
            if not row["defect_id"].strip():
                errors.append(f"{label}: mismatched metrics require defect_id ({', '.join(mismatches)})")
        grouped[reconciliation_id].append((row, parsed, mismatches))

    metadata_fields = [
        "scenario_id", "data_classification", "environment", "commit_sha", "company_code", "period",
        "currency", "money_tolerance",
    ]
    for reconciliation_id, group in grouped.items():
        totals = [item for item in group if item[0]["scope"].strip() == "TOTAL"]
        samples = [item for item in group if item[0]["scope"].strip() == "EMPLOYEE_SAMPLE"]
        if len(totals) != 1:
            errors.append(f"Reconciliation {reconciliation_id}: exactly one TOTAL row is required")
        if not samples:
            errors.append(f"Reconciliation {reconciliation_id}: at least one EMPLOYEE_SAMPLE row is required")
        first = group[0][0]
        for field in metadata_fields:
            values = {item[0][field].strip() for item in group}
            if len(values) != 1:
                errors.append(f"Reconciliation {reconciliation_id}: {field} must be consistent across all rows")
        if first["data_classification"].strip() == "SYNTHETIC_CI" and any(
            item[0]["evidence_ref"].strip().startswith(("http://", "https://")) for item in group
        ):
            errors.append(f"Reconciliation {reconciliation_id}: synthetic evidence must use a synthetic/local reference")

    signoff_by_id: dict[str, dict[str, str]] = {}
    for row_no, signoff in enumerate(signoffs, start=2):
        label = f"Sign-off row {row_no}"
        reconciliation_id = signoff["reconciliation_id"].strip()
        status = signoff["status"].strip()
        if not reconciliation_id:
            errors.append(f"{label}: reconciliation_id is required")
        elif reconciliation_id in signoff_by_id:
            errors.append(f"{label}: duplicate sign-off for {reconciliation_id}")
        else:
            signoff_by_id[reconciliation_id] = signoff
        if status not in SIGNOFF_STATUSES:
            errors.append(f"{label}: invalid status {status!r}")
        parse_iso(signoff["business_approved_at"].strip(), "business_approved_at", label, errors)
        parse_iso(signoff["technical_approved_at"].strip(), "technical_approved_at", label, errors)

    for reconciliation_id, group in grouped.items():
        signoff = signoff_by_id.get(reconciliation_id)
        if signoff is None:
            errors.append(f"Reconciliation {reconciliation_id}: sign-off row is required")
            continue
        status = signoff["status"].strip()
        has_mismatch = any(item[2] for item in group)
        classification = group[0][0]["data_classification"].strip()
        if has_mismatch and status in {"READY_FOR_REVIEW", "APPROVED"}:
            errors.append(f"Reconciliation {reconciliation_id}: mismatches cannot be ready or approved")
        if status == "READY_FOR_REVIEW" and not signoff["evidence_ref"].strip():
            errors.append(f"Reconciliation {reconciliation_id}: READY_FOR_REVIEW requires evidence_ref")
        if status == "APPROVED":
            if classification != "REAL_UAT":
                errors.append(f"Reconciliation {reconciliation_id}: synthetic data cannot be APPROVED")
            for field in (
                "business_owner", "technical_owner", "business_approved_at", "technical_approved_at",
                "decision_note", "evidence_ref",
            ):
                if not signoff[field].strip():
                    errors.append(f"Reconciliation {reconciliation_id}: APPROVED requires {field}")
        if status in {"BLOCKED", "REJECTED"}:
            for field in ("decision_note", "evidence_ref"):
                if not signoff[field].strip():
                    errors.append(f"Reconciliation {reconciliation_id}: {status} requires {field}")

    for reconciliation_id in signoff_by_id:
        if reconciliation_id not in grouped:
            errors.append(f"Sign-off {reconciliation_id}: no reconciliation rows found")

    if strict and not rows:
        errors.append("Strict validation requires at least one reconciliation row")
    if strict and not signoffs:
        errors.append("Strict validation requires at least one sign-off row")

    return errors, {
        "rows": len(rows),
        "reconciliations": len(grouped),
        "signoffs": len(signoffs),
        "mismatchedRows": mismatched_rows,
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--rows", default="docs/uat/templates/uat-reconciliation.csv")
    parser.add_argument("--signoffs", default="docs/uat/templates/uat-reconciliation-signoff.csv")
    parser.add_argument("--strict", action="store_true")
    args = parser.parse_args()

    errors, counts = validate(Path(args.rows), Path(args.signoffs), strict=args.strict)
    if errors:
        print("UAT reconciliation validation FAILED", file=sys.stderr)
        for error in errors:
            print(f"- {error}", file=sys.stderr)
        return 1
    print(
        "UAT reconciliation contracts valid: "
        f"rows={counts['rows']} reconciliations={counts['reconciliations']} "
        f"signoffs={counts['signoffs']} mismatchedRows={counts['mismatchedRows']}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
