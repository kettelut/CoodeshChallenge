# Testing Strategy

## 1. Purpose

Define a consistent, risk-oriented testing strategy for the Coodesh OrderProcessing solution.

The strategy must ensure that:

- Financial calculations are accurate and deterministic.
- Exposure limits cannot be bypassed through concurrent requests.
- Duplicate orders cannot affect exposure more than once.
- FIX Protocol 4.4 messages are correctly constructed, received, processed and correlated.
- OrderGenerator and OrderAccumulator communicate reliably.
- Invalid orders are rejected without changing exposure, while their business-rejected results are retained for idempotency.
- Application behavior remains consistent across success, rejection, timeout and failure scenarios.
- Architecture boundaries and business rules remain protected against regressions.

Testing must prioritize business correctness, financial integrity and integration reliability over arbitrary code coverage percentages.

---

## 2. Project Context

The solution contains two .NET 10 applications:

- **OrderGenerator:** ASP.NET Core web application responsible for receiving order submissions and acting as a FIX Initiator.
- **OrderAccumulator:** FIX Acceptor responsible for receiving orders, validating them, maintaining financial exposure and sending ExecutionReports.

The challenge supports the following symbols:

- PETR4
- VALE3
- VIIA4

Order properties:

- Side: Buy or Sell.
- Quantity: positive integer from 1 to 99,999.
- Price: positive decimal, lower than 1,000 and numerically divisible by `0.01m`.

Exposure calculation:

    Exposure = Total Buy Notional - Total Sell Notional

    Order Notional = Price * OrderQty

The absolute exposure limit is the per-symbol domain constant `100_000_000.00m`, not external configuration.

An order that passes validation receives an ExecutionReport with `ExecType=New`. An order that fails business validation receives an ExecutionReport with `ExecType=Rejected`.

Acceptance atomically adds the full signed notional to exposure. This is a challenge-specific accounting simplification, not a market execution; the challenge does not implement market execution. Tests must assert the accepted report has `ExecType=0`, `OrdStatus=0`, `CumQty=0` and `LeavesQty=OrderQty`.

---

## 3. Testing Principles

Apply the following principles:

- Test observable behavior rather than implementation details.
- Prioritize business-critical paths and failure scenarios.
- Keep unit tests isolated from infrastructure and external systems.
- Use integration tests to validate real component boundaries.
- Use end-to-end tests to validate complete workflows.
- Ensure tests are deterministic and repeatable.
- Avoid dependencies on execution order, wall-clock timing or external network availability.
- Use explicit test data and meaningful assertions.
- Keep financial calculations independent from floating-point arithmetic.
- Test concurrency and idempotency as first-class requirements.
- Do not mock the behavior that the test is intended to validate.
- Prefer small, focused tests with a single reason to fail.
- Do not create tests solely to increase coverage metrics.
- Never claim a test passed without actually executing it.

---

## 4. Recommended Testing Stack

Use the following tools where appropriate:

| Concern | Recommended technology |
|---|---|
| Test framework | xUnit |
| Assertions | FluentAssertions or native xUnit assertions |
| Mocking | NSubstitute or Moq |
| Web integration | Microsoft.AspNetCore.Mvc.Testing |
| FIX integration | QuickFIX/n |
| Code coverage | Coverlet |
| Test execution | `dotnet test` |
| Static analysis | .NET analyzers and SonarQube-compatible rules |

Use the libraries already present in the repository whenever possible. Do not introduce additional dependencies without a concrete testing requirement.

---

## 5. Test Project Organization

Prefer test projects aligned with application boundaries.

Recommended structure:

    tests/
    ├── OrderProcessing.Domain.Tests/
    ├── OrderProcessing.Application.Tests/
    ├── OrderAccumulator.Tests/
    ├── OrderGenerator.Tests/
    └── OrderProcessing.IntegrationTests/

Adapt this structure to the actual solution architecture. Do not create empty or redundant projects.

### Domain tests

Validate:

- Order invariants.
- Financial calculations.
- Exposure updates.
- Exposure boundaries.
- Symbol isolation.
- Idempotency.
- Rejection behavior.

### Application tests

Validate:

- Order submission use cases.
- Application service orchestration.
- Validation flow.
- Dependency interactions.
- Error propagation.
- Cancellation and timeout handling.

### OrderAccumulator tests

Validate:

- FIX message processing.
- Business validation integration.
- ExecutionReport generation.
- Session-related behavior.
- Message duplication handling.
- Exposure consistency.

### OrderGenerator tests

Validate:

- HTTP endpoints or Razor Pages.
- Server-side validation.
- Order submission orchestration.
- FIX response correlation.
- User-facing status representation.

### Integration tests

Validate:

- Communication between the FIX Initiator and Acceptor.
- Actual QuickFIX/n message serialization and parsing.
- Complete order processing workflows.
- Application startup and dependency injection.
- Session lifecycle and failure handling.

