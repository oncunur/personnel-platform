#!/usr/bin/env bash
set -euo pipefail

URL="${1:-${URL:-http://127.0.0.1:8080/api/v1/system/ping}}"
REQUESTS="${REQUESTS:-500}"
CONCURRENCY="${CONCURRENCY:-25}"
MAX_ERROR_RATE="${MAX_ERROR_RATE:-0.01}"
MAX_P95_MS="${MAX_P95_MS:-500}"
CONNECT_TIMEOUT_SECONDS="${CONNECT_TIMEOUT_SECONDS:-3}"
REQUEST_TIMEOUT_SECONDS="${REQUEST_TIMEOUT_SECONDS:-10}"

for value_name in REQUESTS CONCURRENCY MAX_P95_MS; do
  value="${!value_name}"
  if ! [[ "$value" =~ ^[1-9][0-9]*$ ]]; then
    echo "$value_name must be a positive integer." >&2
    exit 2
  fi
done

if ! [[ "$MAX_ERROR_RATE" =~ ^0(\.[0-9]+)?$|^1(\.0+)?$ ]]; then
  echo "MAX_ERROR_RATE must be between 0 and 1." >&2
  exit 2
fi

tmp_file="$(mktemp)"
trap 'rm -f "$tmp_file"' EXIT

export URL CONNECT_TIMEOUT_SECONDS REQUEST_TIMEOUT_SECONDS BEARER_TOKEN

fire_request() {
  local curl_args=(
    --silent
    --output /dev/null
    --connect-timeout "$CONNECT_TIMEOUT_SECONDS"
    --max-time "$REQUEST_TIMEOUT_SECONDS"
    --write-out '%{http_code} %{time_total}'
  )

  if [[ -n "${BEARER_TOKEN:-}" ]]; then
    curl_args+=(--header "Authorization: Bearer ${BEARER_TOKEN}")
  fi

  local output
  if output="$(curl "${curl_args[@]}" "$URL" 2>/dev/null)"; then
    printf '%s\n' "$output"
  else
    printf '000 %s\n' "$REQUEST_TIMEOUT_SECONDS"
  fi
}
export -f fire_request

start_ns="$(date +%s%N)"
seq "$REQUESTS" | xargs -P "$CONCURRENCY" -I {} bash -c 'fire_request' >>"$tmp_file"
end_ns="$(date +%s%N)"

total="$(wc -l <"$tmp_file" | tr -d ' ')"
if [[ "$total" -eq 0 ]]; then
  echo "No request results were recorded." >&2
  exit 1
fi

failed="$(awk '($1 + 0) < 200 || ($1 + 0) >= 400 {count++} END {print count+0}' "$tmp_file")"
succeeded="$((total - failed))"
mean_ms="$(awk '{sum += $2} END {printf "%.0f", (sum/NR)*1000}' "$tmp_file")"
p95_index="$(((total * 95 + 99) / 100))"
p95_seconds="$(awk '{print $2}' "$tmp_file" | sort -n | awk -v target="$p95_index" 'NR == target {print; exit}')"
p95_ms="$(awk -v seconds="${p95_seconds:-0}" 'BEGIN {printf "%.0f", seconds*1000}')"
error_rate="$(awk -v failed="$failed" -v total="$total" 'BEGIN {printf "%.6f", failed/total}')"
elapsed_seconds="$(awk -v start="$start_ns" -v end="$end_ns" 'BEGIN {printf "%.3f", (end-start)/1000000000}')"
rps="$(awk -v total="$total" -v seconds="$elapsed_seconds" 'BEGIN {if (seconds <= 0) print "0.0"; else printf "%.1f", total/seconds}')"

cat <<REPORT
Performance smoke result
------------------------
URL: $URL
Requests: $total
Concurrency: $CONCURRENCY
Succeeded: $succeeded
Failed: $failed
Error rate: $error_rate
Mean latency: ${mean_ms}ms
P95 latency: ${p95_ms}ms
Throughput: ${rps} req/s
Elapsed: ${elapsed_seconds}s
Thresholds: error_rate <= $MAX_ERROR_RATE, p95 <= ${MAX_P95_MS}ms
REPORT

failed_threshold=0
if ! awk -v actual="$error_rate" -v limit="$MAX_ERROR_RATE" 'BEGIN {exit !(actual <= limit)}'; then
  echo "ERROR: error-rate threshold exceeded." >&2
  failed_threshold=1
fi

if (( p95_ms > MAX_P95_MS )); then
  echo "ERROR: p95 latency threshold exceeded." >&2
  failed_threshold=1
fi

exit "$failed_threshold"
