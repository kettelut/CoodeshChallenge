# GitHub Copilot Repository Instructions

## Project Context

This repository implements two independent .NET 10 applications:

1. `OrderGenerator`: ASP.NET Core web application and FIX 4.4 initiator.
2. `OrderAccumulator`: FIX 4.4 acceptor that validates orders and manages exposure.

The solution uses QuickFIX/n and C#. Implementations must remain focused on this assignment and must not introduce a trading engine, database or distributed infrastructure.

## Core Architecture

- Prefer a pragmatic layered architecture: web/API or host, application orchestration, domain rules and FIX infrastructure.
- Keep QuickFIX/n types out of the domain layer.
- Keep FIX-neutral order and exposure contracts independent of FIX message classes.
- Use ASP.NET Core dependency injection and `IOptions<T>` for technical configuration, not for the fixed exposure limit.
- Use Microsoft logging abstractions and structured log messages.
- Keep `OrderGenerator` and `OrderAccumulator` independently runnable.
- Share domain code only when it genuinely reduces duplication. Do not create unnecessary abstractions.

## Technology Rules

- Target `net10.0` for application and test projects.
- Use C# and modern .NET APIs.
- Use ASP.NET Core Razor Pages or MVC unless an explicit change request requires a SPA.
- Use QuickFIX/n for FIX 4.4 communication.
- Use xUnit or the selected repository test framework consistently.
- Use `decimal` for quantity notional, price and exposure.
- Never use `float` or `double` for financial values.
- Use invariant culture parsing for fixed-precision decimal values.
- Do not add Entity Framework, SQL databases, Redis, Kafka, RabbitMQ, Kubernetes or other infrastructure unless the user explicitly changes the requirements.

## FIX Protocol Rules

- Protocol version must be FIX 4.4.
- `NewOrderSingle`:
  - Tag 35 `D`
  - Tag 11 `ClOrdID`
  - Tag 55 `Symbol`
  - Tag 54 `Side`
  - Tag 38 `OrderQty`
  - Tag 44 `Price`
  - Tag 40 `OrdType=2`
  - Tag 60 `TransactTime`
- `ExecutionReport`:
  - Tag 37 `OrderID`
  - Tag 17 `ExecID`
  - Tag 150 `ExecType`
  - Tag 39 `OrdStatus`
  - Tag 55 `Symbol`
  - Tag 54 `Side`
  - Tag 38 `OrderQty`
  - Tag 44 `Price`
  - Tag 60 `TransactTime`
- Accepted orders use:
  - `ExecType=0` New
  - `OrdStatus=0` New
  - `CumQty=0` and `LeavesQty=OrderQty`
- Rejected orders use:
  - `ExecType=8` Rejected
  - `OrdStatus=8` Rejected
  - `OrdRejReason=103` with an appropriate FIX reason
  - `Text=58` with a clear human-readable explanation
- Use the FIX 4.4 typed QuickFIX/n message classes and data dictionary.
- Send replies with `Session.SendToTarget(report, sessionID)`.
- Generate a unique `ClOrdID` for every original web submission.
- Generate unique `OrderID` and `ExecID` values.
- Treat acceptance as an assignment-model update, not as a real market execution.

## Validation Rules

### Symbols

Only these symbols are valid:

- `PETR4`
- `VALE3`
- `VIIA4`

### Side

- Buy: FIX 4.4 side value `1`
- Sell: FIX 4.4 side value `2`

### Quantity

- Positive integer
- Strictly less than `100,000`
- Reject `0`, negative values and `100,000` or greater

### Price

- Positive decimal
- Multiple of `0.01`
- Strictly less than `1,000`
- Validate numerically with `price % 0.01m == 0`, not by counting textual decimal places: `1.10`, `1.100` and `1.1000` are valid; `1.101` and `1.105` are not.
- Reject `0`, negative values, nonmultiples of `0.01m` and `1,000` or greater.

Repeat all business validation in `OrderAccumulator`. The UI or API must never be the only validation boundary.

## Financial Exposure Rules

- Maintain independent exposure by symbol.
- Buy increases exposure.
- Sell decreases exposure.
- `exposure = buy notional - sell notional`
- For this challenge, each accepted order immediately contributes its full `OrderQty * Price` notional, signed positive for Buy and negative for Sell. This is accounting only, not an actual market execution.
- The inclusive per-symbol exposure limit is the fixed domain constant `100_000_000m`, not external configuration.
- Accept when `Abs(candidateExposure) <= 100_000_000m`.
- Reject when `Abs(candidateExposure) > 100_000_000m`.
- Rejected orders must never update exposure.
- Accepted orders must update exposure exactly once.
- Coordinate global `ClOrdID` identity, original result registration and per-symbol exposure validation/update in one atomic operation, including concurrent requests for different symbols. A per-symbol lock alone is insufficient for identity.
- Use one global in-memory lock and an unpublished copy-on-write snapshot containing exposure and original order results. Prepare all fallible work first and publish the complete snapshot with one state reference assignment; never mutate the published snapshot. Pre-commit failure leaves all state unchanged. A failure after publication may leave the caller with an unknown outcome; an identical retry returns the committed result.
- The global lock serializes different symbols and snapshot copying costs memory and time. Do not hold it during network or unrelated I/O. Restart loses in-memory state.
- Store order content and original results in memory: an identical `ClOrdID` and content returns or reproduces the original accepted or rejected result without another financial effect; the same identifier with different content is rejected as a conflict without changing original state.
- Compare order prices by numeric `decimal` value for idempotency: `1.10` and `1.100` are equal. Do not treat FIX transport metadata as changed order content.
- Define the order content used for comparison consistently across processing and retries; compare decimal prices numerically, so `1.10` and `1.1000` alone are not a conflict.
- Concurrent duplicates must have at most one financial transition; other submissions observe the original result or a conflict according to their content.
- Do not maintain separate exposure caches that can diverge.

