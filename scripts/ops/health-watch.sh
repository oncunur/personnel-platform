#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${1:-${BASE_URL:-http://127.0.0.1:8080}}"
BASE_URL="${BASE_URL%/}"
CONNECT_TIMEOUT_SECONDS="${CONNECT_TIMEOUT_SECONDS:-3}"
REQUEST_TIMEOUT_SECONDS="${REQUEST_TIMEOUT_SECONDS:-10}"
MAX_INTEGRATION_BACKLOG="${MAX_INTEGRATION_BACKLOG:-500}"
MAX_INTEGRATION_ERRORS="${MAX_INTEGRATION_ERRORS:-50}"
MAX_INTEGRATION_DEAD_LETTERS="${MAX_INTEGRATION_DEAD_LETTERS:-0}"

for value_name in MAX_INTEGRATION_BACKLOG MAX_INTEGRATION_ERRORS MAX_INTEGRATION_DEAD_LETTERS; do
  value="${!value_name}"
  if ! [[ "$value" =~ ^[0-9]+$ ]]; then
    echo "$value_name must be a non-negative integer." >&2
    exit 2
  fi
done

probe() {
  local name="$1"
  local url="$2"
  if curl --fail --silent --show-error \
    --connect-timeout "$CONNECT_TIMEOUT_SECONDS" \
    --max-time "$REQUEST_TIMEOUT_SECONDS" \
    --output /dev/null \
    "$url"; then
    echo "OK   $name"
  else
    echo "FAIL $name ($url)" >&2
    return 1
  fi
}

failed=0
probe "API liveness" "$BASE_URL/health/live" || failed=1
probe "API readiness (PostgreSQL + Redis)" "$BASE_URL/health/ready" || failed=1
probe "API ping" "$BASE_URL/api/v1/system/ping" || failed=1

if [[ -n "${BEARER_TOKEN:-}" || -n "${COMPANY_ID:-}" ]]; then
  if [[ -z "${BEARER_TOKEN:-}" || -z "${COMPANY_ID:-}" ]]; then
    echo "FAIL integration monitoring requires both BEARER_TOKEN and COMPANY_ID." >&2
    failed=1
  elif ! command -v jq >/dev/null 2>&1; then
    echo "FAIL jq is required when integration monitoring is enabled." >&2
    failed=1
  else
    monitoring_file="$(mktemp)"
    trap 'rm -f "${monitoring_file:-}"' EXIT

    if curl --fail --silent --show-error \
      --connect-timeout "$CONNECT_TIMEOUT_SECONDS" \
      --max-time "$REQUEST_TIMEOUT_SECONDS" \
      --header "Authorization: Bearer ${BEARER_TOKEN}" \
      --output "$monitoring_file" \
      "$BASE_URL/api/v1/integrations/monitoring?companyId=$COMPANY_ID"; then
      backlog="$(jq -r '.totalBacklog // 0' "$monitoring_file")"
      errors="$(jq -r '.totalErrors // 0' "$monitoring_file")"
      dead_letters="$(jq -r '.totalDeadLetters // 0' "$monitoring_file")"

      echo "INFO integration backlog=$backlog errors=$errors deadLetters=$dead_letters"

      if (( backlog > MAX_INTEGRATION_BACKLOG )); then
        echo "FAIL integration backlog threshold exceeded: $backlog > $MAX_INTEGRATION_BACKLOG" >&2
        failed=1
      fi
      if (( errors > MAX_INTEGRATION_ERRORS )); then
        echo "FAIL integration error threshold exceeded: $errors > $MAX_INTEGRATION_ERRORS" >&2
        failed=1
      fi
      if (( dead_letters > MAX_INTEGRATION_DEAD_LETTERS )); then
        echo "FAIL integration dead-letter threshold exceeded: $dead_letters > $MAX_INTEGRATION_DEAD_LETTERS" >&2
        failed=1
      fi
    else
      echo "FAIL authenticated integration monitoring endpoint." >&2
      failed=1
    fi
  fi
else
  echo "SKIP integration business-health probe (set BEARER_TOKEN and COMPANY_ID to enable)."
fi

exit "$failed"
