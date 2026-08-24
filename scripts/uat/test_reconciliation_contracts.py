from __future__ import annotations

import csv
import tempfile
import unittest
from pathlib import Path

from summarize_reconciliation import summarize
from validate_reconciliation import ROW_HEADERS, SIGNOFF_HEADERS, validate

ROOT = Path(__file__).resolve().parents[2]
EMPTY_ROWS = ROOT / "docs" / "uat" / "templates" / "uat-reconciliation.csv"
EMPTY_SIGNOFFS = ROOT / "docs" / "uat" / "templates" / "uat-reconciliation-signoff.csv"
SYN_ROWS = ROOT / "scripts" / "uat" / "fixtures" / "synthetic-reconciliation.csv"
SYN_SIGNOFFS = ROOT / "scripts" / "uat" / "fixtures" / "synthetic-reconciliation-signoff.csv"


def read_rows(path: Path) -> list[dict[str, str]]:
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        return list(csv.DictReader(handle))


def write_rows(path: Path, headers: list[str], rows: list[dict[str, str]]) -> None:
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=headers)
        writer.writeheader()
        writer.writerows(rows)


def validate_temp(rows: list[dict[str, str]], signoffs: list[dict[str, str]]) -> tuple[list[str], dict[str, int]]:
    with tempfile.TemporaryDirectory() as directory:
        base = Path(directory)
        rows_path = base / "rows.csv"
        signoffs_path = base / "signoffs.csv"
        write_rows(rows_path, ROW_HEADERS, rows)
        write_rows(signoffs_path, SIGNOFF_HEADERS, signoffs)
        return validate(rows_path, signoffs_path, strict=True)


