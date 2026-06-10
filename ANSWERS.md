# ANSWERS

Design rationale, the architecture Q&A, the SQL section, and notes on the advanced
enhancements implemented.

---

## Part 2 — Architecture & Design

### 1. How the system architecture is structured

The backend is layered with dependencies pointing inward (clean-architecture style):

- **Domain** — entities and the business invariants: order totals, the lifecycle state
  machine, and SADC/CMA currency validation. No third-party references, so it is fast and
  trivial to unit test, and the rules can't be bypassed by a controller or a query.
- **Application** — use-case services (`OrderService`, `CustomerService`, `ZarReportService`),
  DTOs, FluentValidation validators, and the abstractions infrastructure implements
  (`IAppDbContext`, `IEventPublisher`, `IFxRateProvider`).
- **Infrastructure** — EF Core (DbContext, configurations, migrations), the RabbitMQ
  publisher/connection, the outbox dispatcher and the mocked FX provider.
- **Api** — the HTTP edge: controllers, middleware, auth, versioning, Swagger, observability.
- **Worker** — a separate process that consumes `OrderCreated`.
- **Contracts** — versioned integration-event records shared by the API and Worker.

The aggregate boundary is the `Order` (it owns its line items and is the only place totals and
status change). Cross-process communication is asynchronous via events. The API and Worker
are independently deployable and scalable, which matters for the write-heavy scenario in Q2.

I kept it a modular monolith rather than splitting into microservices: the bounded contexts
here (ordering, fulfilment) are small and share a region/currency model, so the operational
cost of separate services isn't justified yet. The layering means a context could later be
extracted with little churn.

### 2. Scaling to 50k orders/min peak writes

50k/min ≈ 830 writes/sec sustained, with bursts higher. Approach, in order of impact:

- **Stateless, horizontally-scaled API** behind a load balancer; no session affinity. Add
  instances to absorb burst.
- **Don't do synchronous downstream work on the write path.** Order creation is a single
  insert plus an outbox insert in one transaction; fulfilment happens asynchronously via the
  worker. This keeps the write transaction tiny.
- **Database write throughput** is the real bottleneck:
  - Sequential GUID keys (`NEWSEQUENTIALID()` / sequential `Guid`s) to avoid index-page
    fragmentation and hot-page latch contention from random GUID inserts. (For extreme write
    rates, a `bigint` identity or Snowflake-style key removes 8 bytes/row and index churn.)
  - Keep indexes minimal on the hot table; every nonclustered index is extra write cost.
  - Consider table partitioning by month (Q8) so inserts hit the current partition and old
    data is cheap to archive.
- **The outbox dispatcher batches** and can be sharded (claim ranges with `UPDLOCK, READPAST`)
  so multiple dispatchers publish in parallel without double-sending.
- **Scale-out reads** with read replicas; route list/report queries to replicas.
- **Buffer the spike**: if writes still exceed DB capacity, accept the order onto a durable
  queue and persist asynchronously, trading immediate consistency for throughput. I'd only do
  this if measurements demand it.
- **Backpressure**: prefetch limits on the consumer, bounded thread pools, and 429s with
  `Retry-After` at the edge once the system is saturated, rather than collapsing.

I'd validate each step with load tests rather than guessing — the first three usually get you
most of the way.

### 3. Ensuring message reliability

- **Transactional outbox** (implemented). The event is written in the same transaction as the
  order, so producing the message is atomic with the state change. A dispatcher publishes
  asynchronously, retrying failures; nothing is lost if the broker is briefly down.
- **Publisher confirms** on the RabbitMQ side — a publish only counts as done once the broker
  acknowledges it (`WaitForConfirmsOrDie`), with persistent (`DeliveryMode=2`) messages on
  durable queues/exchanges.
- **At-least-once delivery + idempotent consumers.** Delivery can duplicate, so the consumer
  de-duplicates by `EventId` and processing is designed to be repeatable. (In this project the
  de-dupe set is in-memory; production would use a durable processed-events table or Redis.)
- **Manual acks**: the worker only acks after successful processing; failures are retried
  in-process and then **dead-lettered** (the queue has an `x-dead-letter-exchange`) so poison
  messages are quarantined for inspection instead of hot-looping.
- **Ordering**: not globally guaranteed; events carry enough state to be processed
  independently. If per-entity ordering were required I'd partition by a routing key
  (e.g. `OrderId`) onto a single consumer per partition.

### 4. API versioning strategy

