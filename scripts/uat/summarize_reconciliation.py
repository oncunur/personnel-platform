#!/usr/bin/env python3
"""Generate a sanitized UAT-003 readiness summary without exposing payroll amounts."""

from __future__ import annotations

import argparse
import csv
import json
from collections import Counter, defaultdict
from decimal import Decimal
from pathlib import Path

from validate_reconciliation import DECIMAL_FIELDS, INTEGER_FIELDS, evaluate_row, validate


def read_csv(path: Path) -> list[dict[str, str]]:
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        return list(csv.DictReader(handle))


def parsed_values(row: dict[str, str]) -> dict[str, Decimal | int]:
    parsed: dict[str, Decimal | int] = {field: int(row[field]) for field in INTEGER_FIELDS}
    parsed.update({field: Decimal(row[field]) for field in DECIMAL_FIELDS})
    return parsed


def summarize(rows_path: Path, signoffs_path: Path) -> dict[str, object]:
    errors, _ = validate(rows_path, signoffs_path, strict=True)
    if errors:
        raise ValueError("; ".join(errors))

    rows = read_csv(rows_path)
    signoffs = {row["reconciliation_id"].strip(): row for row in read_csv(signoffs_path)}
    grouped: dict[str, list[dict[str, str]]] = defaultdict(list)
    for row in rows:
        grouped[row["reconciliation_id"].strip()].append(row)

    runs: list[dict[str, object]] = []
    overall_mismatches = Counter()
    classifications = Counter()
    for reconciliation_id, group in sorted(grouped.items()):
        signoff = signoffs[reconciliation_id]
        total_row = next(row for row in group if row["scope"].strip() == "TOTAL")
        total_variances = evaluate_row(parsed_values(total_row))
        sample_rows = [row for row in group if row["scope"].strip() == "EMPLOYEE_SAMPLE"]
        sample_mismatch_rows = 0
        mismatch_metrics = Counter(total_variances)
        for row in sample_rows:
            row_variances = evaluate_row(parsed_values(row))
            if row_variances:
                sample_mismatch_rows += 1
                mismatch_metrics.update(row_variances)
        overall_mismatches.update(mismatch_metrics)
        classification = total_row["data_classification"].strip()
        classifications[classification] += 1

        runs.append({
            "reconciliationId": reconciliation_id,
            "scenarioId": total_row["scenario_id"].strip(),
            "dataClassification": classification,
            "environment": total_row["environment"].strip(),
            "commitSha": total_row["commit_sha"].strip(),
            "companyCode": total_row["company_code"].strip(),
            "period": total_row["period"].strip(),
            "currency": total_row["currency"].strip(),
            "signoffStatus": signoff["status"].strip(),
            "totalRowMatches": not total_variances,
            "sampleRows": len(sample_rows),
            "sampleRowsMatched": len(sample_rows) - sample_mismatch_rows,
            "sampleRowsMismatched": sample_mismatch_rows,
            "mismatchMetrics": dict(sorted(mismatch_metrics.items())),
        })

    any_mismatch = bool(overall_mismatches)
    all_real_approved = (
        bool(runs)
        and all(run["dataClassification"] == "REAL_UAT" for run in runs)
        and all(run["signoffStatus"] == "APPROVED" for run in runs)
    )
    all_synthetic = bool(runs) and all(run["dataClassification"] == "SYNTHETIC_CI" for run in runs)
    if any_mismatch:
        verdict = "NO_GO"
    elif all_real_approved:
        verdict = "UAT_003_APPROVED"
    elif all_synthetic:
        verdict = "PASS_SYNTHETIC_RECONCILIATION"
    else:
        verdict = "AWAITING_SIGNOFF"

    return {
        "verdict": verdict,
        "sanitized": True,
        "reconciliations": len(runs),
        "rows": len(rows),
        "dataClassifications": dict(sorted(classifications.items())),
        "mismatchMetrics": dict(sorted(overall_mismatches.items())),
        "gates": {
            "totalsMatch": all(run["totalRowMatches"] for run in runs),
            "samplesMatch": all(run["sampleRowsMismatched"] == 0 for run in runs),
            "zeroUnresolvedVariance": not any_mismatch,
            "realDataOnly": bool(runs) and all(run["dataClassification"] == "REAL_UAT" for run in runs),
            "allRealSignoffsApproved": all_real_approved,
        },
        "runs": runs,
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--rows", required=True)
    parser.add_argument("--signoffs", required=True)
    parser.add_argument("--output")
    args = parser.parse_args()

    try:
        summary = summarize(Path(args.rows), Path(args.signoffs))
    except ValueError as error:
        print(f"UAT reconciliation summary FAILED: {error}")
        return 1
    rendered = json.dumps(summary, indent=2, ensure_ascii=False) + "\n"
    if args.output:
        output = Path(args.output)
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text(rendered, encoding="utf-8")
    print(rendered, end="")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