class UatReconciliationContractTests(unittest.TestCase):
    def test_empty_templates_are_valid_in_schema_mode(self) -> None:
        errors, counts = validate(EMPTY_ROWS, EMPTY_SIGNOFFS)
        self.assertEqual([], errors)
        self.assertEqual(0, counts["rows"])
        self.assertEqual(0, counts["reconciliations"])

    def test_synthetic_total_and_samples_are_valid(self) -> None:
        errors, counts = validate(SYN_ROWS, SYN_SIGNOFFS, strict=True)
        self.assertEqual([], errors)
        self.assertEqual(3, counts["rows"])
        self.assertEqual(1, counts["reconciliations"])
        self.assertEqual(0, counts["mismatchedRows"])

    def test_synthetic_summary_cannot_claim_real_approval(self) -> None:
        summary = summarize(SYN_ROWS, SYN_SIGNOFFS)
        self.assertEqual("PASS_SYNTHETIC_RECONCILIATION", summary["verdict"])
        self.assertTrue(summary["gates"]["totalsMatch"])
        self.assertTrue(summary["gates"]["samplesMatch"])
        self.assertFalse(summary["gates"]["realDataOnly"])
        self.assertFalse(summary["gates"]["allRealSignoffsApproved"])

    def test_mismatch_requires_defect_reference(self) -> None:
        rows = read_rows(SYN_ROWS)
        signoffs = read_rows(SYN_SIGNOFFS)
        rows[0]["planned_minutes_payroll"] = "19201"
        errors, _ = validate_temp(rows, signoffs)
        self.assertTrue(any("mismatched metrics require defect_id" in error for error in errors))

    def test_blocked_mismatch_is_valid_and_forces_no_go(self) -> None:
        rows = read_rows(SYN_ROWS)
        signoffs = read_rows(SYN_SIGNOFFS)
        rows[0]["planned_minutes_payroll"] = "19201"
        rows[0]["defect_id"] = "SYN-DEF-UAT003"
        signoffs[0]["status"] = "BLOCKED"
        signoffs[0]["decision_note"] = "Synthetic variance remains unresolved"
        with tempfile.TemporaryDirectory() as directory:
            base = Path(directory)
            rows_path = base / "rows.csv"
            signoffs_path = base / "signoffs.csv"
            write_rows(rows_path, ROW_HEADERS, rows)
            write_rows(signoffs_path, SIGNOFF_HEADERS, signoffs)
            errors, counts = validate(rows_path, signoffs_path, strict=True)
            self.assertEqual([], errors)
            self.assertEqual(1, counts["mismatchedRows"])
            summary = summarize(rows_path, signoffs_path)
        self.assertEqual("NO_GO", summary["verdict"])
        self.assertEqual(1, summary["mismatchMetrics"]["planned_minutes"])

    def test_payroll_formulas_are_recomputed(self) -> None:
        rows = read_rows(SYN_ROWS)
        signoffs = read_rows(SYN_SIGNOFFS)
        rows[1]["pay_before_statutory_payroll"] = "33760.00"
        rows[1]["defect_id"] = "SYN-DEF-FORMULA"
        signoffs[0]["status"] = "BLOCKED"
        errors, _ = validate_temp(rows, signoffs)
        self.assertEqual([], errors)
        with tempfile.TemporaryDirectory() as directory:
            base = Path(directory)
            rows_path = base / "rows.csv"
            signoffs_path = base / "signoffs.csv"
            write_rows(rows_path, ROW_HEADERS, rows)
            write_rows(signoffs_path, SIGNOFF_HEADERS, signoffs)
            summary = summarize(rows_path, signoffs_path)
        self.assertIn("pay_before_statutory_formula", summary["mismatchMetrics"])
        self.assertIn("employer_cost_before_statutory_formula", summary["mismatchMetrics"])

    def test_synthetic_reconciliation_cannot_be_approved(self) -> None:
        rows = read_rows(SYN_ROWS)
        signoffs = read_rows(SYN_SIGNOFFS)
        signoffs[0].update({
            "status": "APPROVED",
            "business_owner": "synthetic-business",
            "technical_owner": "synthetic-technical",
            "business_approved_at": "2026-08-24T07:00:00Z",
            "technical_approved_at": "2026-08-24T07:01:00Z",
            "decision_note": "Must be rejected because data is synthetic",
        })
        errors, _ = validate_temp(rows, signoffs)
        self.assertTrue(any("synthetic data cannot be APPROVED" in error for error in errors))

    def test_real_uat_requires_dual_approval_and_can_reach_approved_verdict(self) -> None:
        rows = read_rows(SYN_ROWS)
        signoffs = read_rows(SYN_SIGNOFFS)
        for row in rows:
            row["data_classification"] = "REAL_UAT"
        signoffs[0]["status"] = "APPROVED"
        signoffs[0]["decision_note"] = "Totals and employee samples reconciled"
        errors, _ = validate_temp(rows, signoffs)
        self.assertTrue(any("APPROVED requires business_owner" in error for error in errors))

        signoffs[0].update({
            "business_owner": "payroll-owner",
            "technical_owner": "platform-owner",
            "business_approved_at": "2026-08-24T07:00:00Z",
            "technical_approved_at": "2026-08-24T07:05:00Z",
        })
        with tempfile.TemporaryDirectory() as directory:
            base = Path(directory)
            rows_path = base / "rows.csv"
            signoffs_path = base / "signoffs.csv"
            write_rows(rows_path, ROW_HEADERS, rows)
            write_rows(signoffs_path, SIGNOFF_HEADERS, signoffs)
            errors, _ = validate(rows_path, signoffs_path, strict=True)
            self.assertEqual([], errors)
            summary = summarize(rows_path, signoffs_path)
        self.assertEqual("UAT_003_APPROVED", summary["verdict"])
        self.assertTrue(summary["gates"]["allRealSignoffsApproved"])

    def test_duplicate_employee_sample_is_rejected(self) -> None:
        rows = read_rows(SYN_ROWS)
        signoffs = read_rows(SYN_SIGNOFFS)
        rows.append(dict(rows[1]))
        errors, _ = validate_temp(rows, signoffs)
        self.assertTrue(any("duplicate reconciliation/scope/employee key" in error for error in errors))


if __name__ == "__main__":
    unittest.main()