---

## 6. Unit Testing Strategy

Unit tests must validate business rules without requiring:

- A running FIX session.
- A database.
- An HTTP server.
- External services.
- Real network communication.

Use deterministic inputs and outputs.

### 6.1 Order validation

Test all supported symbols:

- PETR4 is accepted.
- VALE3 is accepted.
- VIIA4 is accepted.
- Unsupported symbols are rejected.
- Null, empty or whitespace-only symbols are rejected.

Test order sides:

- Buy is accepted.
- Sell is accepted.
- Undefined or unsupported values are rejected.

Test quantities:

| Input | Expected result |
|---|---|
| 1 | Accepted |
| 99,999 | Accepted |
| 0 | Rejected |
| -1 | Rejected |
| 100,000 | Rejected |

Test prices:

| Input | Expected result |
|---|---|
| 0.01 | Accepted |
| 1.10 | Accepted |
| 1.100 | Accepted |
| 1.1000 | Accepted |
| 10.00 | Accepted |
| 999.99 | Accepted |
| 0 | Rejected |
| Negative value | Rejected |
| 1,000.00 | Rejected |
| 10.001 | Rejected |
| 10.005 | Rejected |
| 1.101 | Rejected |
| 1.105 | Rejected |
| 0.001 | Rejected |

Validate the numeric value with `Price % 0.01m == 0`, not the textual number of decimal places. Ensure validation is performed server-side, independently of frontend validation.

### 6.2 Financial calculations

Use `decimal` exclusively for monetary calculations.

Test:

- Buy notional calculation.
- Sell notional calculation.
- Multiple orders for the same symbol.
- Mixed Buy and Sell orders.
- Positive exposure.
- Negative exposure.
- Zero exposure.
- Decimal precision.
- Large valid quantities and prices.

Example:

    Buy 100 PETR4 at R$10.00 = R$1,000.00
    Sell 40 PETR4 at R$8.00 = R$320.00

    Expected exposure = R$680.00

Another example:

    Buy 100 VALE3 at R$20.00 = R$2,000.00
    Sell 150 VALE3 at R$10.00 = R$1,500.00

    Expected exposure = R$500.00

Verify that calculations do not introduce binary floating-point precision errors.

### 6.3 Exposure limit

The limit is absolute exposure per symbol:

    Maximum absolute exposure = 100,000,000.00

Test:

| Projected exposure | Expected result |
|---|---|
| R$99,999,999.99 | Accepted |
| R$100,000,000.00 | Accepted |
| R$100,000,000.01 | Rejected |
| -R$99,999,999.99 | Accepted |
| -R$100,000,000.00 | Accepted |
| -R$100,000,000.01 | Rejected |

Test transitions across the limit:

- An order that moves exposure below the limit is accepted.
- An order that reaches the exact boundary is accepted.
- An order that exceeds the boundary is rejected.
- A Sell order can reduce positive exposure.
- A Buy order can reduce negative exposure.
- An order that crosses the opposite boundary is rejected.
- Rejected orders do not modify exposure.

Always validate the projected exposure:

    ProjectedExposure = CurrentExposure + SignedOrderNotional

    Buy:  SignedOrderNotional = OrderNotional
    Sell: SignedOrderNotional = -OrderNotional

    Accept when:
    Abs(ProjectedExposure) <= MaximumExposure

The test suite must verify that the limit is applied independently to each symbol.

### 6.4 Symbol isolation

Verify that:

- PETR4 exposure does not affect VALE3.
- VALE3 exposure does not affect VIIA4.
- Each symbol has an independent limit.
- A rejected order for one symbol does not affect another symbol.
- Concurrent orders for different symbols do not interfere with each other.

### 6.5 Idempotency

Use `ClOrdID (11)` as the primary order correlation and idempotency identifier.

Test:

- A new ClOrdID is processed once.
- A duplicate ClOrdID does not update exposure again.
- Retransmitted messages do not produce duplicate financial effects.
- Concurrent duplicate submissions are processed only once.
- Reusing a ClOrdID with identical content returns or reproduces the original accepted or rejected result.
- Reusing a ClOrdID with different content, including another symbol, rejects the conflict without overwriting the original or changing exposure.
- Concurrent identical and conflicting submissions have one decision, no second financial effect, and consistent original results or conflicts.
- Rejected business results are retained even for invalid symbols and replayed identically.
- Numerically equal prices such as `1.10` and `1.100` compare as the same business value; FIX transport metadata does not create a content conflict.
- An originally rejected order remains rejected on identical replay even if exposure later changes.

Idempotency must be validated under concurrent execution, not only sequential requests.

### 6.6 Rejection integrity

For every business rejection, verify:

- Exposure remains unchanged.
- The order is not recorded as accepted.
- No successful financial state transition occurs.
- The rejection reason is available.
- A correct rejected response can be generated.
- Reprocessing the same message does not create additional effects.

---

