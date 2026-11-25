# final-project-validation reference 2

## 4. Functional Validation

Verify the complete order lifecycle.

### 4.1 Order submission

Confirm that OrderGenerator:

- Presents all required fields.
- Restricts symbols to supported values.
- Allows Buy and Sell.
- Validates quantity.
- Validates price.
- Performs server-side validation.
- Prevents invalid orders from being submitted.
- Provides meaningful feedback to the user.

### 4.2 FIX communication

Confirm that:

- OrderGenerator acts as FIX Initiator.
- OrderAccumulator acts as FIX Acceptor.
- Both applications establish the expected FIX session.
- NewOrderSingle messages are correctly generated and received.
- ExecutionReports are correctly generated and correlated.
- Session disconnections and reconnections are handled appropriately.

### 4.3 Accepted orders

Verify that accepted orders:

- Pass all business validations.
- Respect the exposure limit.
- Return `ExecType = 0`.
- Return `OrdStatus = 0`.
- Include the appropriate order identifiers.
- Preserve the correct symbol, side, quantity and price.
- Atomically apply the full signed `Price × OrderQty` to exposure.

Verify `CumQty = 0`, `LeavesQty = OrderQty` and that `AvgPx` does not imply a trade.

### 4.4 Rejected orders

Verify that rejected orders:

- Return `ExecType = 8`.
- Return `OrdStatus = 8`.
- Include an appropriate rejection reason.
- Preserve the original order correlation identifier.
- Do not change exposure.
- Do not leave inconsistent or partially updated state.

### 4.5 User feedback

Verify that OrderGenerator distinguishes:

- Pending submission.
- Accepted order.
- Rejected order.
- Communication failure.
- Unknown outcome caused by timeout or lost connection.

A timeout must not automatically be interpreted as rejection.

---

## 5. Financial Integrity Validation

This section is a mandatory release gate.

### 5.1 Numeric precision

Verify that:

- Financial calculations use `decimal`.
- No `double` or `float` is used for monetary calculations.
- Price satisfies `Price > 0`, `Price < 1000` and `Price % 0.01m == 0` numerically; verify `1.100` accepted and `1.101`, `1.105` and `0.001` rejected.
- Exposure calculations do not introduce floating-point errors.
- Serialization and FIX conversion preserve the intended monetary values.

### 5.2 Exposure calculations

Verify the formula:

`Exposure = Buy Notional - Sell Notional`

Where:

`Notional = Price × OrderQty` for accepted orders.

Test positive, negative and zero exposure.

Verify that each supported symbol has independent exposure.

### 5.3 Exposure limits

Verify:

- The limit is the domain constant `100_000_000.00m`, not external configuration.

| Scenario | Expected result |
|---|---|
| Exposure below +100,000,000 | Accepted |
| Exposure exactly +100,000,000 | Accepted |
| Exposure above +100,000,000 | Rejected |
| Exposure above -100,000,000 | Accepted |
| Exposure exactly -100,000,000 | Accepted |
| Exposure below -100,000,000 | Rejected |

Also verify that an order is evaluated against the projected exposure after applying its financial impact.

### 5.4 Concurrency and atomicity

Verify that simultaneous orders for the same symbol cannot bypass the exposure limit.

Confirm that exposure validation and state mutation are atomic.

Verify that orders for different symbols do not unnecessarily block each other.

### 5.5 Idempotency

Verify that duplicate messages or repeated `ClOrdID` values cannot cause duplicate financial updates.

Confirm that FIX retransmissions do not apply the same order more than once.

Verify identical content reproduces the original accepted or business-rejected result. Conflicting content is rejected without overwriting that result or changing exposure. Verify atomic globally keyed `ClOrdID` coordination, including different or invalid symbols, with result registration and exposure updates for concurrent requests.

### 5.6 Rejection integrity

Verify that every rejected order leaves exposure unchanged while its original business-rejected result is retained.

A failed validation must not partially modify exposure or leave inconsistent order or accounting data.

---

## 6. FIX Protocol Validation

Inspect the actual FIX message implementation.

### 6.1 NewOrderSingle

Verify the presence and correctness of the relevant fields:

| Field | Tag | Expected purpose |
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
- Limit order: `OrdType = 2`

### 6.2 ExecutionReport

Verify the relevant fields:

| Field | Tag | Expected purpose |
|---|---:|---|
| OrderID | 37 | Server order identifier |
| ExecID | 17 | Execution report identifier |
| ClOrdID | 11 | Client order correlation |
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

Confirm that fields are populated according to the actual order lifecycle and FIX 4.4 semantics.

### 6.3 Correlation and asynchronous behavior

Verify:

- Thread-safe pending request management.
- Correct correlation through `ClOrdID`.
- Proper handling of responses arriving asynchronously.
- Cleanup of completed requests.
- Timeout handling.
- Behavior after connection loss.
- No automatic resubmission after an uncertain outcome.

### 6.4 Session reliability

Verify:

- Correct session identifiers.
- Sender and Target CompIDs.
- FIX version.
- Network endpoints.
- Heartbeat configuration.
- Logon and Logout behavior.
- Sequence number management.
- Message persistence and recovery configuration.
- Duplicate and out-of-order message handling.

Do not consider a mocked FIX test sufficient proof of real session interoperability.

---

## 7. Architecture and Code Quality

Verify that:

- Domain logic is independent of ASP.NET Core and QuickFIX/n.
- FIX handlers delegate business operations to application services.
- Controllers and Razor Pages do not contain financial calculations.
- Infrastructure concerns remain outside the domain layer.
- Dependencies follow the intended architectural direction.
- Dependency injection is configured consistently.
- Interfaces exist where they provide meaningful boundaries.
- Classes and methods have clear responsibilities.
- Async operations are handled correctly.
- Cancellation and resource disposal are considered.
- Error handling is consistent.
- Logging is useful and does not expose sensitive information.

Identify unnecessary abstractions, overengineering and avoidable coupling.

Do not require design patterns merely for their presence.

---

