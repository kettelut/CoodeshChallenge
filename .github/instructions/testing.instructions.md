---
applyTo: "**/*Tests/**/*.cs,**/*Test/**/*.cs,**/*.Tests.csproj,**/*.Test.csproj,**/testsettings.json,**/*.runsettings"
---

# Testing Instructions

## 1. Testing Objectives

The OrderProcessing solution must have automated tests that demonstrate correctness, reliability and compliance with the Coodesh challenge requirements.

Testing must prioritize:

- Financial calculation correctness.
- Order validation.
- Exposure limit enforcement.
- Atomicity under concurrent requests.
- Idempotent order processing.
- Correct order acceptance and rejection.
- FIX Protocol 4.4 message handling.
- HTTP order submission.
- Application integration.
- Error handling and resilience.

Tests must provide evidence of actual behavior. Never assume that code is correct because it compiles.

## 2. Testing Strategy

Use a pragmatic testing pyramid:

1. **Unit tests:** Validate isolated domain rules and application behavior.
2. **Integration tests:** Validate interactions between application components and infrastructure.
3. **End-to-end tests:** Validate the complete order submission and response flow.
4. **Regression tests:** Protect previously implemented behavior against unintended changes.

Prefer fast, deterministic unit tests for financial business rules.

Use integration and end-to-end tests for behavior that cannot be reliably validated in isolation.

Do not introduce unnecessary testing infrastructure or external dependencies.

## 3. Testing Technologies

Prefer:

- xUnit for test organization and execution.
- FluentAssertions when already approved or available in the project.
- Moq or NSubstitute for dependency mocking, when appropriate.
- Microsoft.AspNetCore.Mvc.Testing for ASP.NET Core integration tests.
- QuickFIX/n components for FIX integration tests.
- Microsoft.Extensions.DependencyInjection for dependency injection validation.

Use the existing test framework if the repository already establishes one.

Do not add multiple libraries that provide overlapping functionality without justification.

## 4. Test Project Organization

Keep tests organized by application and responsibility.

Recommended structure:

    tests/
    ├── OrderProcessing.Domain.Tests/
    │   ├── Orders/
    │   ├── Exposure/
    │   └── Validation/
    ├── OrderProcessing.Application.Tests/
    │   ├── Orders/
    │   └── Processing/
    ├── OrderProcessing.Infrastructure.Tests/
    │   ├── Fix/
    │   └── Persistence/
    ├── OrderGenerator.Tests/
    │   ├── Integration/
    │   └── Validation/
    └── OrderAccumulator.Tests/
        ├── Integration/
        └── Processing/

Adapt the structure to the actual solution.

Do not create empty test projects or unnecessary folders merely to match this example.

Test names must clearly identify the behavior being validated.

## 5. Test Naming and Structure

Use descriptive test names that communicate:

- The method or behavior.
- The scenario.
- The expected result.

Preferred naming convention:

`MethodName_Scenario_ExpectedResult`

Examples:

- `CalculateExposure_WhenBuyOrderIsAccepted_IncreasesExposure`
- `CalculateExposure_WhenSellOrderIsAccepted_DecreasesExposure`
- `ProcessOrder_WhenExposureLimitIsExceeded_RejectsOrder`
- `ProcessOrder_WhenDuplicateClOrdIdIsReceived_DoesNotUpdateExposureAgain`
- `ProcessOrder_WhenConcurrentOrdersExceedLimit_RejectsExcessOrders`

Use Arrange, Act and Assert sections when they improve readability.

Each test should validate one primary behavior.

Avoid tests that depend on execution order or shared mutable state.

## 6. Financial Domain Testing

Financial domain tests are mandatory and must use `decimal`.

Never use `double` or `float` for monetary assertions.

Use decimal literals with the `m` suffix.

Example:

    decimal price = 25.50m;
    decimal quantity = 100m;
    decimal notional = price * quantity;

Test calculations using exact decimal expectations.

Avoid approximate floating-point assertions for financial values.

## 7. Order Validation Tests

Validate all challenge requirements.

### 7.1 Symbol Validation

