# dotnet-project-architecture reference 2

## 5. Infrastructure Layer

Infrastructure implements technical capabilities required by the application.

Examples:

- Database access.
- External APIs.
- Message brokers.
- FIX protocol integration.
- File storage.
- Email services.
- Caching.

Infrastructure implementations should satisfy contracts defined by the application or domain when dependency inversion is appropriate.

### Infrastructure Rules

- Keep external library types inside infrastructure boundaries.
- Centralize service registration.
- Externalize environment-specific configuration.
- Handle technical failures consistently.
- Use appropriate logging.
- Manage connection and resource lifecycles.
- Avoid embedding business policies in infrastructure adapters.

Infrastructure must not become the owner of business rules simply because it processes external messages.

---

## 6. Presentation Layer

The presentation layer handles external interaction.

For ASP.NET Core applications, it may contain:

- Controllers.
- Minimal API endpoints.
- Razor Pages.
- Request models.
- Response models.
- Filters and middleware.

Presentation responsibilities include:

- Receiving requests.
- Basic request validation.
- Mapping requests to application inputs.
- Invoking use cases.
- Translating application results into HTTP responses.
- Handling presentation-specific errors.

Do not implement domain calculations or financial invariants in controllers or Razor Pages.

### Request Validation

Validate external input at the application boundary.

Use appropriate validation mechanisms for:

- Required fields.
- String lengths.
- Numeric ranges.
- Supported enumerations.
- Formatting.
- Cross-field constraints.

Domain invariants must remain protected even when requests bypass the web interface.

---

## 7. Dependency Injection and Composition

Use built-in .NET dependency injection unless a concrete requirement justifies another container.

### Composition Root

Centralize dependency registration at application startup or in dedicated registration extensions.

Example:

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

Keep composition code separate from business logic.

### Service Lifetimes

Choose service lifetimes based on actual state and resource ownership.

| Lifetime | Typical use |
|---|---|
| Singleton | Stateless shared services or intentionally shared state |
| Scoped | Request-level services and database contexts |
| Transient | Lightweight stateless services |

Verify that:

- Singletons do not capture scoped dependencies.
- Shared state is thread-safe.
- Database contexts are not registered as singletons.
- Disposable resources have clear ownership.
- FIX sessions are initialized and disposed consistently.

Do not select service lifetimes arbitrarily.

---

## 8. Configuration Management

Use the .NET configuration system.

Typical sources:

- `appsettings.json`.
- `appsettings.Development.json`.
- Environment variables.
- User Secrets for local development.
- External secret providers for production.

Use strongly typed options for structured configuration.

Validate mandatory configuration during startup whenever possible.

Never commit:

- Passwords.
- API keys.
- Private certificates.
- Production connection strings.
- Sensitive FIX credentials.

Configuration should describe environment-specific behavior, not contain business logic.

---

## 9. Financial Domain Architecture

For financial applications, architectural correctness must preserve monetary integrity.

### Monetary Representation

Use `decimal` for financial values.

Never use `double` or `float` for monetary calculations.

Represent money with explicit currency semantics when multiple currencies are relevant.

### Financial Invariants

Protect:

- Valid monetary precision.
- Positive or otherwise permitted amounts.
- Quantity boundaries.
- Exposure limits.
- Correct aggregation.
- Atomic financial updates.
- Idempotent processing.

### Exposure Management

For the challenge exposure model:

`Exposure = Buy Notional - Sell Notional`

Where:

`Notional = Price × OrderQty` for each accepted order.

Acceptance atomically adds the full signed notional to the symbol's exposure. This accounting simplification is not a market execution; the challenge does not implement market execution. An accepted FIX report remains `ExecType = 0`, `OrdStatus = 0`, `CumQty = 0`, `LeavesQty = OrderQty`.

The absolute per-symbol limit is the domain constant `100_000_000.00m`, not external configuration. Validate price numerically using `Price > 0`, `Price < 1000` and `Price % 0.01m == 0`; `1.100` is valid.

Do not confuse:

- Order submission.
- Order acceptance.
- Order execution.
- Partial execution.
- Order cancellation.
- Order rejection.

### Concurrency

Financial validation and state mutation must be atomic.

Avoid check-then-update operations without synchronization or transactional protection.

For this challenge, use one global lock and publish a fully prepared copy-on-write snapshot containing exposure and original results in one state reference assignment. Never mutate the published snapshot. Pre-commit failure leaves it unchanged; after publication a lost response is unknown and an identical retry reproduces the committed result. Different symbols are serialized and copying costs time and memory.

### Idempotency

Repeated processing of the same logical operation must not apply financial changes more than once.

Use globally keyed `ClOrdID` and thread-safe duplicate detection. Preserve original accepted and business-rejected results: identical content reproduces the original outcome without a second financial effect; conflicting content is rejected without overwriting it. Coordinate the identifier across symbols, including invalid symbols, atomically with the acceptance decision, exposure update and result registration. Concurrent submissions have one decision and subsequent identical results or conflicts.

Document how idempotency behaves across restarts and multiple application instances.

---

## 10. External Integration Architecture

External integrations should be isolated behind adapters.

Examples include:

- REST APIs.
- FIX Protocol.
- Message brokers.
- Databases.
- Third-party SDKs.

### Adapter Responsibilities

Adapters should:

- Translate external models into internal contracts.
- Translate application results into external representations.
- Handle protocol-specific behavior.
- Manage connection lifecycles.
- Isolate library dependencies.
- Translate technical failures appropriately.

Do not allow external protocol models to become domain entities.

### FIX Protocol Example

For a solution using QuickFIX/n:

- Keep QuickFIX/n types in the FIX integration boundary.
- Translate NewOrderSingle messages into application requests.
- Delegate financial validation to application/domain services.
- Translate application outcomes into ExecutionReports.
- Keep session management separate from financial processing.

The application layer should not depend directly on FIX session objects.

---

