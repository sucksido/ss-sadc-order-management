#!/usr/bin/env bash
#
# Lightweight performance test (no external tooling — bash + curl only).
# Fires N requests at the order-list endpoint with C concurrent workers and
# reports throughput plus latency percentiles. Use scripts/load-test.js (k6)
# for a richer, staged load profile.
#
# Usage:
#   docker compose up -d --build
#   ./scripts/perf-test.sh                      # defaults: 500 requests, 20 concurrent
#   ./scripts/perf-test.sh <requests> <concurrency> <base-url>

set -uo pipefail

REQUESTS="${1:-500}"
CONCURRENCY="${2:-20}"
BASE="${3:-http://localhost:5080}"
TMP="$(mktemp -d)"

green() { printf '\033[32m%s\033[0m' "$1"; }
red()   { printf '\033[31m%s\033[0m' "$1"; }

echo "Authenticating…"
TOKEN=$(curl -s -X POST "$BASE/api/dev/token" | grep -o '"accessToken":"[^"]*"' | sed 's/.*:"//;s/"$//')
[ -z "$TOKEN" ] && { echo "$(red 'Could not obtain token — is the stack up?')"; exit 1; }

echo "Firing $REQUESTS requests, $CONCURRENCY concurrent, at $BASE/api/orders …"
START=$(date +%s)

# Each worker records per-request latency (seconds) and HTTP status to its own file.
worker() {
  local id="$1" n="$2"
  for ((i = 0; i < n; i++)); do
    curl -s -o /dev/null -w '%{time_total} %{http_code}\n' \
      -H "Authorization: Bearer $TOKEN" \
      "$BASE/api/orders?page=1&pageSize=20&sort=createdAt_desc" >> "$TMP/w$id"
  done
}

per=$((REQUESTS / CONCURRENCY))
for ((w = 0; w < CONCURRENCY; w++)); do worker "$w" "$per" & done
wait

END=$(date +%s)
ELAPSED=$((END - START)); [ "$ELAPSED" -eq 0 ] && ELAPSED=1

cat "$TMP"/w* > "$TMP/all" 2>/dev/null
TOTAL=$(wc -l < "$TMP/all" | tr -d ' ')
ERRORS=$(awk '$2 !~ /^2/ {c++} END{print c+0}' "$TMP/all")
RPS=$((TOTAL / ELAPSED))

# Latency percentiles (ms) from sorted times.
awk '{print $1*1000}' "$TMP/all" | sort -n > "$TMP/sorted"
pct() { awk -v p="$1" 'BEGIN{c=0} {a[c++]=$1} END{if(c==0){print "0"; exit} idx=int((p/100)*c); if(idx>=c)idx=c-1; printf "%.0f", a[idx]}' "$TMP/sorted"; }
AVG=$(awk '{s+=$1;c++} END{if(c==0){print 0}else printf "%.0f", s/c}' "$TMP/sorted")

echo
echo "----------------------------------------"
echo " Requests:     $TOTAL"
echo " Errors:       $ERRORS"
echo " Duration:     ${ELAPSED}s"
echo " Throughput:   ~${RPS} req/s"
echo " Latency avg:  ${AVG} ms"
echo " Latency p50:  $(pct 50) ms"
echo " Latency p95:  $(pct 95) ms"
echo " Latency p99:  $(pct 99) ms"
echo "----------------------------------------"

rm -rf "$TMP"
if [ "$ERRORS" -gt 0 ]; then echo "$(red "FAIL: $ERRORS non-2xx responses")"; exit 1; fi
echo "$(green 'PASS: no errors under load.')"
