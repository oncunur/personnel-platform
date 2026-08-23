#!/usr/bin/env python3
from __future__ import annotations

import argparse
import base64
import csv
import hashlib
import hmac
import json
import os
import struct
import sys
import time
import urllib.error
import urllib.request
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from preview_transform import apply_default, load_lookup, load_mapping, transform_row, truthy
from transform_values import apply_transform

FORBIDDEN_EVIDENCE_KEYS = {
    "accesstoken",
    "refreshtoken",
    "challengetoken",
    "enrollmentsecret",
    "totpsecret",
    "password",
    "sourcepayloadjson",
    "transformedpayloadjson",
    "sourcepayloadciphertext",
    "transformedpayloadciphertext",
}

EXPECTED_REAL_SOURCE_BLOCKERS = [
    "REAL_LEGACY_SOURCE_REQUIRED",
    "SOURCE_SPECIFIC_MAPPING_APPROVAL_PENDING",
    "BUSINESS_TOTAL_RECONCILIATION_PENDING",
    "LIVE_TARGET_LOAD_DISABLED_BY_DESIGN",
]


def canonical_json(value: Any) -> str:
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":"))


def sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest().upper()


def sha256_file(path: Path) -> str:
    return sha256_bytes(path.read_bytes())


def totp(base32_secret: str, at: int | None = None) -> str:
    cleaned = base32_secret.strip().replace(" ", "").rstrip("=").upper()
    padding = "=" * ((8 - len(cleaned) % 8) % 8)
    secret = base64.b32decode(cleaned + padding, casefold=True)
    timestamp = int(time.time()) if at is None else at
    counter = timestamp // 30
    digest = hmac.new(secret, struct.pack(">Q", counter), hashlib.sha1).digest()
    offset = digest[-1] & 0x0F
    binary = ((digest[offset] & 0x7F) << 24) | (digest[offset + 1] << 16) | (digest[offset + 2] << 8) | digest[offset + 3]
    return f"{binary % 1_000_000:06d}"


def ensure_safe_evidence(value: Any, path: str = "$") -> None:
    if isinstance(value, dict):
        for key, child in value.items():
            normalized = str(key).replace("_", "").replace("-", "").casefold()
            if normalized in FORBIDDEN_EVIDENCE_KEYS:
                raise ValueError(f"forbidden sensitive evidence key at {path}.{key}")
            ensure_safe_evidence(child, f"{path}.{key}")
    elif isinstance(value, list):
        for index, child in enumerate(value):
            ensure_safe_evidence(child, f"{path}[{index}]")


class ApiClient:
    def __init__(self, api_base: str) -> None:
        self.api_base = api_base.rstrip("/")
        self.access_token: str | None = None

    def request(self, method: str, path: str, payload: Any | None = None, authenticated: bool = False) -> Any:
        data = None if payload is None else canonical_json(payload).encode("utf-8")
        headers = {"Accept": "application/json", "User-Agent": "personnel-platform-migration-dry-run/1.0"}
        if data is not None:
            headers["Content-Type"] = "application/json"
        if authenticated:
            if not self.access_token:
                raise RuntimeError("authenticated API call requested without an access token")
            headers["Authorization"] = f"Bearer {self.access_token}"
        request = urllib.request.Request(self.api_base + path, data=data, headers=headers, method=method)
        try:
            with urllib.request.urlopen(request, timeout=30) as response:
                raw = response.read()
                return None if not raw else json.loads(raw.decode("utf-8"))
        except urllib.error.HTTPError as exc:
            raw = exc.read().decode("utf-8", errors="replace")
            try:
                body = json.loads(raw)
                code = body.get("code") or body.get("errorCode") or "HTTP_ERROR"
                message = body.get("message") or body.get("errorMessage") or f"HTTP {exc.code}"
            except json.JSONDecodeError:
                code = "HTTP_ERROR"
                message = f"HTTP {exc.code}"
            raise RuntimeError(f"{method} {path} failed: {code}: {message}") from exc
        except urllib.error.URLError as exc:
            raise RuntimeError(f"cannot reach migration API at {self.api_base}: {exc.reason}") from exc

    def authenticate(self, username: str, password: str, totp_secret: str | None) -> None:
        login = self.request("POST", "/api/v1/auth/login", {"username": username, "password": password})
        if isinstance(login, dict) and login.get("accessToken"):
            self.access_token = str(login["accessToken"])
            return
        if not isinstance(login, dict) or not login.get("challengeToken"):
            raise RuntimeError("login response did not contain an access token or MFA challenge")
        secret = login.get("enrollmentSecret") or totp_secret
        if not secret:
            raise RuntimeError("MFA is already enrolled. Set MIGRATION_TOTP_SECRET or pass --totp-secret for this disposable dry-run user.")
        completed = self.request(
            "POST",
            "/api/v1/auth/mfa/complete",
            {"challengeToken": login["challengeToken"], "code": totp(str(secret))},
        )
        if not isinstance(completed, dict) or not completed.get("accessToken"):
            raise RuntimeError("MFA completion did not issue an access token")
        self.access_token = str(completed["accessToken"])


