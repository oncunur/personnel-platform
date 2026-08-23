from __future__ import annotations

import csv
import tempfile
import unittest
from pathlib import Path

from summarize_execution import summarize
from validate_execution import DEFECT_HEADERS, EXECUTION_HEADERS, validate

ROOT = Path(__file__).resolve().parents[2]
CATALOG = ROOT / "docs" / "uat" / "uat-scenario-catalog.csv"
EMPTY_EXECUTIONS = ROOT / "docs" / "uat" / "templates" / "uat-execution.csv"
EMPTY_DEFECTS = ROOT / "docs" / "uat" / "templates" / "uat-defects.csv"
SYN_EXECUTIONS = ROOT / "scripts" / "uat" / "fixtures" / "synthetic-executions.csv"
SYN_DEFECTS = ROOT / "scripts" / "uat" / "fixtures" / "synthetic-defects.csv"


def read_rows(path: Path) -> list[dict[str, str]]:
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        return list(csv.DictReader(handle))


def write_rows(path: Path, headers: list[str], rows: list[dict[str, str]]) -> None:
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=headers)
        writer.writeheader()
        writer.writerows(rows)


class UatExecutionContractTests(unittest.TestCase):
    def test_empty_templates_are_valid_in_schema_mode(self) -> None:
        errors, counts = validate(CATALOG, EMPTY_EXECUTIONS, EMPTY_DEFECTS)
        self.assertEqual([], errors)
        self.assertEqual({"executions": 0, "defects": 0}, counts)

    def test_strict_synthetic_fixture_is_valid(self) -> None:
        errors, counts = validate(CATALOG, SYN_EXECUTIONS, SYN_DEFECTS, strict=True)
        self.assertEqual([], errors)
        self.assertEqual(4, counts["executions"])
        self.assertEqual(1, counts["defects"])

    def test_fail_without_defect_is_rejected(self) -> None:
        executions = read_rows(SYN_EXECUTIONS)
        defects = read_rows(SYN_DEFECTS)
        executions[1]["defect_id"] = ""
        with tempfile.TemporaryDirectory() as directory:
            base = Path(directory)
            execution_path = base / "executions.csv"
            defect_path = base / "defects.csv"
            write_rows(execution_path, EXECUTION_HEADERS, executions)
            write_rows(defect_path, DEFECT_HEADERS, defects)
            errors, _ = validate(CATALOG, execution_path, defect_path, strict=True)
        self.assertTrue(any("FAIL requires defect_id" in error for error in errors))

    def test_closed_defect_requires_passing_retest(self) -> None:
        executions = read_rows(SYN_EXECUTIONS)
        defects = read_rows(SYN_DEFECTS)
        defects[0]["retest_execution_id"] = ""
        with tempfile.TemporaryDirectory() as directory:
            base = Path(directory)
            execution_path = base / "executions.csv"
            defect_path = base / "defects.csv"
            write_rows(execution_path, EXECUTION_HEADERS, executions)
            write_rows(defect_path, DEFECT_HEADERS, defects)
            errors, _ = validate(CATALOG, execution_path, defect_path, strict=True)
        self.assertTrue(any("CLOSED requires retest_execution_id" in error for error in errors))

    def test_synthetic_summary_is_not_ready_but_closed_s2_is_not_open(self) -> None:
        summary = summarize(CATALOG, SYN_EXECUTIONS, SYN_DEFECTS)
        self.assertEqual("NOT_READY", summary["verdict"])
        self.assertTrue(summary["gates"]["zeroOpenS1S2"])
        self.assertEqual(0, summary["p0"]["fail"])
        self.assertEqual(1, summary["p0"]["blocked"])
        self.assertGreater(summary["p0"]["notRun"], 0)

    def test_open_s2_forces_no_go(self) -> None:
        defects = read_rows(SYN_DEFECTS)
        defects[0]["status"] = "OPEN"
        defects[0]["fixed_at"] = ""
        defects[0]["retest_execution_id"] = ""
        defects[0]["disposition"] = ""
        with tempfile.TemporaryDirectory() as directory:
            defect_path = Path(directory) / "defects.csv"
            write_rows(defect_path, DEFECT_HEADERS, defects)
            summary = summarize(CATALOG, SYN_EXECUTIONS, defect_path)
        self.assertEqual("NO_GO", summary["verdict"])
        self.assertFalse(summary["gates"]["zeroOpenS1S2"])
        self.assertEqual(1, summary["openDefects"]["s2"])

    def test_all_p0_pass_is_ready_for_signoff(self) -> None:
        catalog = read_rows(CATALOG)
        executions: list[dict[str, str]] = []
        for index, scenario in enumerate((row for row in catalog if row["priority"] == "P0"), start=1):
            executions.append({
                "execution_id": f"ALL-P0-{index:03d}",
                "scenario_id": scenario["scenario_id"],
                "environment": "SYNTHETIC_CI",
                "commit_sha": "c" * 40,
                "tester": "ci-tester",
                "persona": scenario["persona"],
                "started_at": f"2026-08-24T01:{index % 60:02d}:00Z",
                "completed_at": f"2026-08-24T02:{index % 60:02d}:00Z",
                "result": "PASS",
                "observed_result": "Synthetic P0 pass for readiness logic test",
                "evidence_ref": f"synthetic://{scenario['scenario_id']}/pass",
                "defect_id": "",
                "retest_of_execution_id": "",
                "notes": "Synthetic readiness logic only",
            })
        with tempfile.TemporaryDirectory() as directory:
            base = Path(directory)
            execution_path = base / "executions.csv"
            defect_path = base / "defects.csv"
            write_rows(execution_path, EXECUTION_HEADERS, executions)
            write_rows(defect_path, DEFECT_HEADERS, [])
            errors, _ = validate(CATALOG, execution_path, defect_path, strict=True)
            self.assertEqual([], errors)
            summary = summarize(CATALOG, execution_path, defect_path)
        self.assertEqual("UAT_READY_FOR_SIGNOFF", summary["verdict"])
        self.assertTrue(summary["gates"]["allP0Passed"])
        self.assertEqual(summary["p0"]["total"], summary["p0"]["pass"])

    def test_latest_execution_uses_absolute_timestamp_not_string_order(self) -> None:
        executions = [
            {
                "execution_id": "TZ-OLD",
                "scenario_id": "AUTH-001",
                "environment": "SYNTHETIC_CI",
                "commit_sha": "d" * 40,
                "tester": "ci-tester",
                "persona": "Platform Admin",
                "started_at": "2026-08-24T00:50:00+03:00",
                "completed_at": "2026-08-24T01:00:00+03:00",
                "result": "FAIL",
                "observed_result": "Older execution in absolute UTC time",
                "evidence_ref": "synthetic://tz/old",
                "defect_id": "TZ-DEF",
                "retest_of_execution_id": "",
                "notes": "",
            },
            {
                "execution_id": "TZ-NEW",
                "scenario_id": "AUTH-001",
                "environment": "SYNTHETIC_CI",
                "commit_sha": "e" * 40,
                "tester": "ci-tester",
                "persona": "Platform Admin",
                "started_at": "2026-08-23T23:55:00Z",
                "completed_at": "2026-08-24T00:00:00Z",
                "result": "PASS",
                "observed_result": "Newer execution in absolute UTC time",
                "evidence_ref": "synthetic://tz/new",
                "defect_id": "TZ-DEF",
                "retest_of_execution_id": "TZ-OLD",
                "notes": "",
            },
        ]
        defects = [{
            "defect_id": "TZ-DEF",
            "scenario_id": "AUTH-001",
            "severity": "S3",
            "title": "Timezone ordering fixture",
            "status": "CLOSED",
            "owner": "ci-owner",
            "opened_at": "2026-08-23T22:01:00Z",
            "fixed_at": "2026-08-23T23:50:00Z",
            "retest_execution_id": "TZ-NEW",
            "disposition": "Synthetic retest passed",
            "notes": "",
        }]
        with tempfile.TemporaryDirectory() as directory:
            base = Path(directory)
            execution_path = base / "executions.csv"
            defect_path = base / "defects.csv"
            write_rows(execution_path, EXECUTION_HEADERS, executions)
            write_rows(defect_path, DEFECT_HEADERS, defects)
            errors, _ = validate(CATALOG, execution_path, defect_path, strict=True)
            self.assertEqual([], errors)
            summary = summarize(CATALOG, execution_path, defect_path)
        self.assertEqual(1, summary["latestExecutionResults"]["pass"])
        self.assertEqual(0, summary["latestExecutionResults"]["fail"])


if __name__ == "__main__":
    unittest.main()
