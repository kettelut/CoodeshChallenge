# fix44-integration reference 2

## 5. QuickFIX/n Application Lifecycle

Implement the QuickFIX/n application interface according to the installed library version.

Important callbacks include:

| Callback | Responsibility |
|---|---|
| OnCreate | Session created |
| OnLogon | Session established |
| OnLogout | Session terminated |
| ToAdmin | Outgoing administrative message |
| FromAdmin | Incoming administrative message |
| ToApp | Outgoing application message |
| FromApp | Incoming application message |

### Lifecycle Rules

- Do not assume that application startup means the FIX session is connected.
- Track session availability explicitly.
- Handle multiple lifecycle transitions.
- Avoid blocking callbacks with long-running business operations.
- Handle exceptions consistently.
- Avoid silently swallowing protocol failures.
- Dispose session resources during shutdown.
- Do not assume that Logout means every pending order was rejected.

### Session State

Represent relevant states explicitly, such as:

- Disconnected.
- Connecting.
- Connected.
- Disconnecting.
- Faulted.

Ensure that the application does not send orders through an unavailable session.

---

## 6. NewOrderSingle Message

The OrderGenerator sends NewOrderSingle messages using message type:

`D`

The required fields are:

| Field | Tag | Requirement |
|---|---:|---|
| ClOrdID | 11 | Unique client order identifier |
| Symbol | 55 | Supported financial instrument |
| Side | 54 | Buy or Sell |
| OrderQty | 38 | Positive order quantity |
| OrdType | 40 | Limit |
| Price | 44 | Positive order price |
| TransactTime | 60 | Transaction timestamp |

### Expected Values

Side:

- Buy: `1`
- Sell: `2`

Order type:

- Limit: `2`

Supported symbols:

- PETR4.
- VALE3.
- VIIA4.

### Validation Before Sending

Verify:

- ClOrdID is present and unique within its defined scope.
- Symbol is supported.
- Side is valid.
- Quantity is between 1 and 99,999.
- Price is greater than zero.
- Price is below 1,000.
- Price is a numeric multiple of `0.01m` (`1.100` is valid; `1.101` is not).
- Required FIX fields are populated.

The Acceptor must independently validate incoming messages.

Never trust the Initiator's validation as the only protection.

### Message Construction

Use QuickFIX/n generated FIX 4.4 message classes and field types.

Prefer strongly typed fields over manually assembled FIX strings.

Keep message construction inside the FIX integration boundary.

Do not expose QuickFIX/n message objects to the domain layer.

---

## 7. ExecutionReport Message

The OrderAccumulator uses ExecutionReport message type:

`8`

Relevant fields:

| Field | Tag | Purpose |
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

Populate fields according to the actual order lifecycle.

Do not populate execution fields with values that imply a trade occurred when no execution took place.

### Accepted Order

For an accepted but unexecuted order:

- `ExecType = 0` — New.
- `OrdStatus = 0` — New.
- `CumQty = 0`.
- `LeavesQty = OrderQty`.

### Rejected Order

For a rejected order:

- `ExecType = 8` — Rejected.
- `OrdStatus = 8` — Rejected.
- Include an appropriate rejection reason.
- Include a meaningful Text value.
- Preserve the original ClOrdID.

### ExecutionReport Semantics

An ExecutionReport can describe several order lifecycle events.

Do not assume that every ExecutionReport represents a trade execution.

The implementation must distinguish:

- New.
- Rejected.
- Partial fill.
- Fill.
- Canceled.
- Replaced.
- Other supported lifecycle events.

Implement only the events required by the challenge unless additional functionality is explicitly approved.

---

## 8. Acceptance Versus Execution

Upon acceptance, the application atomically adds the full signed `Price × OrderQty` to exposure, subject to the per-symbol domain constant `100_000_000.00m`. This is a challenge-specific accounting simplification, not a market trade; the challenge does not implement market execution.

The accepted FIX report remains `ExecType = 0`, `OrdStatus = 0`, `CumQty = 0` and `LeavesQty = OrderQty`. Do not populate `AvgPx` or other fields as if a trade occurred.

---

## 9. Message Correlation

OrderGenerator must correlate asynchronous ExecutionReports with submitted orders.

Use:

`ClOrdID (11)`

as the primary client order identifier.

### Pending Request Management

Maintain a thread-safe registry of pending requests.

Each pending request should contain only the information necessary to correlate and complete the operation.

Possible implementation:

- `ConcurrentDictionary` keyed by ClOrdID.
- `TaskCompletionSource` for asynchronous completion.
- Explicit timeout handling.
- Cleanup after completion.

### Correlation Rules

- Generate a unique ClOrdID before submission.
- Register the pending request before sending the FIX message.
- Handle responses arriving immediately after transmission.
- Match ExecutionReports using ClOrdID.
- Prevent duplicate completion.
- Remove completed requests.
- Handle session disconnection.
- Avoid indefinite pending requests.

Use `TaskCreationOptions.RunContinuationsAsynchronously` when appropriate to avoid executing arbitrary continuations inline on protocol callback threads.

### Timeout Semantics

A timeout means the response was not received within the expected interval.

It does not prove that the order was rejected.

Possible states include:

- Accepted response received.
- Rejected response received.
- Pending.
- Unknown outcome.
- Communication failure.

Do not translate an unknown outcome into a business rejection.

Do not automatically resubmit an order after timeout.

---