Supported symbols:

- `PETR4`
- `VALE3`
- `VIIA4`

Test cases:

- Each supported symbol is accepted.
- Unsupported symbols are rejected.
- Empty symbols are rejected.
- Null symbols are rejected where applicable.
- Invalid casing follows the documented normalization policy.
- Leading or trailing whitespace follows the documented validation policy.

### 7.2 Side Validation

Test cases:

- Buy is accepted.
- Sell is accepted.
- Unsupported side values are rejected.
- Invalid enum values are rejected.
- Null or missing side values are rejected where applicable.

### 7.3 Quantity Validation

Valid range:

`1 <= Quantity <= 99999`

Test cases:

- Quantity `1` is accepted.
- Quantity `99999` is accepted.
- Quantity `0` is rejected.
- Negative quantity is rejected.
- Quantity `100000` is rejected.
- Fractional quantity is rejected.
- Missing quantity is rejected.

### 7.4 Price Validation

Valid range:

`0 < Price < 1000`

Price must be a multiple of `0.01`.

Test cases:

- Price `0.01` is accepted.
- Prices `1.10`, `1.100` and `1.1000` are accepted as the same numeric multiple of `0.01m`.
- Price `999.99` is accepted.
- Price `0` is rejected.
- Negative price is rejected.
- Price `1000.00` is rejected.
- Price `1000.01` is rejected.
- Price `10.001` is rejected.
- Price `10.005` is rejected.
- Prices `1.101` and `1.105` are rejected.
- Missing price is rejected.

Validate `price % 0.01m == 0` numerically using `decimal`, not the textual number of decimal places.

## 8. Exposure Calculation Tests

The exposure formula is:

`Exposure = Sum(Buy Price * accepted OrderQty) - Sum(Sell Price * accepted OrderQty)`

Test cases must cover:

- Buy orders increase exposure.
- Sell orders decrease exposure.
- Multiple buy orders accumulate correctly.
- Multiple sell orders reduce exposure correctly.
- Mixed buy and sell orders produce the expected net exposure.
- Different symbols maintain independent exposure.
- Zero exposure is handled correctly.
- Negative exposure is handled correctly.
- Large decimal values preserve precision.
- Rejected orders do not affect exposure.

Test that acceptance atomically accounts for the full submitted `Price * OrderQty` immediately. This is challenge accounting only: an accepted `NewOrderSingle` has not been executed in the market.

## 9. Exposure Limit Tests

The maximum absolute exposure per symbol is:

`100000000.00m`, a fixed domain constant rather than a configurable option.

The valid range is inclusive:

`-100000000.00m <= Exposure <= 100000000.00m`

Mandatory boundary tests:

| Scenario | Expected result |
|---|---|
| Projected exposure = `100000000.00m` | Accepted |
| Projected exposure = `100000000.01m` | Rejected |
| Projected exposure = `-100000000.00m` | Accepted |
| Projected exposure = `-100000000.01m` | Rejected |
| Projected exposure inside the limit | Accepted |
| Projected exposure outside the limit | Rejected |

Also test:

- Buy order exceeding the positive limit.
- Sell order exceeding the negative limit.
- Order that moves exposure back inside the limit.
- Order that moves exposure from positive to negative.
- Order that moves exposure from negative to positive.
- Independent limits for each symbol.

Use exact decimal values for boundary assertions.

## 10. Atomicity and Concurrency Tests

Concurrency testing is mandatory for exposure processing.

The system must not allow simultaneous orders to bypass the exposure limit.

Test cases:

- Multiple concurrent orders for the same symbol.
- Concurrent buy orders approaching the positive limit.
- Concurrent sell orders approaching the negative limit.
- Concurrent orders for different symbols.
- Simultaneous duplicate `ClOrdID` submissions.
- Simultaneous same-ID submissions with different content and different symbols must not bypass global identity checks.
- Concurrent acceptance and rejection decisions.
- Consistency of final exposure after concurrent processing.

Example scenario:

