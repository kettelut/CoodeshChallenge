# execute-project-phase reference 2

## 8. Critical Financial Requirements

Financial correctness is mandatory throughout implementation.

The challenge supports:

- `PETR4`
- `VALE3`
- `VIIA4`

Order constraints:

- Quantity must be an integer greater than zero and less than 100,000.
- Price must be greater than zero and less than 1,000.
- Price must be a multiple of 0.01.

Exposure formula:

`Exposure = Sum(accepted Buy Price * OrderQty) - Sum(accepted Sell Price * OrderQty)`

Maximum absolute exposure per symbol:

`100_000_000.00m`, a domain constant, not external configuration.

Validate `Price % 0.01m == 0` numerically: `1.100` is valid and `1.101` is invalid.

Mandatory requirements:

- Use `decimal` for all monetary calculations.
- Never use `double` or `float` for financial values.
- Enforce exposure independently per symbol.
- Validate projected exposure before modifying state.
- Make validation and update atomic.
- Prevent concurrent orders from bypassing the limit.
- Ensure rejected orders do not affect exposure.
- Prevent duplicate financial processing.
- Use `ClOrdID` as the primary order identifier.
- Preserve original accepted and business-rejected results: identical content reproduces the original result, while conflicting content is rejected without overwriting it or changing exposure.
- Coordinate identifiers across their scope, including different or invalid symbols, atomically with result registration and exposure updates; concurrent duplicates have one decision and at most one financial transition.

Boundary conditions:

- `100000000.00m` is valid.
- `100000000.01m` is invalid.
- `-100000000.00m` is valid.
- `-100000000.01m` is invalid.

Do not weaken financial rules to make implementation or tests pass.

### Acceptance and Execution Semantics

Acceptance atomically applies the full signed `Price * OrderQty` to exposure. This challenge-specific accounting simplification is not a market execution, and the challenge does not implement one. The accepted report remains `ExecType = 0`, `OrdStatus = 0`, `CumQty = 0`, `LeavesQty = OrderQty`.

## 9. Critical FIX Requirements

Use QuickFIX/n and FIX Protocol 4.4.

Application roles:

- OrderGenerator: Initiator.
- OrderAccumulator: Acceptor.

NewOrderSingle required fields:

| Field | Tag |
|---|---:|
| ClOrdID | 11 |
| Symbol | 55 |
| Side | 54 |
| OrderQty | 38 |
| OrdType | 40 |
| Price | 44 |
| TransactTime | 60 |

Expected values:

- Buy: Side `1`.
- Sell: Side `2`.
- Limit: OrdType `2`.

ExecutionReport fields:

| Field | Tag |
|---|---:|
| OrderID | 37 |
| ExecID | 17 |
| ClOrdID | 11 |
| Symbol | 55 |
| Side | 54 |
| ExecType | 150 |
| OrdStatus | 39 |
| OrderQty | 38 |
| Price | 44 |
| CumQty | 14 |
| LeavesQty | 151 |
| AvgPx | 6 |
| TransactTime | 60 |
| OrdRejReason | 103 |
| Text | 58 |

Accepted order:

- `ExecType = 0` (New).
- `OrdStatus = 0` (New).
- `CumQty = 0`.
- `LeavesQty = OrderQty`.

Rejected order:

- `ExecType = 8` (Rejected).
- `OrdStatus = 8` (Rejected).

Additional requirements:

- Correlate responses using `ClOrdID`.
- Support asynchronous responses.
- Handle out-of-order responses.
- Handle session disconnections.
- Handle duplicate messages.
- Handle request timeouts.
- Avoid duplicate financial effects.
- Do not automatically resubmit an order with an unknown outcome.
- Keep FIX handlers thin.
- Keep financial business logic outside QuickFIX/n callbacks.

## 10. Testing Requirements

Testing must be part of implementation, not postponed until final delivery.

Require tests for:

### Domain

- Supported symbols.
- Valid and invalid sides.
- Quantity boundaries.
- Price boundaries.
- Decimal precision.
- Exposure calculation.
- Buy and sell behavior.
- Per-symbol isolation.
- Exposure boundaries.
- Rejected order state preservation.

### Concurrency

- Simultaneous orders for the same symbol.
- Concurrent exposure updates.
- Concurrent duplicate identifiers.
- Final exposure consistency.
- Independent symbol processing.

### Idempotency

- Duplicate `ClOrdID`.
- Concurrent duplicate submissions.
- FIX retransmissions.
- Duplicate ExecutionReports.
- Original processing result preservation.
- Identical accepted and rejected results replayed; conflicting content rejected, including concurrent cross-symbol reuse.

### FIX

- NewOrderSingle mapping.
- ExecutionReport mapping.
- Accepted and rejected responses.
- Session startup and shutdown.
- Logon and Logout.
- Disconnect and reconnection.
- Message correlation.
- Timeout behavior.

### HTTP

- Form validation.
- Server-side validation.
- Successful submission.
- Rejection presentation.
- Pending status.
- Unknown outcome.
- Error handling.

Use deterministic tests.

Avoid arbitrary delays and timing-dependent assertions.

Never fabricate test results.

## 11. Security Requirements

Ensure implementation follows the security instructions.

Mandatory considerations:

- Validate all external input.
- Protect financial state.
- Avoid hardcoded secrets.
- Avoid logging credentials.
- Restrict FIX session access.
- Validate FIX session identifiers.
- Require HTTPS for network-accessible environments and production; isolated local development may use HTTP loopback with non-sensitive test data. Never transmit credentials or sensitive information over HTTP.
- Protect browser-based state-changing operations against CSRF.
- Restrict CORS where applicable.
- Avoid exposing stack traces.
- Review NuGet dependencies.
- Prevent resource exhaustion.
- Avoid unsafe deserialization.
- Do not disable TLS certificate validation.

Document any security limitations caused by the challenge scope.

## 12. Architecture and Code Quality

Ensure implementation respects:

- Separation of concerns.
- Dependency inversion.
- Domain independence from infrastructure.
- Explicit application boundaries.
- Dependency injection.
- Appropriate asynchronous programming.
- Nullable reference types.
- Structured logging.
- Meaningful error handling.
- Focused classes and methods.
- Consistent naming.
- Minimal dependencies.

Avoid:

- Business logic inside controllers or Razor Pages.
- Financial calculations inside FIX callbacks.
- Direct domain dependencies on QuickFIX/n.
- Unnecessary abstractions.
- Premature optimization.
- Unnecessary design patterns.
- Unrelated refactoring.
- Duplicated business rules.

Do not compromise maintainability for artificial code reduction.

