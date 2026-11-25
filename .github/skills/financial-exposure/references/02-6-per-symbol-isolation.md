# financial-exposure reference 2

## 6. Per-Symbol Isolation

Exposure must be maintained independently for each supported symbol.

Conceptual representation:

    PETR4 → Exposure
    VALE3 → Exposure
    VIIA4 → Exposure

A transaction involving PETR4 must not modify VALE3 or VIIA4 exposure.

Avoid a single aggregate exposure limit across all instruments.

### Required Invariants

- Each symbol has independent exposure.
- Each symbol has independent limit validation.
- Each symbol has independent exposure; the global lock serializes updates across symbols.
- Rejected orders do not modify any symbol exposure.
- Unknown symbols are rejected before state mutation.

---

## 7. Atomicity and Concurrency

Financial validation and state mutation must be atomic.

The following sequence is unsafe when concurrent requests are possible:

1. Read current exposure.
2. Calculate projected exposure.
3. Validate the limit.
4. Update exposure.

Two concurrent requests can read the same previous exposure and both be accepted even when their combined impact exceeds the limit.

### Required Processing Model

Treat validation and update as one indivisible operation for the affected symbol.

Conceptual workflow:

1. Coordinate the globally keyed `ClOrdID` across requests, including those with different or invalid symbols.
2. Check for an existing result and compare business content.
3. Validate a new order and, for a valid symbol, synchronize its exposure.
4. Read exposure, calculate the projected value and validate the limit.
5. Atomically update exposure for acceptance and record the original accepted or rejected result.
6. Release synchronization and generate the appropriate response.

The identifier claim, result registration and financial transition form one consistency boundary. A per-symbol lock alone cannot protect the same identifier reused across different symbols. A business rejection is recorded even if the symbol or another field is invalid; malformed protocol messages and technical failures remain distinct.

### Synchronization Strategy

Use one global lock and a complete unpublished copy-on-write snapshot containing all
symbol exposures and original `ClOrdID` results. Construct the result and all state
changes before one state reference assignment; never mutate the published snapshot.
Pre-commit failures leave both identity history and exposure unchanged. After publication,
a lost response is unknown and an identical retry returns the committed result.

The lock deliberately serializes different symbols; snapshot copying costs time and
memory. Keep network and unrelated I/O outside the lock. A thread-safe collection alone
cannot protect this compound transition.

---

## 8. Idempotency

The same logical order must never change exposure more than once.

FIX uses `ClOrdID (11)` as the client order identifier.

Use it as the primary correlation and globally keyed duplicate-detection identifier in the accumulator.

### Required Behavior

For a new identifier, process normally and retain the accepted or rejected business result and original order content.

- Identical content: return or reproduce that original result, with no second financial effect.
- Different content: reject the identifier reuse conflict without overwriting the original result or changing exposure.
- Concurrent submissions: exactly one processing decision wins; others observe its original result or a conflict according to content.

Define and document the business fields used for content comparison. Compare prices by decimal numeric value, so `1.10` and `1.100` are equal; do not mistake FIX transport metadata for changed order content.

### Duplicate Detection

Verify that:

- Duplicate checks are thread-safe.
- Registration and exposure mutation are coordinated.
- Concurrent duplicates cannot produce two financial transitions.
- Retransmitted FIX messages do not create duplicate financial updates.
- Identifier scope is explicitly defined.
- Conflicting reuse of an identifier is rejected safely, including across symbols.

A duplicate identifier with different business fields must not silently be treated as a legitimate new order.

### Persistence Considerations

For in-memory implementations, document that idempotency state is lost after restart unless recovery mechanisms exist.

For persistent implementations, ensure that duplicate detection and financial updates participate in the same consistency boundary.

---

## 9. Rejection Integrity

Rejected orders must not affect exposure or produce a financial transition. Retain the original business-rejected result and content for idempotent replay.

A rejected order must not:

- Increase exposure.
- Decrease exposure.
- Partially update exposure.
- Overwrite the original result on conflicting identifier reuse.
- Leave inconsistent order state.
- Cause an accepted response to be generated.

### Rejection Scenarios

Examples include:

- Unsupported symbol.
- Invalid side.
- Quantity outside permitted bounds.
- Invalid price.
- Price not numerically divisible by 0.01.
- Exposure limit exceeded.
- Duplicate identifier conflict.

Malformed or incompatible FIX messages are protocol failures, not automatically business rejections.

Distinguish business rejections from technical failures.

A FIX protocol failure is not necessarily a business rejection.

---

## 10. FIX Protocol Mapping

The financial domain must remain independent of QuickFIX/n types.

Translate FIX messages into internal application contracts before executing business rules.

### NewOrderSingle

Relevant fields:

| Field | Tag | Meaning |
|---|---:|---|
| ClOrdID | 11 | Client order identifier |
| Symbol | 55 | Financial instrument |
| Side | 54 | Buy or Sell |
| OrderQty | 38 | Order quantity |
| OrdType | 40 | Order type |
| Price | 44 | Order price |
| TransactTime | 60 | Transaction timestamp |

Expected values:

- Buy: `Side = 1`
- Sell: `Side = 2`
- Limit: `OrdType = 2`

### ExecutionReport

Relevant fields:

| Field | Tag | Meaning |
|---|---:|---|
| OrderID | 37 | Server order identifier |
| ExecID | 17 | Execution report identifier |
| ClOrdID | 11 | Client order identifier |
| Symbol | 55 | Financial instrument |
| Side | 54 | Order side |
| ExecType | 150 | Execution event type |
| OrdStatus | 39 | Current order status |
| OrderQty | 38 | Original order quantity |
| Price | 44 | Order price |
| CumQty | 14 | Cumulative executed quantity |
| LeavesQty | 151 | Remaining quantity |
| AvgPx | 6 | Average execution price |
| TransactTime | 60 | Transaction timestamp |
| OrdRejReason | 103 | Rejection reason |
| Text | 58 | Human-readable explanation |

### Accepted Order

For an accepted but unexecuted order:

- `ExecType = 0`
- `OrdStatus = 0`
- `CumQty = 0`
- `LeavesQty = OrderQty`

The full accepted notional affects exposure without changing these FIX quantities.

### Rejected Order

For a rejected order:

- `ExecType = 8`
- `OrdStatus = 8`
- Include an appropriate rejection explanation.
- Preserve the original `ClOrdID`.

Do not populate execution-related fields as though a trade occurred when no execution has taken place.

---

## 11. Order Processing Workflow

A recommended financial processing workflow:

1. Receive NewOrderSingle.
2. Validate mandatory FIX fields.
3. Translate the message into an internal order request.
4. Acquire the global lock, coordinate the globally keyed `ClOrdID` and check original content and result.
5. Return the original result for identical content or reject conflicting reuse.
6. For a new identifier, validate symbol, side, quantity and numeric price increment.
7. Under the global lock, calculate projected exposure and prepare the accepted or rejected result.
8. Prepare an unpublished snapshot with both the resulting exposure and original result.
9. Publish the entire snapshot once; all fallible business-result and snapshot preparation precedes publication.
10. Release synchronization, generate the appropriate ExecutionReport and return it through the FIX session.

Ensure that failures cannot leave partially updated financial state.

---

