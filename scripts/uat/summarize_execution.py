#!/usr/bin/env python3
"""Generate a sanitized UAT-002 readiness summary from execution and defect registers."""

from __future__ import annotations

import argparse
import csv
import json
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path

OPEN_DEFECT_STATUSES = {"OPEN", "IN_PROGRESS", "FIXED", "RETEST_PENDING"}


def read_csv(path: Path) -> list[dict[str, str]]:
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        return list(csv.DictReader(handle))


def completed_at_key(row: dict[str, str]) -> datetime:
    value = row.get("completed_at", "").strip()
    if not value:
        return datetime.min.replace(tzinfo=timezone.utc)
    parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
    if parsed.tzinfo is None:
        parsed = parsed.replace(tzinfo=timezone.utc)
    return parsed.astimezone(timezone.utc)


def summarize(catalog_path: Path, executions_path: Path, defects_path: Path) -> dict[str, object]:
    catalog = read_csv(catalog_path)
    executions = read_csv(executions_path)
    defects = read_csv(defects_path)

    scenario_by_id = {row["scenario_id"].strip(): row for row in catalog}
    latest: dict[str, dict[str, str]] = {}
    for row in executions:
        scenario_id = row["scenario_id"].strip()
        current = latest.get(scenario_id)
        if current is None or completed_at_key(row) >= completed_at_key(current):
            latest[scenario_id] = row

    priority_totals = Counter(row["priority"].strip() for row in catalog)
    result_counts = Counter(row["result"].strip() for row in latest.values())
    p0_total = priority_totals["P0"]
    p0_pass = p0_fail = p0_blocked = p0_not_run = 0

    domain_summary: dict[str, dict[str, int]] = {}
    for scenario_id, scenario in scenario_by_id.items():
        domain = scenario["domain"].strip()
        bucket = domain_summary.setdefault(domain, {"total": 0, "pass": 0, "fail": 0, "blocked": 0, "notRun": 0})
        bucket["total"] += 1
        result = latest.get(scenario_id, {}).get("result", "NOT_RUN").strip() or "NOT_RUN"
        key = {"PASS": "pass", "FAIL": "fail", "BLOCKED": "blocked", "NOT_RUN": "notRun"}.get(result, "notRun")
        bucket[key] += 1

        if scenario["priority"].strip() == "P0":
            if result == "PASS":
                p0_pass += 1
            elif result == "FAIL":
                p0_fail += 1
            elif result == "BLOCKED":
                p0_blocked += 1
            else:
                p0_not_run += 1

    open_defects = [row for row in defects if row["status"].strip() in OPEN_DEFECT_STATUSES]
    open_by_severity = Counter(row["severity"].strip() for row in open_defects)
    open_s1_s2 = open_by_severity["S1"] + open_by_severity["S2"]

    if open_s1_s2 > 0 or p0_fail > 0:
        verdict = "NO_GO"
    elif p0_blocked > 0 or p0_not_run > 0:
        verdict = "NOT_READY"
    else:
        verdict = "UAT_READY_FOR_SIGNOFF"

    return {
        "verdict": verdict,
        "catalog": {
            "total": len(catalog),
            "p0": p0_total,
            "p1": priority_totals["P1"],
            "p2": priority_totals["P2"],
        },
        "latestExecutionResults": {
            "pass": result_counts["PASS"],
            "fail": result_counts["FAIL"],
            "blocked": result_counts["BLOCKED"],
            "notRunExplicit": result_counts["NOT_RUN"],
            "scenariosWithExecution": len(latest),
        },
        "p0": {
            "total": p0_total,
            "pass": p0_pass,
            "fail": p0_fail,
            "blocked": p0_blocked,
            "notRun": p0_not_run,
        },
        "openDefects": {
            "total": len(open_defects),
            "s1": open_by_severity["S1"],
            "s2": open_by_severity["S2"],
            "s3": open_by_severity["S3"],
            "s4": open_by_severity["S4"],
        },
        "gates": {
            "zeroOpenS1S2": open_s1_s2 == 0,
            "zeroP0Failures": p0_fail == 0,
            "zeroP0Blocked": p0_blocked == 0,
            "allP0Executed": p0_not_run == 0,
            "allP0Passed": p0_pass == p0_total,
        },
        "domains": dict(sorted(domain_summary.items())),
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--catalog", default="docs/uat/uat-scenario-catalog.csv")
    parser.add_argument("--executions", required=True)
    parser.add_argument("--defects", required=True)
    parser.add_argument("--output")
    args = parser.parse_args()

    summary = summarize(Path(args.catalog), Path(args.executions), Path(args.defects))
    rendered = json.dumps(summary, indent=2, ensure_ascii=False) + "\n"
    if args.output:
        output = Path(args.output)
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text(rendered, encoding="utf-8")
    print(rendered, end="")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
