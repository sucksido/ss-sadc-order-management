# SADC Order Management System

A full-stack order management system for the Southern African Development Community (SADC)
region. It manages customers, orders and line items through the order lifecycle
(`Pending → Paid → Fulfilled`, with cancellation before fulfilment), validates currency
against the customer's country using SADC and Common Monetary Area rules, publishes an
`OrderCreated` event reliably via a transactional outbox, and consumes that event in a
background worker that simulates fulfilment.

The solution is built with ASP.NET Core (.NET 8), EF Core (code-first) on SQL Server,
RabbitMQ for messaging, and a React + TypeScript front-end.

> This is an assessment project. It favours clear architecture, correct business rules and
> demonstrable engineering practice over feature completeness. Assumptions and trade-offs are
> called out throughout this document and in [ANSWERS.md](ANSWERS.md).

---

## Solution architecture

The backend follows a layered (clean-architecture-influenced) design. Dependencies point
inward — the domain knows nothing about EF Core, RabbitMQ or ASP.NET.

```
SadcOms.Domain          Entities, value rules, order lifecycle, SADC/CMA validation.
                        No third-party dependencies. Unit-tested in isolation.

SadcOms.Contracts       Versioned integration-event contracts shared by API and Worker.

SadcOms.Application     Use-case services, DTOs, FluentValidation validators, and the
                        abstractions infrastructure implements (IAppDbContext,
                        IEventPublisher, IFxRateProvider). Holds the outbox/idempotency
                        entities.

SadcOms.Infrastructure  EF Core DbContext + configurations + migrations, RabbitMQ
                        publisher/connection, the outbox dispatcher, and the mocked FX
                        provider. The only project that knows a concrete DB or broker.

SadcOms.Api             Controllers, middleware (correlation id, ProblemDetails), JWT auth,
                        API versioning, Swagger, health checks, OpenTelemetry wiring.

SadcOms.Worker          Background service that consumes OrderCreated and simulates
                        downstream fulfilment, with retry and dead-lettering.

src/web                 React + TypeScript (Vite) front-end.
```

Why this shape: the business rules (totals, lifecycle, currency validity) are the part most
worth protecting from change, so they live in a dependency-free core that is fast and trivial
to test. Everything volatile — the database, the broker, the web framework — sits at the edge
behind interfaces. See [ANSWERS.md](ANSWERS.md) for the longer discussion.

### Request → event flow

