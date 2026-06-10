# Testing strategy

This project is verified at multiple levels. The table maps each testing type to the concrete
artifacts that exercise it and how to run them. Everything below the line is reproducible.

| Test type | Where it lives | How it's satisfied |
| --- | --- | --- |
| **Unit** | `tests/SadcOms.UnitTests` | Domain rules (SADC/CMA validation, order totals & rounding, lifecycle state machine), application services (with EF in-memory + fakes), and the FX provider. |
| **Integration** | `tests/SadcOms.IntegrationTests` | Real ASP.NET Core pipeline via `WebApplicationFactory` (routing, model binding, validation, auth, ProblemDetails) with the DB swapped for in-memory and the broker stubbed. |
| **Functional** | integration tests + `scripts/smoke-test.sh` | Each business requirement (server-side totals, currency validation, pagination, status transitions, idempotency, FX report) is asserted against the API as a black box. |
| **System** | `docker-compose.yml` + `scripts/smoke-test.sh` + worker logs | The whole system runs together (API, Worker, SQL Server, RabbitMQ, web). The end-to-end order→event→fulfilment flow is confirmed in the worker log (`Fulfilment simulated for order …`). |
| **Smoke** | `scripts/smoke-test.sh` | 20 fast checks across health, auth, customers, orders, status/idempotency and the ZAR report — a quick "is it alive and correct" pass after any deploy. |
| **Security** | `scripts/security-test.sh` | Authn (missing/garbage/empty/tampered tokens → 401), input validation (malformed JSON → 4xx not 5xx), injection-safety (parameterised queries survive an injection-style search), payload abuse (page-size clamping), and idempotency-key misuse (key reuse across resources → 409). |
| **Performance** | `scripts/load-test.js` (k6), `scripts/perf-test.sh` (curl) | Staged load against read + write paths with thresholds: `http_req_failed < 1%` and `p(95) < 500ms`. The bash variant reports throughput and p50/p95/p99 with no tooling to install. |
| **Regression** | `dotnet test` in CI (`.github/workflows/ci.yml`) | The full unit + integration suite runs on every push, plus an EF "pending model changes" check that fails the build if the schema and migrations drift. |

## Running everything

```bash
# 1. Unit + integration + regression (no external services needed)
dotnet test

# 2. Front-end build / type-check
cd src/web && npm run build && cd ../..

# 3. Bring up the full system
docker compose up -d --build

# 4. Smoke + functional + system
./scripts/smoke-test.sh
docker compose logs worker | grep "Fulfilment simulated"   # confirms async fulfilment

# 5. Security
./scripts/security-test.sh

# 6. Performance (choose one)
#    a) k6 via Docker (no install):
docker run --rm -e BASE_URL=http://host.docker.internal:5080 \
  -v "$PWD/scripts:/scripts" grafana/k6 run /scripts/load-test.js
#    b) zero-dependency bash:
./scripts/perf-test.sh 500 20

# 7. Tear down
docker compose down
```

## Notes & scope

- The performance thresholds are sized for a developer laptop running the full stack in Docker;
  they validate that the read/write paths stay responsive under concurrency and that nothing
  errors under load — not absolute production capacity. The scaling approach for 50k orders/min
  is discussed in [ANSWERS.md](../ANSWERS.md).
- Security testing here covers the application surface (authn/authz, validation, injection-safety,
  idempotency). Infrastructure hardening (TLS, secret management, rate limiting, WAF) is called
  out in ANSWERS.md as production follow-ups rather than implemented in this assessment.
- Acceptance/UAT and contract testing are out of scope for a take-home, but the integration and
  smoke suites double as executable acceptance checks for the documented requirements.
