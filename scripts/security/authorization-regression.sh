#!/usr/bin/env bash
set -euo pipefail

: "${API_BASE:?Set API_BASE, e.g. http://localhost:8080}"
: "${TOKEN_COMPANY_A:?Bearer token with personnel/document/report permissions scoped only to company A}"
: "${TOKEN_LOW_PRIV:?Bearer token without security administration and sensitive reveal permissions}"
: "${COMPANY_B_EMPLOYEE_ID:?Employee id belonging to inaccessible company B}"
: "${COMPANY_B_DOCUMENT_ID:?Employee document id belonging to inaccessible company B}"
: "${LOW_PRIV_SCOPED_EMPLOYEE_ID:?Employee id inside TOKEN_LOW_PRIV company scope}"
: "${OTHER_USER_EXPORT_JOB_ID:?Completed export job owned by another user}"

PASS=0
FAIL=0

request_status() {
  local token="$1" method="$2" path="$3" body="${4:-}"
  local args=(-sS -o /tmp/pp-security-body -w '%{http_code}' -X "$method" -H "Authorization: Bearer $token")
  if [[ -n "$body" ]]; then
    args+=(-H 'Content-Type: application/json' --data "$body")
  fi
  curl "${args[@]}" "$API_BASE$path"
}

expect_denied() {
  local name="$1" token="$2" method="$3" path="$4" body="${5:-}"
  local status
  status=$(request_status "$token" "$method" "$path" "$body")
  if [[ "$status" == "403" || "$status" == "404" ]]; then
    printf 'PASS %-38s HTTP %s\n' "$name" "$status"
    PASS=$((PASS + 1))
  else
    printf 'FAIL %-38s HTTP %s body=%s\n' "$name" "$status" "$(cat /tmp/pp-security-body)"
    FAIL=$((FAIL + 1))
  fi
}

expect_status() {
  local name="$1" expected="$2" token="$3" method="$4" path="$5" body="${6:-}"
  local status
  status=$(request_status "$token" "$method" "$path" "$body")
  if [[ "$status" == "$expected" ]]; then
    printf 'PASS %-38s HTTP %s\n' "$name" "$status"
    PASS=$((PASS + 1))
  else
    printf 'FAIL %-38s expected=%s actual=%s body=%s\n' "$name" "$expected" "$status" "$(cat /tmp/pp-security-body)"
    FAIL=$((FAIL + 1))
  fi
}

# IDOR / company-scope bypass: object exists but caller has no company scope.
expect_denied "personnel IDOR/company scope" "$TOKEN_COMPANY_A" GET "/api/v1/personnel/employees/$COMPANY_B_EMPLOYEE_ID"
expect_denied "document file cross-company" "$TOKEN_COMPANY_A" GET "/api/v1/documents/employee-documents/$COMPANY_B_DOCUMENT_ID/file"

# Secure export ownership: caller has export permission but must not download another user's job.
expect_denied "report export ownership" "$TOKEN_COMPANY_A" GET "/api/v1/reports/exports/$OTHER_USER_EXPORT_JOB_ID/file"

# Permission bypass: object is deliberately inside the caller's scope, therefore denial must come from reveal permission.
expect_status "sensitive reveal permission" 403 "$TOKEN_LOW_PRIV" GET "/api/v1/personnel/employees/$LOW_PRIV_SCOPED_EMPLOYEE_ID/sensitive?reveal=true"

# Role escalation must be rejected by the endpoint permission gate before service-level target lookup.
expect_status "role escalation permission gate" 403 "$TOKEN_LOW_PRIV" PUT "/api/v1/security/users/00000000-0000-0000-0000-000000000001/roles" '{"roleIds":[]}'

printf '\nAuthorization regression: %d passed, %d failed\n' "$PASS" "$FAIL"
[[ "$FAIL" -eq 0 ]]