1. Initial exposure is close to the maximum.
2. Two orders are submitted concurrently.
3. Each order individually appears valid against the initial exposure.
4. Together, the orders would exceed the limit.
5. The system must accept only the orders that keep exposure within the limit.

Verify that the final exposure never exceeds the permitted range.

Do not rely on arbitrary delays such as `Task.Delay` to simulate concurrency.

Use synchronization primitives such as `TaskCompletionSource`, barriers or controlled test doubles to coordinate concurrent execution deterministically.

Avoid flaky tests based on thread scheduling assumptions.

## 11. Idempotency Tests

`ClOrdID (11)` is the primary client order identifier.

Test cases:

- First submission is processed.
- Duplicate submission does not modify exposure again.
- Identical `ClOrdID` and order content returns or reproduces the original accepted or rejected result without another financial effect.
- Different content with the same `ClOrdID` is rejected as a conflict without changing original order state or exposure.
- Equal numeric prices such as `1.10` and `1.1000` do not by themselves create a content conflict.
- Concurrent duplicate submissions produce only one financial effect.
- Concurrent identical and conflicting submissions, including different symbols, follow the same result and conflict rules.
- Different identifiers represent independent orders.
- Invalid or missing identifiers are rejected where required.
- FIX retransmissions do not create duplicate financial effects.

Idempotency and original-result registration must be tested together with per-symbol exposure updates under a global identity consistency boundary; per-symbol locks alone are insufficient.

A failure at the identity/result/exposure commit boundary must not leave partial state. Inject failures before exposure preparation, during processing and immediately before result registration or snapshot publication; assert the entire published state remains unchanged and retry each request. Simulate a lost response after publication and verify that an identical retry reproduces the committed result. Test concurrent identical and conflicting identifiers across symbols.

A test that only verifies duplicate detection without validating financial state is insufficient.

## 12. Rejection Tests

Rejected orders must not affect financial state.

Test cases:

- Invalid symbol.
- Invalid side.
- Invalid quantity.
- Invalid price.
- Exposure limit exceeded.
- Conflicting reuse of an order identifier with different content.
For every business rejection scenario, verify:

- The order is not accepted.
- The correct rejection reason is returned.
- Exposure remains unchanged.
- No duplicate financial side effect occurs.
- The rejection is correctly represented in the application response.

Test malformed FIX messages and missing required protocol fields separately: expect the applicable FIX Session Reject or Business Message Reject where appropriate, not necessarily an ExecutionReport, and assert no financial state change. Treat timeout, disconnect, unavailable sessions and unexpected application failures as technical or unknown outcomes rather than business rejections.

## 13. FIX Protocol 4.4 Testing

FIX integration tests must validate both message structure and business behavior.

### 13.1 NewOrderSingle

Validate the required fields:

| Field | Tag | Expected purpose |
|---|---:|---|
| ClOrdID | 11 | Client order identifier |
| Symbol | 55 | Trading symbol |
| Side | 54 | Buy or Sell |
| OrderQty | 38 | Order quantity |
| OrdType | 40 | Order type |
| Price | 44 | Limit price |
| TransactTime | 60 | Transaction timestamp |

Validate:

- Buy maps to Side `1`.
- Sell maps to Side `2`.
- Limit order maps to OrdType `2`.
- Quantity is represented correctly.
- Price preserves decimal precision.
- ClOrdID is preserved.
- Required fields are present.
- Invalid messages are rejected.

### 13.2 ExecutionReport

Validate the relevant response fields:

| Field | Tag | Expected purpose |
|---|---:|---|
| OrderID | 37 | Server order identifier |
| ExecID | 17 | Execution report identifier |
| ClOrdID | 11 | Client order correlation |
| Symbol | 55 | Trading symbol |
| Side | 54 | Order side |
| ExecType | 150 | Execution report type |
| OrdStatus | 39 | Order status |
| OrderQty | 38 | Order quantity |
| Price | 44 | Order price |
| CumQty | 14 | Cumulative executed quantity |
| LeavesQty | 151 | Remaining quantity |
| AvgPx | 6 | Average execution price |
| TransactTime | 60 | Transaction timestamp |
| OrdRejReason | 103 | Rejection reason |
| Text | 58 | Additional information |