def transform_mapping_value(source_row: dict[str, str], mapping: dict[str, str], lookup_base: Path) -> str:
    field = mapping["source_field"]
    if field not in source_row:
        raise ValueError(f"lineage source field is missing: {field}")
    value = apply_transform(source_row.get(field), mapping["transformation"])
    value = apply_default(value, mapping["default_rule"])
    if mapping["lookup_dictionary"].strip():
        lookup = load_lookup(mapping["lookup_dictionary"], lookup_base)
        lookup_key = value.strip().casefold()
        if value and lookup_key not in lookup:
            raise ValueError(f"lineage lookup value not found for {field}")
        value = lookup.get(lookup_key, "") if value else ""
    if not value:
        raise ValueError(f"lineage source field is empty after transformation: {field}")
    return value


def derive_source_key(source_row: dict[str, str], mappings: list[dict[str, str]], lookup_base: Path) -> str:
    lineage_mappings = [mapping for mapping in mappings if truthy(mapping["lineage_key"])]
    if not lineage_mappings:
        raise ValueError("at least one approved mapping row must have lineage_key=TRUE")
    parts = [transform_mapping_value(source_row, mapping, lookup_base) for mapping in lineage_mappings]
    key = "|".join(parts)
    if len(key) > 240:
        raise ValueError("derived source key exceeds 240 characters")
    return key