1. `POST /api/orders` validates input, builds the `Order` aggregate (total computed
   server-side, currency checked against the customer's country), and **in one transaction**
   writes both the order and an `OrderCreated` row to the **outbox**.
2. The **outbox dispatcher** (hosted in the API) polls unprocessed rows and publishes them to
   RabbitMQ with publisher confirms, then marks them processed.
3. The **worker** consumes `OrderCreated`, simulates fulfilment with in-process retry, acks on
   success, and dead-letters poison messages.

This removes the dual-write problem: the event can never be lost relative to the order, and is
never published for an order that rolled back.

---

## Tech stack

| Concern        | Choice                                                            |
| -------------- | ----------------------------------------------------------------- |
| API            | ASP.NET Core 8, controllers, API versioning, Swagger/OpenAPI      |
| Persistence    | EF Core 8 (code-first), SQL Server, optimistic concurrency        |
| Messaging      | RabbitMQ (topic exchange + DLQ), transactional outbox             |
| Validation     | FluentValidation (request) + domain invariants (business rules)   |
| Auth           | JWT bearer — Microsoft Entra in prod, symmetric dev key locally   |
| Observability  | Serilog (structured logs + correlation id), OpenTelemetry traces/metrics, health checks |
| Front-end      | React 18 + TypeScript, Vite, React Router                         |
| Tests          | xUnit, FluentAssertions, EF in-memory, `WebApplicationFactory`    |

---

## Running it

### Option A — Docker Compose (everything)

Requires Docker. From the repository root:

```bash
docker compose up --build
```

This starts SQL Server, RabbitMQ, the API, the worker and the web app. The API runs in the
`Development` profile so it auto-applies migrations and seeds sample data.

| Service        | URL                                            |
| -------------- | ---------------------------------------------- |
| Web app        | http://localhost:5173                          |
| API + Swagger  | http://localhost:5080/swagger                  |
| RabbitMQ admin | http://localhost:15672  (guest / guest)        |
| SQL Server     | localhost,1433  (sa / Your_strong_Pass123)     |

### Option B — Run locally

Prerequisites: .NET 8 SDK, Node 20+, and SQL Server + RabbitMQ reachable (the compose file
can provide just those: `docker compose up sqlserver rabbitmq`).

```bash
# API (auto-migrates + seeds in Development, serves Swagger at /swagger)
dotnet run --project src/SadcOms.Api

# Worker (separate terminal)
dotnet run --project src/SadcOms.Worker

# Front-end (separate terminal)
cd src/web && npm install && npm run dev   # http://localhost:5173
```

### Authentication for trying it out

Endpoints require a bearer token. In development the API exposes a helper that mints one:

```bash
curl -X POST http://localhost:5080/api/dev/token
```

Use the returned `accessToken` as `Authorization: Bearer <token>`. In Swagger, click
**Authorize** and paste it. The front-end has a **Sign in (dev token)** button that does this
for you. In production this endpoint is disabled and tokens come from Microsoft Entra.

A Postman collection is provided at [`docs/SadcOms.postman_collection.json`](docs/SadcOms.postman_collection.json)
— run *Auth / Get dev token* first and the rest chain automatically.

---

## API summary

All routes are under `/api` and versioned (default `1.0`; a future version can be selected
with the `X-Api-Version` header or `?api-version=`).

| Method | Route                                                          | Notes                              |
| ------ | -------------------------------------------------------------- | ---------------------------------- |
| POST   | `/api/customers`                                               | Create customer                    |
| GET    | `/api/customers/{id}`                                          | Get by id                          |
| GET    | `/api/customers?search=&page=&pageSize=`                       | Search + paginate (pageSize ≤ 100) |
| POST   | `/api/orders`                                                  | Create order (total computed server-side) |
| GET    | `/api/orders/{id}`                                             | Get with line items                |
| GET    | `/api/orders?customerId=&status=&page=&pageSize=&sort=`        | Filter, sort, paginate             |
| PUT    | `/api/orders/{id}/status`                                      | Transition status; **requires `Idempotency-Key` header** |
| GET    | `/api/reports/orders/zar?status=&since=`                       | Order totals converted to ZAR      |
| GET    | `/health/live`, `/health/ready`                                | Liveness / readiness               |

Errors are returned as RFC 7807 `application/problem+json`, including the `correlationId`.

---

## Testing

```bash
dotnet test                      # unit + integration tests
cd src/web && npm run build      # front-end type-check + build
```

- **Unit tests** cover the domain rules (SADC/CMA validation, order totals, lifecycle state
  machine), the application services (create order + outbox write, idempotent status updates,
  filtering/paging), and the FX provider.
- **Integration tests** drive the real HTTP pipeline with `WebApplicationFactory`, swapping
  SQL Server for the EF in-memory provider and stubbing the broker, while still exercising
  authentication and validation.

The full testing strategy (test pyramid) is described in [ANSWERS.md](ANSWERS.md).

---

## Database & migrations

EF Core code-first with three migrations that demonstrate schema evolution:

- `InitialCreate` — Customers, Orders, OrderLineItems.
- `AddOrderRowVersion` — adds the `rowversion` concurrency token to Orders.
- `AddOutbox` — adds the OutboxMessages and IdempotencyRecords tables.

```bash
dotnet ef migrations add <Name> --project src/SadcOms.Infrastructure --startup-project src/SadcOms.Api
dotnet ef database update      --project src/SadcOms.Infrastructure --startup-project src/SadcOms.Api
dotnet ef migrations script --idempotent -o migrations.sql --project src/SadcOms.Infrastructure --startup-project src/SadcOms.Api
```

An equivalent idempotent script is checked in at [`docs/migrations.sql`](docs/migrations.sql).
Migration, zero-downtime and rollback strategy — plus how CI validates migrations — are in
[ANSWERS.md](ANSWERS.md).

---

## Configuration

Settings bind from `appsettings.json`, environment variables (double-underscore syntax, e.g.
`ConnectionStrings__SqlServer`) and user-secrets. Key sections:

- `ConnectionStrings:SqlServer`
- `RabbitMq:*` (host, exchange, queues, dead-letter)
- `Jwt:*` (`Authority` for Entra; `DevSigningKey` for local tokens)
- `Outbox:*` (poll interval, batch size, max attempts)
- `Fx:CacheDuration`
- `OpenTelemetry:OtlpEndpoint` (empty → console exporter)

---

## Assumptions & trade-offs

- **Lifecycle.** The brief lists `Pending → Paid → Fulfilled → Cancelled`. I read that as the
  set of states, not a literal path, and implemented a state machine where cancellation is
  allowed from `Pending` or `Paid`, and `Fulfilled`/`Cancelled` are terminal — cancelling an
  already-dispatched order is not a sensible business move.
- **Entra is mocked.** Real JWT validation is wired up against an `Authority`; with none
  configured the API validates a symmetric dev key and exposes `POST /api/dev/token`. No real
  tenant is required to run the project.
- **FX is mocked.** Rates are fixed/illustrative behind `IFxRateProvider`; CMA currencies
  convert to ZAR at par. Results are cached per currency pair.
- **Idempotency** is implemented server-side via an `IdempotencyRecords` table keyed by the
  `Idempotency-Key` header, storing the original response for replay.
- **The application layer references EF Core's abstractions** (`DbSet`/`SaveChanges`) for
  expressive queries — a deliberate, common trade-off rather than a full repository
  abstraction. Rationale in ANSWERS.md.
- Not production-hardened: no rate limiting, secrets are local-dev placeholders, and the
  worker's de-duplication is in-memory (a durable store is noted as the production choice).
