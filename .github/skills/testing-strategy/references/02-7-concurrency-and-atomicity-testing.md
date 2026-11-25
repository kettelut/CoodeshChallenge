# testing-strategy reference 2

## 7. Concurrency and Atomicity Testing

Concurrency is a critical financial integrity requirement.

Exposure validation and state modification must be atomic per symbol, coordinated with globally keyed `ClOrdID` handling. A symbol lock alone cannot prevent same-identifier races across different or invalid symbols.

Inject failures before exposure preparation, during processing and just before result registration or snapshot publication. Assert that the complete published exposure and identity state remains unchanged and retry. Simulate a lost response after publication and verify the identical retry reproduces the committed result.

A test that only submits concurrent requests and checks that no exception occurred is insufficient.

### 7.1 Concurrent limit validation

Create a deterministic scenario where two or more orders independently appear valid against the same initial exposure but cannot all be accepted together.

Example:

    Initial PETR4 exposure: R$99,000,000.00

    Concurrent Buy A: R$600,000.00
    Concurrent Buy B: R$600,000.00

    Expected:
    - Exactly one order is accepted.
    - The other order is rejected.
    - Final exposure is R$99,600,000.00.

The exact accepted order may vary, but the final financial state must always remain valid.

### 7.2 Concurrent order submissions

Test:

- Multiple Buy orders for the same symbol.
- Multiple Sell orders for the same symbol.
- Mixed Buy and Sell orders for the same symbol.
- Orders for different symbols.
- High-volume parallel submissions.
- Concurrent duplicate ClOrdIDs.
- Concurrent requests close to the exposure boundary.

### 7.3 Atomicity assertions

After each concurrency test, verify:

- Final exposure is correct.
- No accepted order was omitted from accounting.
- No rejected order affected exposure.
- No order was accounted for more than once.
- Exposure never exceeds the fixed domain constant.
- Per-symbol isolation remains intact.

Use synchronization primitives such as `Barrier`, `TaskCompletionSource` or controlled test hooks when necessary.

Avoid relying on arbitrary delays such as `Task.Delay` to create concurrency conditions.

### 7.4 Race condition testing

Attempt to reproduce:

- Simultaneous read-modify-write operations.
- Duplicate message processing.
- Concurrent idempotency checks.
- Order acceptance followed by inconsistent response generation.
- Session callbacks arriving concurrently.
- Application shutdown during active processing.

Repeat critical concurrency tests enough times to expose intermittent failures while keeping them deterministic and bounded.

---

## 8. FIX Protocol 4.4 Testing

FIX integration tests must validate protocol behavior and business behavior separately.

Assert rejected ExecutionReports for protocol-valid business-invalid orders. Test malformed or protocol-incompatible messages against the applicable Session Reject or Business Message Reject where appropriate, without requiring an order report or changing financial state. Test timeout, disconnect and session unavailability as communication failures or unknown outcomes.

Do not rely exclusively on mocked FIX messages to claim protocol compatibility.

### 8.1 NewOrderSingle construction

Validate the required fields:

| FIX tag | Field | Expected value |
|---|---|---|
| 11 | ClOrdID | Unique order identifier |
| 55 | Symbol | Supported symbol |
| 54 | Side | 1=Buy, 2=Sell |
| 38 | OrderQty | Valid quantity |
| 40 | OrdType | 2=Limit |
| 44 | Price | Valid decimal price |
| 60 | TransactTime | Valid transaction timestamp |

Test:

- Valid Buy order.
- Valid Sell order.
- Unsupported symbol.
- Invalid side.
- Invalid quantity.
- Invalid price.
- Missing required field.
- Malformed message.
- Invalid session or message context.

### 8.2 ExecutionReport generation

Validate accepted orders:

| FIX tag | Field | Expected value |
|---|---|---|
| 37 | OrderID | Assigned order identifier |
| 17 | ExecID | Execution report identifier |
| 11 | ClOrdID | Original client order identifier |
| 55 | Symbol | Original symbol |
| 54 | Side | Original side |
| 150 | ExecType | 0=New |
| 39 | OrdStatus | 0=New |
| 38 | OrderQty | Original quantity |
| 14 | CumQty | 0 |
| 151 | LeavesQty | Original quantity |
| 58 | Text | Optional explanatory text |