This challenge uses in-memory state. Exposure and duplicate history may be lost when `OrderAccumulator` restarts. QuickFIX/n session sequence persistence is separate and should use local runtime directories.

## Correlation and Web Requests

- Generate `ClOrdID` in `OrderSubmissionService`, not in the browser.
- Track pending submissions in a thread-safe in-memory registry.
- Correlate browser requests to reports using `ClOrdID`.
- Remove pending requests after completion or timeout.
- A timeout must not be represented as accepted or rejected.
- Session disconnect must be represented separately from an ExecutionReport response.
- Concurrent submissions must not share or reuse a pending request.

## Configuration

- Use `appsettings.json` and environment variables.
- Do not hard-code ports, FIX identifiers, heartbeat values, timeout values or exposed credentials.
- Isolated local development may use HTTP loopback with non-sensitive test data. Network-accessible environments and production require HTTPS; never transmit credentials or sensitive information over HTTP.
- Add launch profiles for local execution.
- Use separate FIX session settings for initiator and acceptor.
- Use runtime FIX store/log paths outside source control.
- Do not commit secrets or local environment files.

## Logging and Error Handling

- Use structured logging for session, order, validation and timeout events.
- Avoid logging secrets or full sensitive payloads.
- Log the FIX session ID and `ClOrdID` when useful.
- Never convert communication failures into false accepted or rejected outcomes.
- A protocol-valid business-invalid order receives a rejected ExecutionReport. Malformed or protocol-incompatible FIX messages use the applicable Session Reject or Business Message Reject mechanism; do not generate an order ExecutionReport indiscriminately for protocol errors.
- Implement explicit handling for logon, logout, disconnect, timeout and reconnect.
- Stop QuickFIX/n engines cleanly during application shutdown.

## Testing Requirements

When implementing or changing behavior, add or update tests for:

### Unit Tests

- Valid and invalid symbols
- Buy and Sell sides
- Quantity `1`, `99,999`, `0`, negative values and `100,000`
- Price `0.01`, `999.99`, `0.001`, `1.1`, negative values and `1,000`
- Numeric price multiples: accept `1.100` and `1.1000`, reject `1.101` and `1.105`.
- Positive and negative exposure
- Exposure exactly at the limit
- Exposure above or below the limit
- Accepted and rejected orders
- Identical `ClOrdID` and content return the original result, including after rejection; differing content is rejected as a conflict.
- Decimal precision

### Concurrency Tests

- Simultaneous Buy orders
- Simultaneous Sell orders
- Mixed simultaneous orders
- Orders for the same symbol
- Orders for different symbols
- Individually valid orders whose collective notional exceeds the limit
- Concurrent identical and conflicting deliveries of the same `ClOrdID`, including different symbols, with one financial transition at most
- Failures at the identity/result/exposure commit boundary do not leave partial state
- Inject failures before exposure preparation, during processing and before result registration/publication; verify the entire published state and retry behavior. Test a lost response after publication and replay of the committed result.
- Verification that exposure never exceeds the absolute limit

### FIX Integration Tests

- Initiator/acceptor connectivity
- NewOrderSingle transmission
- ExecutionReport acceptance with `CumQty=0` and `LeavesQty=OrderQty`, despite immediate exposure accounting
- ExecutionReport rejection
- Correct `ClOrdID` correlation
- Disconnection and reconnection
- Invalid FIX messages
- Duplicate FIX messages
- Sequence store behavior

### End-to-End Tests

- Submit a valid order through the web form
- Verify FIX transmission
- Verify the resulting report
- Verify frontend accepted/rejected feedback
- Verify exposure changes only for accepted orders

Example limit checks:

- Existing exposure `99,999,000`; Buy `100 @ 10.00` is accepted at exactly `100,000,000`.
- Existing exposure `99,999,000`; Buy `101 @ 10.00` is rejected at `100,000,010`.
- Existing exposure `-99,999,000`; Sell `101 @ 10.00` is rejected at `-100,000,010`.

## Project and Documentation Standards

- Keep `.github/copilot-instructions.md` accurate when repository conventions change.
- Keep `README.md` accurate after structural or setup changes.
- Update example FIX configurations with implementation changes.
- Do not commit `bin/`, `obj/`, `.vs/`, `.vscode/` user settings, `TestResults/`, FIX runtime stores, logs or local secret configuration.
- Use English for documentation and comments unless an existing repository convention explicitly requires another language.
- Use the existing `.gitignore` and add only necessary ignore rules.

## Definition of Done

Before considering a change complete:

- The solution builds on .NET 10.
- Relevant unit and integration tests pass.
- Accepted/rejected behavior is covered.
- Concurrent exposure cannot bypass the limit.
- Duplicate `ClOrdID` values do not alter exposure twice.
- FIX tags and enumerations are correct.
- Rejection reports include a reason and text.
- Session and request failure states are handled.
- README setup and run instructions match the actual projects.
- No database or broker infrastructure was introduced by default.
