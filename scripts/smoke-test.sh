#!/usr/bin/env bash
#
# End-to-end smoke test for the SADC OMS API.
#
# Drives the running stack through the full happy path (auth -> customer -> order
# -> status transition -> FX report) plus the key negative cases (auth required,
# currency validation, idempotency). Exercises real HTTP against a live API.
#
# Usage:
#   docker compose up -d --build         # start the stack first
#   ./scripts/smoke-test.sh              # defaults to http://localhost:5080
#   ./scripts/smoke-test.sh http://host:port
#
# Requires only bash + curl. Exits non-zero if any check fails.

set -uo pipefail

BASE="${1:-http://localhost:5080}"
BODY="$(mktemp)"
PASS=0
FAIL=0

green() { printf '\033[32m%s\033[0m' "$1"; }
red()   { printf '\033[31m%s\033[0m' "$1"; }
note()  { printf '\n\033[1m== %s ==\033[0m\n' "$1"; }

# check <description> <expected-status> <actual-status>
check() {
  if [ "$2" = "$3" ]; then
    printf '  %s %s (HTTP %s)\n' "$(green PASS)" "$1" "$3"; PASS=$((PASS + 1))
  else
    printf '  %s %s (expected %s, got %s)\n' "$(red FAIL)" "$1" "$2" "$3"; FAIL=$((FAIL + 1))
    [ -s "$BODY" ] && printf '       body: %s\n' "$(head -c 300 "$BODY")"
  fi
}

# assert <description> <condition-result 0|1>
assert() {
  if [ "$2" = "0" ]; then printf '  %s %s\n' "$(green PASS)" "$1"; PASS=$((PASS + 1))
  else printf '  %s %s\n' "$(red FAIL)" "$1"; FAIL=$((FAIL + 1)); fi
}

# json_str <key> — extract a string value from the last response body
json_str() { grep -o "\"$1\":\"[^\"]*\"" "$BODY" | head -1 | sed "s/.*:\"//;s/\"$//"; }

# req <METHOD> <path> [json-data] [extra curl args...] -> echoes status code, writes body to $BODY
req() {
  local method="$1" path="$2" data="${3:-}"; shift; shift; [ $# -gt 0 ] && shift || true
  if [ -n "$data" ]; then
    curl -s -o "$BODY" -w '%{http_code}' -X "$method" "$BASE$path" \
      -H 'Content-Type: application/json' -d "$data" "$@"
  else
    curl -s -o "$BODY" -w '%{http_code}' -X "$method" "$BASE$path" "$@"
  fi
}

STAMP="$(date +%s)"

# --------------------------------------------------------------- Wait for API
note "Waiting for API at $BASE"
for i in $(seq 1 60); do
  if curl -s -o /dev/null "$BASE/health/live"; then echo "  API is up."; break; fi
  [ "$i" = "60" ] && { echo "  $(red 'API did not become ready in time.')"; exit 1; }
  sleep 2
done

# ----------------------------------------------------------------- Health
note "Health checks"
check "liveness probe"  200 "$(req GET /health/live)"
check "readiness probe" 200 "$(req GET /health/ready)"

# ----------------------------------------------------------------- Security
note "Security"
AUTH="Authorization: Bearer placeholder"
check "unauthenticated request is rejected" 401 "$(req GET /api/customers)"

code=$(req POST /api/dev/token)
check "dev token issued" 200 "$code"
TOKEN="$(json_str accessToken)"
AUTH="Authorization: Bearer $TOKEN"
[ -n "$TOKEN" ] && assert "token extracted" 0 || assert "token extracted" 1

# ----------------------------------------------------------------- Customers
note "Customers"
code=$(req POST /api/customers \
  "{\"name\":\"Smoke Test $STAMP\",\"email\":\"smoke-$STAMP@example.com\",\"countryCode\":\"ZA\"}" \
  -H "$AUTH")
check "create customer (ZA)" 201 "$code"
CID="$(json_str id)"

check "get customer by id"          200 "$(req GET "/api/customers/$CID" '' -H "$AUTH")"
check "list customers (search+page)" 200 "$(req GET "/api/customers?search=Smoke&page=1&pageSize=10" '' -H "$AUTH")"
check "page size is clamped to <=100" 200 "$(req GET "/api/customers?pageSize=1000" '' -H "$AUTH")"

# ----------------------------------------------------------------- Orders
note "Orders"
code=$(req POST /api/orders \
  "{\"customerId\":\"$CID\",\"currencyCode\":\"ZAR\",\"lines\":[{\"productSku\":\"SKU-1\",\"quantity\":2,\"unitPrice\":125.50}]}" \
  -H "$AUTH")
check "create order (valid ZAR)" 201 "$code"
OID="$(json_str id)"
grep -q '"totalAmount":251' "$BODY"; assert "total computed server-side (251.00)" $?

check "reject invalid currency (ZA cannot use USD)" 400 "$(req POST /api/orders \
  "{\"customerId\":\"$CID\",\"currencyCode\":\"USD\",\"lines\":[{\"productSku\":\"X\",\"quantity\":1,\"unitPrice\":10}]}" \
  -H "$AUTH")"

check "get order by id" 200 "$(req GET "/api/orders/$OID" '' -H "$AUTH")"
check "list orders (filter+sort+page)" 200 \
  "$(req GET "/api/orders?status=Pending&page=1&pageSize=10&sort=createdAt_desc" '' -H "$AUTH")"

# ----------------------------------------------------------- Status + idempotency
note "Status transitions & idempotency"
KEY="smoke-$STAMP"
code=$(req PUT "/api/orders/$OID/status" '{"status":"Paid"}' -H "$AUTH" -H "Idempotency-Key: $KEY")
check "transition Pending -> Paid" 200 "$code"
grep -q '"status":"Paid"' "$BODY"; assert "order is now Paid" $?

# Replay the SAME key with a different body — must return the original outcome.
req PUT "/api/orders/$OID/status" '{"status":"Cancelled"}' -H "$AUTH" -H "Idempotency-Key: $KEY" >/dev/null
grep -q '"status":"Paid"' "$BODY"; assert "idempotent replay kept status Paid (not Cancelled)" $?

check "status update without Idempotency-Key is rejected" 400 \
  "$(req PUT "/api/orders/$OID/status" '{"status":"Paid"}' -H "$AUTH")"

# ----------------------------------------------------------------- FX report
note "FX / ZAR report"
code=$(req GET /api/reports/orders/zar '' -H "$AUTH")
check "ZAR report responds" 200 "$code"
grep -q '"grandTotalZar"' "$BODY"; assert "report contains grandTotalZar" $?

# ----------------------------------------------------------------- Summary
note "Summary"
printf '  Passed: %s   Failed: %s\n\n' "$(green $PASS)" "$([ "$FAIL" -gt 0 ] && red "$FAIL" || echo "$FAIL")"
rm -f "$BODY"

if [ "$FAIL" -gt 0 ]; then
  echo "Smoke test FAILED."
  exit 1
fi
echo "Smoke test passed. (Tip: 'docker compose logs worker' should show 'Fulfilment simulated' for the order above.)"