def prepare_rows(source_path: Path, mappings: list[dict[str, str]], lookup_base: Path) -> tuple[list[dict[str, Any]], int, int]:
    with source_path.open("r", encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        source_rows = list(reader)
    if not source_rows:
        raise ValueError("dry-run source contains no data rows")

    stage_rows: list[dict[str, Any]] = []
    error_rows = 0
    for row_number, source_row in enumerate(source_rows, start=2):
        clear, _masked, errors = transform_row(source_row, mappings, lookup_base)
        source_key = derive_source_key(source_row, mappings, lookup_base)
        source_payload = canonical_json(source_row)
        transformed_payload = canonical_json(clear)
        if errors:
            error_rows += 1
        stage_rows.append(
            {
                "rowNumber": row_number,
                "sourceKey": source_key,
                "sourceRowHash": sha256_bytes(source_payload.encode("utf-8")),
                "sourcePayloadJson": source_payload,
                "transformedPayloadJson": transformed_payload,
                "warningCode": None,
                "warningMessage": None,
                "errorCode": "TRANSFORM_ERROR" if errors else None,
                "errorMessage": "; ".join(errors)[:1900] if errors else None,
            }
        )
    return stage_rows, len(source_rows), error_rows


def get_or_create_company(client: ApiClient, code: str, name: str) -> dict[str, Any]:
    companies = client.request("GET", "/api/v1/organization/companies", authenticated=True)
    if not isinstance(companies, list):
        raise RuntimeError("organization company list response is not an array")
    for company in companies:
        if str(company.get("code", "")).casefold() == code.casefold():
            return company
    created = client.request(
        "POST",
        "/api/v1/organization/companies",
        {"code": code, "name": name, "taxNumber": None, "phone": None, "email": None, "address": None, "defaultCurrency": "TRY"},
        authenticated=True,
    )
    if not isinstance(created, dict) or not created.get("id"):
        raise RuntimeError("company creation response is invalid")
    return created


def count_rows(rows: list[dict[str, Any]], field: str) -> dict[str, int]:
    counts = Counter(str(row.get(field, "UNKNOWN")) for row in rows)
    return dict(sorted(counts.items()))


def run_once(
    client: ApiClient,
    label: str,
    company_id: str,
    source_system: str,
    source_object: str,
    target_entity: str,
    source_path: Path,
    source_hash: str,
    mapping_hash: str,
    stage_rows: list[dict[str, Any]],
    source_row_count: int,
    transform_error_rows: int,
) -> dict[str, Any]:
    started = time.monotonic()
    run = client.request(
        "POST",
        "/api/v1/migrations/runs",
        {
            "companyId": company_id,
            "sourceSystem": source_system,
            "sourceObject": source_object,
            "targetEntity": target_entity,
            "sourceFileName": source_path.name,
            "sourceContentHash": source_hash,
            "mappingHash": mapping_hash,
        },
        authenticated=True,
    )
    run_id = str(run["id"])
    staged = client.request(
        "POST",
        f"/api/v1/migrations/runs/{run_id}/stage",
        {"version": run["version"], "rows": stage_rows},
        authenticated=True,
    )
    validation = client.request(
        "POST",
        f"/api/v1/migrations/runs/{run_id}/validate",
        {"version": staged["run"]["version"]},
        authenticated=True,
    )
    rows = client.request("GET", f"/api/v1/migrations/runs/{run_id}/rows?take=2000", authenticated=True)
    if not isinstance(rows, list):
        raise RuntimeError("migration row list response is not an array")

    forbidden_payload_keys = {
        "sourcePayloadJson",
        "transformedPayloadJson",
        "sourcePayloadCiphertext",
        "transformedPayloadCiphertext",
    }
    payload_exposed = any(any(key in row for key in forbidden_payload_keys) for row in rows)
    unique_keys = len({str(row.get("sourceKey")) for row in rows})
    successful_rows = int(validation["run"]["validRows"]) + int(validation["run"]["warningRows"]) + int(validation["run"]["duplicateRows"])

    reconciled = client.request(
        "POST",
        f"/api/v1/migrations/runs/{run_id}/reconcile",
        {
            "version": validation["run"]["version"],
            "metrics": [
                {
                    "metricCode": "SOURCE_ROW_COUNT",
                    "metricName": "Source rows vs staged rows",
                    "sourceValue": source_row_count,
                    "targetValue": int(validation["run"]["totalRows"]),
                    "tolerance": 0,
                    "notes": "Technical dry-run row-count reconciliation.",
                },
                {
                    "metricCode": "UNIQUE_SOURCE_KEY_COUNT",
                    "metricName": "Unique source keys vs staged source keys",
                    "sourceValue": len({row["sourceKey"] for row in stage_rows}),
                    "targetValue": unique_keys,
                    "tolerance": 0,
                    "notes": "Technical lineage-key reconciliation.",
                },
                {
                    "metricCode": "TRANSFORM_SUCCESS_COUNT",
                    "metricName": "Transform-success rows vs non-error staged rows",
                    "sourceValue": source_row_count - transform_error_rows,
                    "targetValue": successful_rows,
                    "tolerance": 0,
                    "notes": "Business-value reconciliation remains pending for a real legacy extract.",
                },
            ],
        },
        authenticated=True,
    )
    duration_ms = round((time.monotonic() - started) * 1000)
    final_run = reconciled["run"]
    return {
        "label": label,
        "runId": run_id,
        "status": final_run["status"],
        "durationMs": duration_ms,
        "counts": {
            "total": int(final_run["totalRows"]),
            "valid": int(final_run["validRows"]),
            "warning": int(final_run["warningRows"]),
            "error": int(final_run["errorRows"]),
            "duplicate": int(final_run["duplicateRows"]),
        },
        "idempotence": {
            "new": int(staged["newRows"]),
            "changed": int(staged["changedRows"]),
            "unchanged": int(staged["unchangedRows"]),
        },
        "rowStatusCounts": count_rows(rows, "status"),
        "idempotenceStatusCounts": count_rows(rows, "idempotenceStatus"),
        "validation": {
            "canProceed": bool(validation["canProceed"]),
            "blockingErrors": int(validation["blockingErrors"]),
            "warnings": int(validation["warnings"]),
            "duplicates": int(validation["duplicates"]),
        },
        "reconciliation": {
            "mismatchCount": int(reconciled["mismatchCount"]),
            "metrics": [
                {
                    "code": metric["metricCode"],
                    "sourceValue": metric["sourceValue"],
                    "targetValue": metric["targetValue"],
                    "difference": metric["difference"],
                    "tolerance": metric["tolerance"],
                    "status": metric["status"],
                }
                for metric in reconciled["metrics"]
            ],
        },
        "apiPayloadExposureDetected": payload_exposed,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description="Execute MIG-004 dry-run #1 against the live migration API and emit sanitized evidence.")
    parser.add_argument("--api-base", default=os.environ.get("MIGRATION_API_BASE", "http://127.0.0.1:8080"))
    parser.add_argument("--username", default=os.environ.get("BOOTSTRAP_ADMIN_USERNAME", "admin"))
    parser.add_argument("--password", default=os.environ.get("BOOTSTRAP_ADMIN_PASSWORD", "Admin123!ChangeMe"))
    parser.add_argument("--totp-secret", default=os.environ.get("MIGRATION_TOTP_SECRET"))
    parser.add_argument("--source", default="scripts/migration/fixtures/legacy-employees.csv")
    parser.add_argument("--mapping", default="scripts/migration/fixtures/employee-field-mapping.csv")
    parser.add_argument("--source-system", default="SYNTHETIC_HR")
    parser.add_argument("--source-object", default="EMPLOYEE_EXPORT")
    parser.add_argument("--target-entity", default="Employee")
    parser.add_argument("--lookup-base", default=".")
    parser.add_argument("--company-code", default="MIGDRYRUN")
    parser.add_argument("--company-name", default="Migration Dry Run Company")
    parser.add_argument("--allow-review", action="store_true")
    parser.add_argument("--evidence", default="migration-output/dry-run-1-evidence.json")
    args = parser.parse_args()

    source_path = Path(args.source)
    mapping_path = Path(args.mapping)
    lookup_base = Path(args.lookup_base)
    total_started = time.monotonic()

    try:
        mappings = load_mapping(mapping_path, args.source_system, args.source_object, args.allow_review)
        if not mappings:
            raise ValueError("no approved mapping rows found for selected source object")
        stage_rows, source_row_count, transform_error_rows = prepare_rows(source_path, mappings, lookup_base)
        source_hash = sha256_file(source_path)
        mapping_hash = sha256_file(mapping_path)

        client = ApiClient(args.api_base)
        client.authenticate(args.username, args.password, args.totp_secret)
        company = get_or_create_company(client, args.company_code, args.company_name)
        company_id = str(company["id"])

        initial = run_once(
            client, "initial", company_id, args.source_system, args.source_object, args.target_entity,
            source_path, source_hash, mapping_hash, stage_rows, source_row_count, transform_error_rows,
        )
        replay = run_once(
            client, "idempotence-replay", company_id, args.source_system, args.source_object, args.target_entity,
            source_path, source_hash, mapping_hash, stage_rows, source_row_count, transform_error_rows,
        )

        gates = {
            "initialHasNoBlockingErrors": initial["counts"]["error"] == 0 and initial["validation"]["canProceed"],
            "initialReconciles": initial["status"] == "RECONCILED" and initial["reconciliation"]["mismatchCount"] == 0,
            "initialClassifiesAllRowsNew": initial["idempotence"] == {"new": source_row_count, "changed": 0, "unchanged": 0},
            "replayHasNoBlockingErrors": replay["counts"]["error"] == 0 and replay["validation"]["canProceed"],
            "replayReconciles": replay["status"] == "RECONCILED" and replay["reconciliation"]["mismatchCount"] == 0,
            "replayClassifiesAllRowsUnchanged": replay["idempotence"] == {"new": 0, "changed": 0, "unchanged": source_row_count},
            "replayMarksAllRowsDuplicate": replay["counts"]["duplicate"] == source_row_count,
            "apiDoesNotExposeProtectedPayloads": not initial["apiPayloadExposureDetected"] and not replay["apiPayloadExposureDetected"],
        }
        passed = all(gates.values())
        evidence = {
            "schemaVersion": "1.0",
            "evidenceType": "MIG-004_SYNTHETIC_TECHNICAL_BASELINE",
            "generatedAt": datetime.now(timezone.utc).isoformat(),
            "source": {
                "sourceSystem": args.source_system,
                "sourceObject": args.source_object,
                "targetEntity": args.target_entity,
                "sourceFileName": source_path.name,
                "sourceContentHash": source_hash,
                "mappingFileName": mapping_path.name,
                "mappingHash": mapping_hash,
                "rowCount": source_row_count,
                "approvedMappingCount": len(mappings),
            },
            "company": {"id": company_id, "code": str(company.get("code", args.company_code))},
            "runs": [initial, replay],
            "totalDurationMs": round((time.monotonic() - total_started) * 1000),
            "gates": gates,
            "expectedRealSourceBlockers": EXPECTED_REAL_SOURCE_BLOCKERS,
            "realSourceReady": False,
            "verdict": "PASS_SYNTHETIC_TECHNICAL_BASELINE" if passed else "FAIL_SYNTHETIC_TECHNICAL_BASELINE",
        }
        ensure_safe_evidence(evidence)
        evidence_path = Path(args.evidence)
        evidence_path.parent.mkdir(parents=True, exist_ok=True)
        evidence_path.write_text(json.dumps(evidence, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        print(
            f"MIG-004 dry-run complete: verdict={evidence['verdict']} rows={source_row_count} "
            f"initial={initial['status']} replay={replay['status']} evidence={evidence_path}"
        )
        return 0 if passed else 1
    except (OSError, ValueError, RuntimeError, KeyError, TypeError) as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