The API uses `Asp.Versioning` with a default of `v1.0`. The brief's routes are unversioned
(`/api/customers`), so I kept those paths and made the version selectable by header
(`X-Api-Version`) or query string, defaulting to 1.0. This keeps existing URLs stable while
leaving a clean path to `v2`.

For a public API I'd generally prefer **URI versioning** (`/api/v1/...`) for cache/proxy
friendliness and obviousness, switching to header/media-type versioning where clients prefer
stable URLs. The principles either way: additive changes are non-breaking (new optional
fields, new endpoints) and don't bump the version; breaking changes (removing/renaming fields,
changing semantics) introduce `v2` and `v1` is supported through a deprecation window
(communicated via `Sunset`/`Deprecation` headers). DTOs are versioned separately from domain
types so internal refactors don't leak.

### 5. Security considerations (Microsoft Entra / JWT)

- **Authn**: JWT bearer. In production the API validates tokens against an Entra `Authority`
  (issuer, audience, signature, expiry checked against Entra's published JWKS). Locally, with
  no authority configured, it validates a symmetric dev key and offers `POST /api/dev/token`
  so the project runs without a tenant. The dev endpoint disables itself outside development.
- **Authz**: a baseline policy requires an authenticated principal; the natural next step is
  scope/role claims from Entra (e.g. `orders.read`, `orders.write`) enforced per endpoint, and
  app roles or groups for coarser access.
- **Transport & secrets**: HTTPS/HSTS in production; secrets from Key Vault / managed identity,
  never in source. The committed dev key is clearly labelled local-only.
- **Input & output**: model + FluentValidation validation, EF parameterisation (no string SQL)
  to prevent injection, and ProblemDetails that don't leak internals (500s hide the message and
  reference a correlation id).
- **Other hardening I'd add**: rate limiting, CORS locked to known origins (already restricted),
  security headers, audit logging of state changes, and least-privilege DB credentials.

### 6. Testing strategy (test pyramid)

- **Base — unit tests (most numerous, fastest).** Pure domain logic with no I/O: SADC/CMA
  validation, total calculation/rounding, the lifecycle state machine. Application services
  are tested against the EF in-memory provider with fakes for the broker/FX, covering the
  outbox write, idempotent replays, filtering and paging.
- **Middle — integration tests.** `WebApplicationFactory` drives the real HTTP pipeline
  (routing, model binding, validation, auth, ProblemDetails) with SQL Server swapped for the
  in-memory provider and the broker stubbed. These catch wiring/contract issues units miss.
- **Top — end-to-end (fewest).** Out of scope here, but I'd run a handful against the full
  docker-compose stack (real SQL Server + RabbitMQ) asserting the order→event→fulfilment flow,
  ideally with Testcontainers in CI.

Guidelines: test behaviour, not implementation; one assertion theme per test; deterministic
(inject `TimeProvider`, no real clocks/sleeps). Coverage is collected in CI but treated as a
signal, not a target.

### 7. Observability (logging, tracing, metrics)

- **Structured logging** with Serilog. A correlation id is taken from the inbound
  `X-Correlation-Id` (or derived from the trace id), pushed into the log context, echoed on the
  response, and carried onto the bus in the event — so a request can be followed across the
  API, the outbox and the worker.
- **Tracing** with OpenTelemetry: ASP.NET Core and HttpClient instrumentation plus a custom
  `ActivitySource`, exported to an OTLP collector (or the console locally). I'd extend spans
  across the broker boundary by propagating trace context in message headers.
- **Metrics** via OpenTelemetry: runtime + ASP.NET instrumentation and custom counters
  (`orders_created_total`, `order_status_changed_total`). Key SLIs to alert on: request
  latency/error rate, outbox backlog/age, consumer lag and DLQ depth.
- **Health checks**: `/health/live` (process) and `/health/ready` (database) for orchestrator
  probes.

### 8. Performance (indexes, caching, backpressure)

- **Indexes** aligned to access paths: a composite `(CustomerId, Status, CreatedAt)` for the
  main order list, `(Status, CreatedAt)` for status-filtered queries, a unique index on
  customer email, and a **filtered** index on unprocessed outbox rows. See the SQL section for
  covering-index reasoning.
- **Caching**: FX rates are cached per currency pair (read-mostly, tolerant of slight
  staleness). Reference data (the SADC table) is in memory. I'd add output/response caching for
  read-heavy report endpoints with short TTLs and cache-busting on writes.
- **Query hygiene**: `AsNoTracking` for reads, projections to DTOs (no over-fetching), explicit
  `Include` only where needed, keyset pagination available for deep paging.
