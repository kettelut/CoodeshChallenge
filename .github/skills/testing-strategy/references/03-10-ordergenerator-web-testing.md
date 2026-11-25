# testing-strategy reference 3

## 10. OrderGenerator Web Testing

Use ASP.NET Core integration testing where appropriate.

Validate:

- Application responds successfully.
- Order submission endpoint or page is accessible.
- Valid orders are accepted for submission.
- Invalid orders are rejected server-side.
- Symbol selection is restricted to supported symbols.
- Buy and Sell values are mapped correctly.
- Quantity validation works.
- Price validation works.
- FIX submission failures are handled correctly.
- Pending requests are represented accurately.
- Accepted and rejected responses are displayed correctly.
- Timeout is represented as an unknown or pending outcome, not as a rejection.
- Sensitive internal exceptions are not exposed to users.

Test both valid and invalid HTTP requests independently of browser-side validation.

### 10.1 Input validation matrix

Include:

- Empty symbol.
- Unsupported symbol.
- Missing side.
- Invalid side.
- Quantity zero.
- Negative quantity.
- Quantity above maximum.
- Price zero.
- Negative price.
- Price equal to or greater than 1,000.
- Numerically valid trailing-zero prices (`1.10`, `1.100`, `1.1000`) and prices not divisible by `0.01m` (`1.101`, `1.105`, `0.001`).
- Missing price.
- Malformed numeric input.
- Extremely long or unexpected input values.

### 10.2 Web security tests

Validate:

- Anti-forgery protection where applicable.
- Input encoding and output encoding.
- No unsafe HTML rendering.
- Appropriate request size limits.
- No sensitive information in error responses.
- No secrets in rendered HTML.
- Correct HTTP status codes.
- Secure cookie configuration when cookies are used.

---

## 11. End-to-End Testing

End-to-end tests must validate the complete user-to-business workflow.

Recommended scenarios:

### Scenario 1: Accepted Buy order

1. Submit a valid Buy order for PETR4.
2. Verify that OrderGenerator creates a valid NewOrderSingle.
3. Verify that OrderAccumulator receives the message.
4. Verify that business validation succeeds.
5. Verify that exposure is updated atomically by the full accepted notional.
6. Verify that the ExecutionReport has `ExecType=0`, `OrdStatus=0`, `CumQty=0` and `LeavesQty=OrderQty`.
7. Verify that OrderGenerator correlates the response correctly.
8. Verify the final order status.

### Scenario 2: Accepted Sell order

1. Submit a valid Sell order.
2. Verify correct FIX Side mapping.
3. Verify correct notional calculation.
4. Verify exposure reduction.
5. Verify accepted ExecutionReport.
6. Verify the final order status.

### Scenario 3: Exposure limit rejection

1. Establish exposure close to the fixed domain limit.
2. Submit an order that exceeds the limit.
3. Verify that OrderAccumulator rejects the order.
4. Verify `ExecType=Rejected`.
5. Verify that exposure remains unchanged.
6. Verify that OrderGenerator displays the rejection.

### Scenario 4: Exact exposure boundary

1. Establish exposure immediately below the limit.
2. Submit an order that reaches exactly R$100,000,000.
3. Verify that the order is accepted.
4. Verify the exact final exposure.

### Scenario 5: Duplicate order

1. Submit an order with a specific ClOrdID.
2. Submit the same message again.
3. Verify that the order is processed idempotently.
4. Verify that exposure changes only once.
5. Verify identical accepted and rejected submissions reproduce their original results.
6. Reuse the identifier with conflicting content, including a different symbol, and verify conflict rejection without modifying original state.
7. Repeat identical and conflicting cases concurrently and verify one decision and at most one financial transition.

### Scenario 6: Concurrent orders

1. Establish a known exposure.
2. Submit multiple orders concurrently for the same symbol.
3. Verify atomic validation.
4. Verify that only valid orders are accepted.
5. Verify that final exposure remains within the limit.

### Scenario 7: FIX connection failure

1. Make the Acceptor unavailable.
2. Submit an order through OrderGenerator.
3. Verify that the application reports the communication failure.
4. Verify that it does not falsely report business rejection.
5. Verify that no unsafe automatic resubmission occurs.

### Scenario 8: Response timeout

1. Submit an order.
2. Simulate a delayed ExecutionReport.
3. Verify that the client timeout is handled.
4. Verify that the outcome is represented as unknown.
5. Deliver the delayed response.
6. Verify that the original request is correlated safely.
7. Verify that exposure is not modified twice.

---

## 12. Test Data and Test Isolation

Test data must be explicit and reproducible.

Prefer:

- Test builders for valid orders.
- Object mothers for common business scenarios.
- Explicit decimal values.
- Dedicated FIX session identifiers.
- Unique temporary directories.
- Isolated network ports.
- Controlled clocks when timestamps affect behavior.

Avoid:

- Shared mutable global state.
- Production configuration.
- Production FIX endpoints.
- Real credentials.
- Dependence on external systems.
- Random values without a fixed seed.
- Tests that depend on execution order.

Each test must leave the system in a clean state.

For in-memory exposure implementations, ensure that tests use isolated service instances or explicitly reset state between scenarios.

---

## 13. Test Doubles and Mocking

Use mocks or substitutes only at external boundaries.

Appropriate candidates:

- FIX transport abstractions.
- Clock or time provider.
- External services.
- Logging abstractions.
- Persistence interfaces.
- Application-level dependencies.

Do not mock:

- The financial calculation being tested.
- Exposure validation in exposure tests.
- Idempotency logic in idempotency tests.
- FIX serialization in tests intended to validate serialization.
- The complete order-processing workflow in end-to-end tests.

For concurrency and integration tests, prefer real implementations whenever practical.

Use test doubles to isolate dependencies, not to bypass the behavior under validation.

---

## 14. Determinism and Asynchronous Testing

Asynchronous processing is central to FIX communication.

Tests must avoid timing assumptions.

Prefer:

- `TaskCompletionSource` with `RunContinuationsAsynchronously`.
- Explicit synchronization signals.
- Cancellation tokens.
- Bounded timeouts.
- Controlled test hooks.
- Awaitable completion conditions.

Avoid:

- Arbitrary `Task.Delay` as the primary synchronization mechanism.
- Blocking calls such as `.Wait()` and `.Result`.
- Unbounded waiting.
- Thread sleeps.
- Shared mutable test state.

Ensure that every asynchronous test has a maximum execution time and fails with a useful diagnostic message.

Test cancellation separately from timeout and business rejection.

---

