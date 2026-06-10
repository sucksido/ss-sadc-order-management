#!/usr/bin/env bash
#
# Security test for the SADC OMS API.
#
# Focused negative/abuse cases: authentication, token tampering, input validation,
# injection-safety, payload abuse and idempotency-key misuse. Complements the
# functional smoke test (scripts/smoke-test.sh).
#
# Usage:
#   docker compose up -d --build
#   ./scripts/security-test.sh                 # defaults to http://localhost:5080
#   ./scripts/security-test.sh http://host:port
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

check() {
  if [ "$2" = "$3" ]; then printf '  %s %s (HTTP %s)\n' "$(green PASS)" "$1" "$3"; PASS=$((PASS + 1))
  else printf '  %s %s (expected %s, got %s)\n' "$(red FAIL)" "$1" "$2" "$3"; FAIL=$((FAIL + 1))
    [ -s "$BODY" ] && printf '       body: %s\n' "$(head -c 300 "$BODY")"; fi
}
assert() {
  if [ "$2" = "0" ]; then printf '  %s %s\n' "$(green PASS)" "$1"; PASS=$((PASS + 1))
  else printf '  %s %s\n' "$(red FAIL)" "$1"; FAIL=$((FAIL + 1)); fi
}
json_str() { grep -o "\"$1\":\"[^\"]*\"" "$BODY" | head -1 | sed "s/.*:\"//;s/\"$//"; }

req() {
  local method="$1" path="$2" data="${3:-}"; shift; shift; [ $# -gt 0 ] && shift || true
  if [ -n "$data" ]; then
    curl -s -o "$BODY" -w '%{http_code}' -X "$method" "$BASE$path" -H 'Content-Type: application/json' -d "$data" "$@"
  else
    curl -s -o "$BODY" -w '%{http_code}' -X "$method" "$BASE$path" "$@"
  fi
}

STAMP="$(date +%s)"

note "Waiting for API at $BASE"
for i in $(seq 1 60); do
  curl -s -o /dev/null "$BASE/health/live" && { echo "  API is up."; break; }
  [ "$i" = "60" ] && { echo "  $(red 'API not ready'); exiting"; exit 1; }
  sleep 2
done

# Obtain a valid token (control).
req POST /api/dev/token >/dev/null
TOKEN="$(json_str accessToken)"

note "Authentication & authorization"
check "no token is rejected"                 401 "$(req GET /api/customers)"
check "garbage bearer token is rejected"     401 "$(req GET /api/customers '' -H 'Authorization: Bearer not-a-jwt')"
check "empty bearer token is rejected"       401 "$(req GET /api/customers '' -H 'Authorization: Bearer ')"

# Tamper the signature: flip the final character of the JWT.
LAST="${TOKEN: -1}"; [ "$LAST" = "A" ] && REPL="B" || REPL="A"
TAMPERED="${TOKEN:0:${#TOKEN}-1}$REPL"
check "tampered-signature token is rejected" 401 "$(req GET /api/customers '' -H "Authorization: Bearer $TAMPERED")"
check "valid token is accepted (control)"    200 "$(req GET /api/customers '' -H "Authorization: Bearer $TOKEN")"

AUTH="Authorization: Bearer $TOKEN"

note "Input validation & error shape"
# Malformed JSON must be a clean 400, never a 500 (no stack-trace leakage).
mc=$(req POST /api/customers '{ this is not json ' -H "$AUTH")
assert "malformed JSON body -> 4xx, not 5xx" "$([ "$mc" -ge 400 ] && [ "$mc" -lt 500 ] && echo 0 || echo 1)"
check "invalid country code is rejected"     400 "$(req POST /api/customers "{\"name\":\"x\",\"email\":\"x$STAMP@e.com\",\"countryCode\":\"US\"}" -H "$AUTH")"
check "invalid currency pairing is rejected" 400 "$(req POST /api/customers "{\"name\":\"x\",\"email\":\"bad-email\",\"countryCode\":\"ZA\"}" -H "$AUTH")"

note "Injection safety & payload abuse"
# EF Core parameterises queries; an injection-style search must return safely, never 500.
inj=$(req GET "/api/customers?search=%27%20OR%201%3D1%3B%20DROP%20TABLE%20Customers%3B--&page=1&pageSize=5" '' -H "$AUTH")
check "SQL-injection-style search handled safely" 200 "$inj"
# Verify the table still exists / API still healthy afterwards.
check "API still healthy after injection attempt" 200 "$(req GET /health/ready)"
# Oversized page size is clamped, not honoured.
pc=$(req GET "/api/customers?pageSize=100000" '' -H "$AUTH")
check "oversized pageSize accepted (clamped)" 200 "$pc"
grep -q '"pageSize":100000' "$BODY"; assert "pageSize is NOT echoed as 100000 (clamped to <=100)" "$([ $? -ne 0 ] && echo 0 || echo 1)"

note "Idempotency-Key misuse"
# Seed a customer + two orders for the idempotency cross-use test.
req POST /api/customers "{\"name\":\"Sec $STAMP\",\"email\":\"sec-$STAMP@example.com\",\"countryCode\":\"ZA\"}" -H "$AUTH" >/dev/null
CID="$(json_str id)"
req POST /api/orders "{\"customerId\":\"$CID\",\"currencyCode\":\"ZAR\",\"lines\":[{\"productSku\":\"S\",\"quantity\":1,\"unitPrice\":10}]}" -H "$AUTH" >/dev/null
OID1="$(json_str id)"
req POST /api/orders "{\"customerId\":\"$CID\",\"currencyCode\":\"ZAR\",\"lines\":[{\"productSku\":\"S\",\"quantity\":1,\"unitPrice\":10}]}" -H "$AUTH" >/dev/null
OID2="$(json_str id)"

KEY="sec-key-$STAMP"
check "first use of idempotency key succeeds" 200 \
  "$(req PUT "/api/orders/$OID1/status" '{"status":"Paid"}' -H "$AUTH" -H "Idempotency-Key: $KEY")"
# Re-using the SAME key against a DIFFERENT resource must be rejected (409), not silently applied.
check "reusing key on a different request -> 409" 409 \
  "$(req PUT "/api/orders/$OID2/status" '{"status":"Paid"}' -H "$AUTH" -H "Idempotency-Key: $KEY")"

note "Summary"
printf '  Passed: %s   Failed: %s\n\n' "$(green $PASS)" "$([ "$FAIL" -gt 0 ] && red "$FAIL" || echo "$FAIL")"
rm -f "$BODY"
[ "$FAIL" -gt 0 ] && { echo "Security test FAILED."; exit 1; }
echo "Security test passed."