Validate accepted orders:

- `ExecType = 0` (New).
- `OrdStatus = 0` (New).
- `ClOrdID` matches the original request.
- `CumQty = 0` and `LeavesQty = OrderQty`, even though the full notional is immediately accounted in exposure.
- No field, including `AvgPx`, implies an actual trade.

Validate rejected orders:

- `ExecType = 8` (Rejected).
- `OrdStatus = 8` (Rejected).
- `ClOrdID` matches the original request.
- Rejection information is meaningful.
- Exposure remains unchanged.

Do not treat `ExecType = New` as proof that a trade was executed.

### 13.3 FIX Session Tests

Test:

- Initiator startup.
- Acceptor startup.
- Successful Logon.
- Logout.
- Unexpected disconnection.
- Session reconnection.
- Invalid session identifiers.
- Missing required configuration.
- Invalid configuration values.
- Message delivery failures.
- Sequence number handling.
- Duplicate message handling.
- Session lifecycle recovery.

Use isolated session configuration and temporary message stores where appropriate.

Do not let integration tests connect to production endpoints.

## 14. Asynchronous Correlation Tests

OrderGenerator must correlate asynchronous ExecutionReports with pending requests.

Test cases:

- Response received for an existing ClOrdID.
- Response received for an unknown ClOrdID.
- Responses received out of order.
- Multiple requests pending simultaneously.
- Request timeout.
- FIX session disconnect while requests are pending.
- Duplicate ExecutionReport.
- Pending request cleanup.
- Response received after timeout.

A timeout must not be interpreted as an order rejection.

A late response must not corrupt another request's state.

Use deterministic synchronization and controlled test doubles rather than arbitrary delays.

## 15. ASP.NET Core Integration Tests

Test the OrderGenerator HTTP interface.

Validate:

- Application startup.
- Order submission endpoint or page.
- Required field validation.
- Invalid request handling.
- Server-side validation.
- Application service invocation.
- Accepted order presentation.
- Rejected order presentation.
- Pending order presentation.
- Unknown outcome presentation.
- Error handling.
- Dependency injection configuration.

Use `WebApplicationFactory<TEntryPoint>` when appropriate.

Do not require a real FIX connection for tests that only validate HTTP behavior.

Mock or replace the FIX client boundary when testing the web application in isolation.

## 16. Application Integration Tests

Test interactions between application components.

Validate:

- OrderGenerator sends a valid NewOrderSingle.
- OrderAccumulator receives and parses the message.
- The domain validates the order.
- Exposure is evaluated and updated atomically.
- An ExecutionReport is generated.
- The response is correlated with the original request.
- Rejected orders do not affect exposure.
- Duplicate messages do not create duplicate financial effects.

Prefer controlled local FIX sessions for real protocol integration tests.

Keep tests isolated from external infrastructure.

## 17. Test Doubles and Mocking

Use test doubles only where they provide meaningful isolation.

Requirements:

- Mock external dependencies.
- Keep domain tests independent of infrastructure.
- Avoid mocking the behavior under test.
- Avoid excessive interaction verification.
- Prefer simple fakes for deterministic stateful dependencies.
- Use real implementations for important serialization and protocol behavior when practical.

Do not mock every class by default.

For financial calculations and exposure rules, prefer testing the actual domain implementation.

## 18. Test Data Management

Test data must be deterministic and isolated.

Requirements:

- Use explicit decimal values.
- Use unique identifiers where required.
- Avoid shared mutable state.
- Reset in-memory state between tests.
- Isolate FIX sessions and message stores.
- Avoid depending on test execution order.
- Avoid production data.
- Avoid real credentials.

Use builders or test data factories when they improve readability.

Do not create overly generic test frameworks for a small challenge.

## 19. Test Quality and Reliability

Tests must be:

- Deterministic.
- Independent.
- Readable.
- Repeatable.
- Focused.
- Fast where possible.
- Explicit about expected behavior.

Avoid:

