# financial-exposure reference 3

## 12. Error and Timeout Semantics

Differentiate:

- Invalid order.
- Exposure limit exceeded.
- Duplicate order.
- Invalid FIX message.
- FIX session unavailable.
- Network failure.
- Processing timeout.
- Unexpected application failure.

### Unknown Outcome

A communication timeout does not prove that the order was rejected.

The order may have been processed successfully while its response was lost or delayed.

The system must represent an unknown outcome explicitly.

Do not automatically resubmit an order after timeout. A deliberate identical retry must preserve the original identifier and content.

Use `ClOrdID` to correlate responses and support reconciliation.

---

## 13. In-Memory State Limitations

An in-memory exposure manager can be acceptable for the challenge if its limitations are explicit.

Verify:

- State is thread-safe.
- Financial operations are atomic.
- Duplicate detection is coordinated with exposure updates.
- Symbol isolation is maintained.
- Restart behavior is documented.
- Multi-instance limitations are disclosed.

### Operational Limitations

An in-memory implementation generally loses:

- Exposure after restart.
- Processed-order identifiers.
- Order history.
- Pending business state.

It also does not automatically provide shared consistency across multiple application instances.

Do not describe an in-memory implementation as durable financial accounting.

---

## 14. Financial Testing Strategy

Financial behavior must be tested with deterministic automated tests.

### Order Validation

| Scenario | Expected result |
|---|---|
| PETR4 | Valid symbol |
| VALE3 | Valid symbol |
| VIIA4 | Valid symbol |
| Unsupported symbol | Rejected |
| Buy | Valid side |
| Sell | Valid side |
| Invalid side | Rejected |
| Quantity = 1 | Accepted |
| Quantity = 99,999 | Accepted |
| Quantity = 0 | Rejected |
| Quantity = 100,000 | Rejected |
| Price = 0 | Rejected |
| Price below 0 | Rejected |
| Price = 999.99 | Accepted |
| Price = 1,000 | Rejected |
| Price = 1.10, 1.100 or 1.1000 | Accepted |
| Price = 1.101, 1.105 or 0.001 | Rejected |

### Exposure Calculations

Test:

- Buy increases exposure.
- Sell decreases exposure.
- Multiple buys.
- Multiple sells.
- Mixed Buy and Sell operations.
- Zero exposure.
- Positive exposure.
- Negative exposure.
- Independent symbols.
- Large valid quantities.
- Decimal precision.

### Exposure Boundaries

| Projected exposure | Expected result |
|---:|---|
| 99,999,999.99 | Accepted |
| 100,000,000.00 | Accepted |
| 100,000,000.01 | Rejected |
| -99,999,999.99 | Accepted |
| -100,000,000.00 | Accepted |
| -100,000,000.01 | Rejected |

### Rejection Integrity

Verify that:

- Invalid symbols do not change exposure.
- Invalid quantities do not change exposure.
- Invalid prices do not change exposure.
- Exposure-limit rejections do not change exposure.
- Duplicate orders do not change exposure twice.

### Concurrency

Test:

- Concurrent orders for the same symbol.
- Concurrent orders for different symbols.
- Multiple orders approaching the exposure limit.
- Concurrent duplicate identifiers.
- Concurrent accepted and rejected orders.
- Atomicity under simultaneous processing.

Inject failures before exposure preparation, during processing and before result registration or snapshot publication; assert the entire published state remains unchanged and retry. Simulate a lost response after publication and verify an identical retry reproduces the committed result.

A concurrency test must verify the final exposure and accepted/rejected outcomes, not merely the absence of exceptions.

### Idempotency

Test:

- Identical `ClOrdID` and content replay accepted and rejected original results sequentially and concurrently.
- Same `ClOrdID` with conflicting content, including a different symbol, is rejected without changing the original result or exposure.
- Retransmitted FIX message.
- Same identifier with conflicting fields.
- Duplicate response handling.
- Behavior after restart when state is in-memory.

---

## 15. Common Financial Implementation Errors

Avoid:

- Using `double` or `float` for monetary values.
- Validating current exposure instead of projected exposure.
- Applying the exposure limit to all symbols collectively.
- Using an incorrect sign for Sell orders.
- Updating exposure before validation completes.
- Updating exposure outside the synchronization boundary.
- Treating a rejected order as accepted.
- Applying duplicate orders more than once.
- Assuming thread-safe collections guarantee atomic financial operations.
- Confusing accepted quantity with executed quantity.
- Treating `ExecType = New` as a trade execution.
- Automatically resubmitting uncertain orders.
- Rounding monetary values without a defined rule.
- Losing consistency between order state and exposure state.
- Claiming durable financial state when only in-memory storage exists.

---

## 16. Financial Exposure Review Checklist

### Validation

- [ ] Supported symbols are enforced.
- [ ] Buy and Sell are correctly represented.
- [ ] Quantity boundaries are correct.
- [ ] Price boundaries are correct.
- [ ] Price precision is enforced.
- [ ] Validation occurs before exposure mutation, and rejected business results are retained.

### Calculation

- [ ] Monetary calculations use `decimal`.
- [ ] Buy increases exposure.
- [ ] Sell decreases exposure.
- [ ] Exposure uses the correct quantity.
- [ ] Exposure is calculated independently per symbol.
- [ ] No unintended rounding occurs.

### Limits

- [ ] Absolute exposure limit is the domain constant `100_000_000.00m`, not external configuration.
- [ ] Positive boundary is inclusive.
- [ ] Negative boundary is inclusive.
- [ ] Projected exposure is validated.
- [ ] Rejected orders leave exposure unchanged.

### Consistency

- [ ] Validation and update are atomic.
- [ ] Concurrent orders cannot bypass the limit.
- [ ] Identical duplicates reproduce accepted and rejected results; conflicts are rejected atomically across symbols.
- [ ] Order state and exposure remain consistent.
- [ ] Different symbols are isolated.

### FIX Integration

- [ ] FIX types do not leak into the domain.
- [ ] NewOrderSingle fields are validated.
- [ ] ExecutionReport statuses are correct.
- [ ] ClOrdID correlation is maintained.
- [ ] Timeout does not imply rejection.
- [ ] Retransmissions do not duplicate financial updates.

### Testing and Documentation

- [ ] Financial boundaries have automated tests.
- [ ] Concurrency is tested.
- [ ] Idempotency is tested.
- [ ] Rejection integrity is tested.
- [ ] Acceptance-versus-execution semantics are documented.
- [ ] Persistence and recovery limitations are documented.

---