- **Backpressure**: consumer prefetch limits and idempotency bound in-flight work; at the edge,
  rate limiting and 429s degrade gracefully under overload. The async write path is the primary
  backpressure mechanism — spikes queue rather than overwhelm the database.

### 9. Data retention & compliance (POPIA)

POPIA (South Africa's data-protection act, with analogues across SADC) treats customer name and
email as personal information. Practically:

- **Minimisation & purpose limitation**: store only what's needed (we keep name, email, country)
  and document why.
- **Retention**: define per-entity retention (e.g. orders kept N years for tax/finance, then
  archived or anonymised); enforce with a scheduled job. Partitioning by month (Q8) makes
  archival/purge cheap.
- **Subject rights**: support access/erasure. Because orders must remain for financial records,
  "erasure" usually means pseudonymising the customer (replace PII, keep the transactional shell
  linked by id) rather than hard-deleting.
- **Security & auditing**: encryption in transit and at rest (TDE), least-privilege access, and
  an audit trail of who changed what. Logs should avoid PII; the correlation id is an opaque id,
  not personal data.
- **Lawful processing & cross-border**: record consent/lawful basis; be deliberate about where
  data is hosted given cross-border transfer rules.

### 10. When GraphQL would make sense here

GraphQL earns its keep when clients need to compose **varied, nested reads** and you want to
avoid over-/under-fetching and endpoint sprawl — e.g. a back-office dashboard pulling a
customer with their orders, line items and fulfilment status in one shaped request, with
different screens needing different field sets.

For this system today, REST is the better fit: the operations are a small, well-defined set,
writes have clear command semantics and HTTP caching/idempotency map cleanly to REST. I'd
introduce a **read-only** GraphQL endpoint (as the optional enhancement suggests) over orders
and line items if consumer demand for flexible queries grew, keeping commands on REST. The main
cost to manage is the N+1 problem — solved with data-loader batching and projecting straight to
the query shape — plus query-cost limiting to prevent expensive arbitrary queries.

---

## SQL Section

Schema reference: `Orders(Id, CustomerId, Status, CreatedAt, CurrencyCode, TotalAmount,
RowVersion)`, `OrderLineItems(Id, OrderId, ProductSku, Quantity, UnitPrice)`,
`Customers(Id, Name, Email, CountryCode, CreatedAt)`.

### 1. Pagination query for listing orders

Offset paging (matches the API; simple, supports arbitrary page jumps):

```sql
SELECT  o.Id, o.CustomerId, o.Status, o.CreatedAt, o.CurrencyCode, o.TotalAmount
FROM    Orders AS o
WHERE   (@CustomerId IS NULL OR o.CustomerId = @CustomerId)
  AND   (@Status     IS NULL OR o.Status     = @Status)
ORDER BY o.CreatedAt DESC, o.Id DESC          -- Id is a stable tie-breaker
OFFSET (@Page - 1) * @PageSize ROWS
FETCH NEXT @PageSize ROWS ONLY;
```

`OFFSET` gets expensive on deep pages (the server still scans skipped rows). For large offsets,
**keyset (seek) pagination** is better — pass the last row's sort key:

```sql
SELECT TOP (@PageSize) o.Id, o.CustomerId, o.Status, o.CreatedAt, o.TotalAmount
FROM   Orders AS o
WHERE  (@CustomerId IS NULL OR o.CustomerId = @CustomerId)
  AND  (o.CreatedAt < @LastCreatedAt
        OR (o.CreatedAt = @LastCreatedAt AND o.Id < @LastId))
ORDER BY o.CreatedAt DESC, o.Id DESC;
```

### 2. Top spenders over the last 90 days

```sql
SELECT TOP (20)
       c.Id, c.Name,
       SUM(o.TotalAmount) AS TotalSpent,
       COUNT(*)           AS OrderCount
FROM   Orders   AS o
JOIN   Customers AS c ON c.Id = o.CustomerId
WHERE  o.CreatedAt >= DATEADD(DAY, -90, SYSUTCDATETIME())
  AND  o.Status IN ('Paid', 'Fulfilled')        -- only revenue-bearing orders
GROUP BY c.Id, c.Name
ORDER BY TotalSpent DESC;
```

Note: summing across currencies is only meaningful after FX normalisation (see the ZAR report);
in a multi-currency report I'd convert `TotalAmount` to ZAR before aggregating.

### 3. Index strategy for filtering by CustomerId, Status, CreatedAt

A composite nonclustered index ordered to match the query's equality-then-range pattern:

```sql
CREATE NONCLUSTERED INDEX IX_Orders_CustomerId_Status_CreatedAt
    ON Orders (CustomerId, Status, CreatedAt DESC)
    INCLUDE (CurrencyCode, TotalAmount);
```

Leading columns are the equality predicates (`CustomerId`, `Status`); `CreatedAt` last supports
the `ORDER BY`/range. The `INCLUDE` makes it **covering** for the list query, avoiding key
lookups (Q4). A second index `(Status, CreatedAt)` serves status-only queries (e.g. "all Paid
orders"). I would *not* index `Status` alone — its low cardinality makes it a poor leading
column.

### 4. Execution-plan analysis & removing key lookups

Symptom: the plan shows a **Key Lookup (Clustered)** beside an Index Seek, often with a Nested
Loops join back to the clustered index. That means the nonclustered index found the rows but had
to fetch extra columns from the base table — extra reads per row.

Diagnosis:

```sql
SET STATISTICS IO, TIME ON;
-- run the query; inspect the actual execution plan
```

Fixes:
- **Cover the query** by adding the SELECTed-but-not-indexed columns to `INCLUDE` (as above), so
  the seek alone satisfies the query.
- Reduce `SELECT *` to only needed columns so covering stays cheap.
- If the lookup is selective and rare, it may be acceptable; covering every query bloats writes,
  so cover the hot ones and measure.

### 5. Optimistic concurrency using `rowversion`

`Orders.RowVersion` is a SQL Server `rowversion` (auto-incremented on every update). EF maps it
as a concurrency token, so an update includes the original value in its `WHERE` clause:

```sql
UPDATE Orders
SET    Status = @NewStatus
WHERE  Id = @Id
  AND  RowVersion = @OriginalRowVersion;   -- 0 rows affected => someone else changed it
```

If `@@ROWCOUNT = 0`, the row changed since we read it; EF raises `DbUpdateConcurrencyException`,
which the service turns into HTTP 409 so the client can reload and retry. This prevents lost
updates without holding locks across the user's think-time (pessimistic locking would).

### 6. Deadlock scenario & mitigation

Scenario: two transactions update an order and insert/update related rows in **opposite order**
— T1 locks `Orders` then `OrderLineItems`; T2 locks `OrderLineItems` then `Orders`. Each holds
one lock and waits for the other; SQL Server kills one as the deadlock victim (error 1205).

Mitigations:
- **Consistent lock ordering**: always touch tables/rows in the same order across all code
  paths (e.g. parent before children).
- **Short transactions**: do validation/computation before `BEGIN TRAN`; commit quickly.
- **Right indexes**: deadlocks often come from scans taking range locks; the seekable indexes
  above narrow locking to the rows actually touched.
- **Appropriate isolation**: `READ COMMITTED SNAPSHOT` (RCSI) lets readers use row versions
  instead of taking shared locks, removing many reader/writer deadlocks.
- **Retry on 1205**: deadlocks are transient, so retry the transaction a couple of times with
  small jitter (EF's `EnableRetryOnFailure` is configured for SQL transient errors).

### 7. Window function — running total per customer

```sql
SELECT  o.CustomerId,
        o.Id,
        o.CreatedAt,
        o.TotalAmount,
        SUM(o.TotalAmount) OVER (
            PARTITION BY o.CustomerId
            ORDER BY o.CreatedAt, o.Id
            ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
        ) AS RunningTotal
FROM    Orders AS o
ORDER BY o.CustomerId, o.CreatedAt, o.Id;
```

`PARTITION BY CustomerId` restarts the running sum per customer; the explicit frame makes the
cumulative intent unambiguous.

### 8. Partitioning strategy for large datasets

Partition `Orders` (and align `OrderLineItems`) on a **`CreatedAt` range** by month using a
partition function/scheme:

```sql
CREATE PARTITION FUNCTION pf_OrdersByMonth (datetimeoffset)
    AS RANGE RIGHT FOR VALUES ('2025-01-01', '2025-02-01', '2025-03-01' /* ... */);

CREATE PARTITION SCHEME ps_OrdersByMonth
    AS PARTITION pf_OrdersByMonth ALL TO ([PRIMARY]);
```

Benefits: inserts concentrate on the current partition (better cache/locality), old months can
be archived or dropped via fast **partition switching** instead of large `DELETE`s, and
maintenance/stats can run per partition. A sliding-window job adds next month's boundary and
switches out the oldest. The partitioning column must be part of the clustered key. For purely
hot/cold storage I might instead use a filtered/archival table; partitioning wins when the table
is genuinely huge and time-sliced.

### 9. Outbox pattern — database design

```sql
CREATE TABLE OutboxMessages (
    Id            uniqueidentifier  NOT NULL PRIMARY KEY,
    Type          nvarchar(200)     NOT NULL,   -- logical event name for deserialisation
    Payload       nvarchar(max)     NOT NULL,   -- JSON body
    CorrelationId nvarchar(100)     NULL,        -- ties back to the originating request
    OccurredAt    datetimeoffset    NOT NULL,
    ProcessedAt   datetimeoffset    NULL,        -- NULL = not yet published
    Attempts      int               NOT NULL DEFAULT 0,
    LastError     nvarchar(2000)    NULL
);

-- Filtered index: the dispatcher only scans unprocessed rows, so the index stays tiny.
CREATE NONCLUSTERED INDEX IX_OutboxMessages_Unprocessed
    ON OutboxMessages (ProcessedAt, OccurredAt)
    WHERE ProcessedAt IS NULL;
```

The producer inserts the row in the same transaction as the business change. A dispatcher claims
a batch oldest-first, publishes with confirms, and stamps `ProcessedAt`. Multiple dispatchers
can run safely by claiming rows with `UPDLOCK, READPAST`:

```sql
UPDATE TOP (50) OutboxMessages WITH (UPDLOCK, READPAST)
SET    Attempts = Attempts + 1
OUTPUT inserted.Id, inserted.Type, inserted.Payload, inserted.CorrelationId
WHERE  ProcessedAt IS NULL AND Attempts < 10;
```

Processed rows are pruned by a retention job. `Attempts`/`LastError` give poison-message
visibility.

### 10. Stored procedure — transaction report

Daily order totals by currency and status over a date range:

```sql
CREATE OR ALTER PROCEDURE dbo.usp_OrderTransactionReport
    @FromUtc datetimeoffset,
    @ToUtc   datetimeoffset
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        CAST(o.CreatedAt AS date) AS OrderDate,
        o.CurrencyCode,
        o.Status,
        COUNT(*)            AS OrderCount,
        SUM(o.TotalAmount)  AS TotalAmount,
        AVG(o.TotalAmount)  AS AverageOrderValue
    FROM   Orders AS o
    WHERE  o.CreatedAt >= @FromUtc
      AND  o.CreatedAt <  @ToUtc
    GROUP BY CAST(o.CreatedAt AS date), o.CurrencyCode, o.Status
    ORDER BY OrderDate, o.CurrencyCode, o.Status;
END;
```

Parameterised (no injection, plan reuse), bounded by an explicit half-open date range so it is
index-friendly against `(CreatedAt)`/the composite index.

---

## Part 3 — Advanced enhancements implemented

I implemented four of the optional enhancements:

1. **Messaging reliability — Outbox pattern.** Events are written transactionally and published
   by a polling dispatcher with confirms and retry; the consumer dead-letters poison messages.
   (`OutboxMessage`, `OutboxDispatcher`, `RabbitMqEventPublisher`.)
2. **FX conversion.** `GET /api/reports/orders/zar` converts order totals to ZAR via a mocked,
   cached `IFxRateProvider`; CMA currencies convert at par; amounts are rounded per currency
   minor unit. (`MockFxRateProvider`, `ZarReportService`.)
3. **Observability.** Serilog structured logging with correlation ids, OpenTelemetry traces and
   metrics (incl. custom counters), and liveness/readiness health checks.
4. **DevOps.** Docker Compose for the whole stack, a GitHub Actions CI pipeline (build, test,
   front-end build, and an EF "pending model changes" check), and environment bootstrapping via
   configuration + auto-migrate/seed in development.

### Migration lifecycle, zero-downtime & rollback

- **Migration strategy.** Code-first, one migration per schema change, reviewed in PRs. CI runs
  `dotnet ef migrations has-pending-model-changes` so a model edit without a matching migration
  fails the build, keeping code and schema in lockstep. Migrations are applied as a deploy step
  (script or `database update`), not silently at app start in production.
- **Zero-downtime.** Use **expand/contract**: additive, backward-compatible changes first
  (new nullable columns/tables, new indexes built `ONLINE`), deploy code that writes both
  old/new, backfill, then a later release removes the old shape. New columns are added nullable
  or with defaults so old and new app versions coexist during a rolling deploy.
- **Rollback.** Each migration has a `Down`. For genuinely destructive changes I avoid relying
  on `Down` in production (it can lose data) and prefer a forward-fix migration plus a database
  backup/restore point taken before deploy. Generating an idempotent script
  (`docs/migrations.sql`) lets a DBA review and apply changes deliberately.