- Arbitrary sleeps.
- Time-dependent assertions without controlled clocks.
- Network dependencies in unit tests.
- Shared mutable fixtures without isolation.
- Excessive mocking.
- Tests that pass without asserting meaningful outcomes.
- Assertions based on implementation details rather than behavior.

When testing time-sensitive behavior, inject or control time where appropriate.

## 20. Test Coverage

Coverage is useful but must not replace meaningful assertions.

Prioritize coverage of:

- Financial domain rules.
- Validation.
- Exposure calculations.
- Exposure limits.
- Atomicity.
- Idempotency.
- FIX message mapping.
- Request correlation.
- Rejection handling.
- HTTP application behavior.

Do not optimize for a specific coverage percentage at the expense of test quality.

Do not add meaningless tests solely to increase coverage.

Report uncovered critical business paths explicitly.

## 21. Test Execution

Use the .NET CLI to execute tests.

Restore dependencies:

    dotnet restore

Build the solution:

    dotnet build

Execute all tests:

    dotnet test

Execute tests in Release configuration:

    dotnet test --configuration Release

Execute a specific test project:

    dotnet test path/to/TestProject.csproj

Execute tests without rebuilding:

    dotnet test --no-build

Use `--no-build` only when a successful build for the current source state has already been performed.

Capture and report:

- Command executed.
- Test projects discovered.
- Total tests.
- Passed tests.
- Failed tests.
- Skipped tests.
- Execution errors.
- Relevant warnings.

Never claim that tests passed if they were not executed.

## 22. Test Failure Analysis

When a test fails:

1. Identify the failing test.
2. Inspect the assertion and stack trace.
3. Determine whether the failure is caused by production code, test code or environment.
4. Reproduce the failure when possible.
5. Identify the underlying cause.
6. Correct the implementation or test only when justified.
7. Re-run the affected test.
8. Re-run the relevant test suite.
9. Report remaining failures.

Do not modify assertions merely to make tests pass.

Do not weaken financial validation or business rules to eliminate failures.

Do not ignore intermittent failures without investigating their cause.

## 23. Test Scope and Project Limitations

The challenge may use in-memory state and local FIX sessions.

Tests must accurately reflect these limitations.

If the implementation uses in-memory exposure state, validate:

- State isolation between tests.
- Correct behavior during application lifetime.
- Expected reset behavior after restart.
- No unsupported claims of persistence or multi-instance consistency.

If persistence is introduced, test transaction boundaries, consistency and recovery.

Do not claim production-grade durability based only on in-memory tests.

## 24. Definition of Done

A feature is not considered complete until:

- [ ] Relevant unit tests exist.
- [ ] Financial calculations use decimal.
- [ ] Validation scenarios are covered.
- [ ] Exposure boundary tests pass.
- [ ] Rejected orders preserve exposure.
- [ ] Atomicity is tested where shared state is modified.
- [ ] Idempotency is tested.
- [ ] FIX message mapping is tested where applicable.
- [ ] Asynchronous correlation is tested where applicable.
- [ ] Integration tests cover relevant application boundaries.
- [ ] Tests are deterministic.
- [ ] Relevant test suites have been executed.
- [ ] Failures and limitations are documented.

## 25. Instructions for GitHub Copilot

When creating or modifying tests:

1. Inspect the existing solution and test conventions.
2. Read the architecture, C#, .NET, financial domain, FIX and security instructions.
3. Identify the behavior and expected result before writing the test.
4. Prefer tests that validate externally observable behavior.
5. Use decimal values for financial assertions.
6. Cover boundary conditions and negative scenarios.
7. Include concurrency and idempotency tests for financial state changes.
8. Use deterministic synchronization for asynchronous and concurrent tests.
9. Keep unit tests independent of FIX infrastructure.
10. Use real FIX sessions only when protocol integration is the behavior under test.
11. Never weaken production requirements to make tests pass.
12. Never fabricate test execution results.
13. Report missing coverage and unverified scenarios explicitly.

Align test expectations with the documented decision: immediate full-notional exposure accounting is not market execution.