Validate rejected orders:

| FIX tag | Field | Expected value |
|---|---|---|
| 11 | ClOrdID | Original client order identifier |
| 150 | ExecType | 8=Rejected |
| 39 | OrdStatus | 8=Rejected |
| 103 | OrdRejReason | Appropriate rejection reason when available |
| 58 | Text | Clear rejection description |

Additional checks:

- Correct FIX message type.
- Correct FIX version.
- Required fields present.
- Correct tag values.
- Correct field types and formats.
- Valid message structure.
- Correct response correlation.
- No exposure modification for rejected orders.

Ensure that `CumQty`, `LeavesQty` and `AvgPx` do not imply a market execution despite the immediate exposure update.

### 8.3 FIX message processing

Test:

- Valid NewOrderSingle received by the Acceptor.
- Business validation success.
- Business validation rejection.
- Unsupported message type.
- Invalid message fields.
- Missing mandatory fields.
- Duplicate messages.
- Retransmitted messages.
- Invalid session state.
- Session disconnect during processing.

### 8.4 FIX response correlation

Use `ClOrdID (11)` to correlate requests and responses.

Test:

- Correct response resolves the matching pending request.
- Responses received out of order resolve the correct requests.
- Multiple pending orders are correlated independently.
- Unknown ClOrdID does not resolve another request.
- Duplicate ExecutionReports do not complete or alter state incorrectly.
- A response arriving after timeout is handled safely.
- Pending requests are cleaned up after completion or timeout.
- Cancellation is handled without leaking pending resources.

A timeout must not be interpreted as a business rejection.

A timed-out request may have been accepted by the Acceptor even if the Initiator did not receive the response.

Never automatically resubmit an order after an ambiguous timeout. A deliberate identical retry must preserve the original identifier and content.

### 8.5 FIX session lifecycle

Validate:

- Session creation.
- Successful Logon.
- Logout.
- Unexpected disconnection.
- Reconnection.
- Session recovery.
- Invalid credentials or session configuration.
- Message rejection.
- Application shutdown.
- Graceful session termination.

Distinguish protocol-level failures from business-level order rejections.

---

## 9. Integration Testing

Integration tests validate the interaction between real application components.

Use isolated test configuration and controlled infrastructure.

### 9.1 Application integration

Validate:

- Dependency injection registration.
- Application startup.
- Configuration loading.
- Application service execution.
- Domain service integration.
- Infrastructure adapters.
- Logging and error propagation.
- Graceful shutdown.

### 9.2 FIX Initiator and Acceptor integration

Prefer a real QuickFIX/n Initiator and Acceptor communicating through a local test connection.

The test environment must use:

- Dedicated FIX session identifiers.
- Isolated ports.
- Temporary message stores.
- Temporary log directories.
- Deterministic session configuration.
- Explicit startup and shutdown procedures.

Validate the complete flow:

    OrderGenerator
        |
        | NewOrderSingle
        v
    OrderAccumulator
        |
        | Business validation
        v
    Exposure validation
        |
        +---- Accepted
        |        |
        |        v
        |    ExecutionReport: New
        |
        +---- Rejected
                 |
                 v
             ExecutionReport: Rejected
        |
        v
    OrderGenerator receives response
        |
        v
    Request correlation completed

Test both accepted and rejected orders through actual FIX communication.

### 9.3 Integration failure scenarios

Test:

- Acceptor unavailable during submission.
- Initiator unavailable.
- Connection lost after message transmission.
- Connection lost before response reception.
- Invalid FIX session configuration.
- Session logout during pending requests.
- Duplicate messages after reconnect.
- Response received after client timeout.
- Application restart.
- Message-store recovery behavior.

Do not assume that a successful TCP connection proves successful FIX processing.

---

