---
applyTo: "**/Fix/**,**/FIX/**,**/fix/**,**/*Fix*.cs,**/*FIX*.cs,**/*QuickFix*.cs,**/*ExecutionReport*.cs,config/**/*.cfg,src/OrderGenerator/**/*.cfg,src/OrderAccumulator/**/*.cfg,src/OrderGenerator/appsettings*.json,src/OrderAccumulator/appsettings*.json,**/FIX44.xml,**/FIX4.4.xml"
---

# FIX Protocol 4.4 Instructions

## 1. Purpose and Scope

These instructions define the standards for implementing FIX Protocol 4.4 communication in the OrderProcessing solution using QuickFIX/n.

The solution contains two applications:

- **OrderGenerator:** FIX Initiator responsible for submitting financial orders.
- **OrderAccumulator:** FIX Acceptor responsible for receiving, validating and processing orders.

The FIX integration must provide reliable message exchange, correct protocol semantics, asynchronous response correlation and protection against duplicate financial processing.

FIX infrastructure must remain isolated from domain and application business rules.

The path patterns cover the planned `Infrastructure/Fix` directories and other explicitly named FIX directories, FIX-named C# files, `.cfg` files in the planned FIX `config` directory and applications, application-root appsettings files, and FIX 4.4 dictionaries. Generic FIX-related files elsewhere may require manual attachment or additional targeted patterns when the layout exists.

## 2. Protocol and Library

Use:

- FIX Protocol 4.4.
- QuickFIX/n.
- C#.
- .NET 10.

Official QuickFIX/n documentation:

https://quickfixn.org/

Requirements:

- Use QuickFIX/n for FIX session management and message handling.
- Follow FIX 4.4 message definitions.
- Use supported QuickFIX/n APIs.
- Avoid manually implementing FIX message parsing or transport.
- Keep protocol-specific dependencies inside the integration layer.
- Do not introduce another FIX engine without explicit justification.

Do not assume that examples from another FIX version are automatically compatible with FIX 4.4.

## 3. Application Roles

### 3.1 OrderGenerator — Initiator

OrderGenerator must act as the FIX Initiator.

Responsibilities:

- Establish the FIX session with OrderAccumulator.
- Submit `NewOrderSingle` messages.
- Generate unique client order identifiers.
- Maintain pending order correlation.
- Receive asynchronous `ExecutionReport` messages.
- Translate FIX responses into application-level results.
- Handle session disconnections and reconnections.
- Expose order status to the presentation layer.

The web presentation layer must not reference QuickFIX/n types directly.

### 3.2 OrderAccumulator — Acceptor

OrderAccumulator must act as the FIX Acceptor.

Responsibilities:

- Accept incoming FIX sessions.
- Receive `NewOrderSingle` messages.
- Validate protocol-level message requirements.
- Translate messages into application commands.
- Delegate financial processing to application services.
- Generate accepted or rejected `ExecutionReport` messages.
- Handle session lifecycle events.
- Prevent duplicate financial processing.

FIX callbacks must not contain exposure calculations or independent business rule implementations.

## 4. FIX Session Configuration

Use QuickFIX/n configuration files or equivalent strongly typed configuration.

A session configuration must define the appropriate settings, including:

- `ConnectionType`.
- `BeginString`.
- `SenderCompID`.
- `TargetCompID`.
- `SocketConnectHost` for Initiator.
- `SocketConnectPort` for Initiator.
- `SocketAcceptPort` for Acceptor.
- `HeartBtInt`.
- `FileStorePath` or the approved persistence mechanism.
- `FileLogPath`.
- `StartTime`.
- `EndTime`.
- `ResetOnLogon`.
- `ResetOnLogout`.
- `ResetOnDisconnect`.
- `ReconnectInterval`.

Typical roles:

| Application | ConnectionType |
|---|---|
| OrderGenerator | initiator |
| OrderAccumulator | acceptor |

Requirements:

- Sender and Target CompIDs must match between applications.
- Initiator connection settings must point to the Acceptor endpoint.
- Session identifiers must be consistent.
- Configuration must be externalized.
- Do not hardcode credentials or environment-specific endpoints.
- Validate required settings during startup.
- Use separate configuration for different environments.

Do not enable sequence-number resets indiscriminately. Reset behavior must be consistent with the intended session recovery strategy.

## 5. Session Lifecycle

Implement the appropriate QuickFIX/n application callbacks.

Relevant callbacks include:

- `OnCreate`.
- `OnLogon`.
- `OnLogout`.
- `ToAdmin`.
- `FromAdmin`.
- `ToApp`.
- `FromApp`.

Responsibilities:

- Initialize session-specific state.
- Track logon and logout events.
- Handle administrative messages.
- Process application messages.
- Record relevant lifecycle information.
- Release resources during shutdown.

Do not perform lengthy business operations inside lifecycle callbacks.

Do not assume that a session is available before successful logon.

Handle session disconnections without losing application-level order correlation.

## 6. NewOrderSingle Message

OrderGenerator must submit orders using FIX message:

`NewOrderSingle (35=D)`

Required fields:

| Field | FIX Tag | Description |
|---|---:|---|
| ClOrdID | 11 | Unique client order identifier |
| Symbol | 55 | Financial instrument |
| Side | 54 | Buy or Sell |
| OrderQty | 38 | Order quantity |
| OrdType | 40 | Order type |
| Price | 44 | Limit price |
| TransactTime | 60 | Transaction timestamp |

Supported values:

| Field | Value | Meaning |
|---|---:|---|
| Side (54) | 1 | Buy |
| Side (54) | 2 | Sell |
| OrdType (40) | 2 | Limit |

Requirements:

- Generate a unique `ClOrdID` for each new order.
- Use a valid FIX timestamp.
- Validate required fields before sending.
- Preserve decimal price precision.
- Validate the numeric decimal value as a multiple of `0.01m`, not its textual scale: `1.100` and `1.1000` are valid; `1.101` and `1.105` are not.
- Do not use floating-point values for financial fields.
- Ensure message values match the approved domain contract.
- Do not reuse a `ClOrdID` for a different order.

The FIX integration layer must translate application-level order data into the correct FIX fields.

## 7. ExecutionReport Message

OrderAccumulator must respond using:

`ExecutionReport (35=8)`

The ExecutionReport must identify the original order and accurately represent its processing status.

Relevant fields:

| Field | FIX Tag | Description |
|---|---:|---|
| OrderID | 37 | Order identifier |
| ExecID | 17 | Execution report identifier |
| ClOrdID | 11 | Original client order identifier |
| Symbol | 55 | Financial instrument |
| Side | 54 | Buy or Sell |
| ExecType | 150 | Execution report type |
| OrdStatus | 39 | Current order status |
| OrderQty | 38 | Original order quantity |
| Price | 44 | Order price |
| CumQty | 14 | Cumulative executed quantity |
| LeavesQty | 151 | Remaining quantity |
| AvgPx | 6 | Average execution price |
| TransactTime | 60 | Transaction timestamp |
| OrdRejReason | 103 | Rejection reason |
| Text | 58 | Additional information |

Populate fields according to the actual order lifecycle.

Do not populate execution-related fields with invented values.

## 8. Accepted Orders

For an accepted new order, use:

- `ExecType (150) = 0` — New.
- `OrdStatus (39) = 0` — New.

For a newly accepted but unexecuted order:

- `CumQty (14) = 0`.
- `LeavesQty (151) = OrderQty (38)`.

The report must contain the original `ClOrdID`.

The accepted report must only be sent after the business operation has successfully completed according to the approved processing model.

Important:

`ExecType=New` means the order has been acknowledged as new. It does not mean that a trade has been executed.

Acceptance immediately accounts for the full `Price × OrderQty` in challenge exposure, atomically with the decision. It does not represent a market execution: do not report a trade, set `CumQty` to `OrderQty`, or set `LeavesQty` to zero because exposure changed.

## 9. Rejected Orders

For a rejected order, use:

- `ExecType (150) = 8` — Rejected.
- `OrdStatus (39) = 8` — Rejected.

Include when appropriate:

- `OrdRejReason (103)`.
- `Text (58)`.

Rejection reasons must correspond to the actual business or protocol validation failure.

Examples:

- Unsupported symbol.
- Invalid quantity.
- Invalid price.
- Exposure limit exceeded.
- Invalid order data.

Requirements:

- Preserve the original `ClOrdID`.
- Return a meaningful rejection reason.
- Do not modify exposure.
- Do not register the order as accepted.
- Do not report a successful order when processing failed.

A business rejection is a valid application outcome, not necessarily a FIX session failure.

## 10. FIX Message Validation

Validate incoming messages before invoking business processing.

Check:

- Required fields.
- Field data types.
- Valid enum values.
- Symbol format.
- Side.
- Quantity.
- Price.
- Order type.
- Client order identifier.
- Timestamp validity where required.

Distinguish protocol-level validation from business validation.

Protocol-level validation includes malformed messages, missing required FIX fields and invalid field representations.

Business validation includes unsupported symbols, invalid financial values and exposure limit violations.

For protocol-valid orders, return a rejected ExecutionReport for business rejection. Malformed or protocol-incompatible messages use the applicable FIX 4.4 Session Reject or Business Message Reject mechanism, including engine-level validation where appropriate; do not generate an order ExecutionReport for every protocol failure. Timeout, disconnect and session unavailability are communication failures or unknown outcomes, not business rejections.

Do not duplicate the complete business validation implementation inside FIX handlers.

Translate validated protocol data into application-level commands and let the financial domain enforce business invariants.

## 11. Message Handling Architecture

Keep FIX handlers thin.

Recommended processing flow:

1. Receive the FIX message.
2. Validate protocol structure.
3. Extract required fields.
4. Translate the message into an application command.
5. Invoke the appropriate application service.
6. Receive an explicit business result.
7. Map the result to an ExecutionReport.
8. Send the response through the appropriate FIX session.

FIX handlers must not:

- Calculate exposure directly.
- Implement financial limit rules.
- Manage domain entities independently.
- Access persistence directly without an approved abstraction.
- Contain presentation logic.
- Modify financial state outside the application/domain processing flow.

Do not expose QuickFIX/n message types to the Domain layer.

## 12. Message Correlation

Use `ClOrdID (11)` as the primary correlation identifier.

OrderGenerator must maintain a thread-safe mapping between submitted orders and pending responses.

Requirements:

- Support multiple pending orders.
- Correlate each response with the correct request.
- Handle responses arriving out of order.
- Complete the correct pending operation.
- Remove completed entries safely.
- Prevent correlation state from growing indefinitely.
- Preserve enough information to reconcile unknown outcomes.

Use appropriate concurrency-safe structures.

A `ConcurrentDictionary` may be suitable for pending correlation, but the complete lifecycle must still be coordinated correctly.

Do not correlate responses using symbol, price or quantity alone.

## 13. Asynchronous Response Handling

FIX responses may arrive asynchronously.

Order submission must not assume that the response is available immediately after sending a message.

The application must distinguish:

- Pending.
- Accepted.
- Rejected.
- Unknown outcome.
- Technical failure.

Use asynchronous application contracts where appropriate.

Avoid blocking request threads while waiting indefinitely for ExecutionReports.

Define a configurable response timeout.

A timeout means that the response was not received within the configured period. It does not prove that the order was rejected.

Do not automatically resubmit an order after a timeout.

A delayed ExecutionReport may still arrive after the timeout.

The implementation must define how late responses are correlated and reconciled.

## 14. Duplicate Messages and Idempotency

FIX session-level duplicate handling does not replace application-level idempotency.

Requirements:

- Detect previously processed `ClOrdID` values.
- Prevent duplicate exposure updates.
- Handle retransmitted messages.
- Handle concurrent duplicate delivery.
- Return or reproduce the original accepted or rejected result for identical order content under the same `ClOrdID`; reject different content as an identifier conflict without changing the original order or exposure.
- Coordinate globally keyed identity, original-result registration and per-symbol exposure validation/update atomically, including concurrent same-ID requests for different symbols. Per-symbol locks alone are insufficient.
- Concurrent duplicates produce at most one financial transition and otherwise observe the original result or a conflict according to content.

Consider FIX retransmission indicators and sequence-number behavior where applicable.

Do not assume that every repeated message is a new business order.

Do not process a duplicate request as a new financial operation.

Reproduce the original processing outcome according to the application contract and FIX semantics; do not treat an identical retransmission as a new order.

## 15. Session Recovery and Reliability

The integration must handle session interruptions safely.

Consider:

- Unexpected disconnects.
- Initiator reconnection.
- Session logon recovery.
- Sequence-number synchronization.
- Message retransmission.
- Duplicate delivery.
- Pending application orders.
- Delayed ExecutionReports.

Use QuickFIX/n session recovery capabilities appropriately.

Do not assume that session reconnection guarantees application-level order reconciliation.

Do not reset sequence numbers automatically after every disconnect.

Document limitations related to session persistence and application state recovery.

## 16. FIX Storage and Logging

Configure message storage and logging according to the approved operational strategy.

Consider:

- Message store path.
- Session log path.
- File permissions.
- Disk capacity.
- Retention.
- Environment separation.
- Sensitive information exposure.

Requirements:

- Avoid logging credentials.
- Avoid exposing authentication material.
- Avoid excessive logging of sensitive business information.
- Use correlation identifiers in application logs.
- Distinguish administrative messages from application messages.
- Avoid logging complete messages unless explicitly justified.

Use structured application logging through `ILogger<T>`.

QuickFIX/n protocol logging and application logging must have clearly defined responsibilities.

## 17. Thread Safety

QuickFIX/n callbacks may execute in different execution contexts.

Requirements:

- Do not assume callbacks are executed sequentially.
- Protect shared mutable state.
- Use thread-safe correlation structures.
- Ensure application services enforce financial atomicity.
- Avoid holding locks while sending network messages.
- Avoid deadlocks between callback execution and application tasks.
- Avoid blocking asynchronous processing unnecessarily.

Thread safety must be considered independently at the FIX integration and financial domain levels.

A thread-safe FIX handler does not automatically guarantee atomic financial processing.

## 18. Error Handling

Distinguish between the following failure categories:

### Protocol Errors

Examples:

- Missing required fields.
- Invalid field representation.
- Unsupported message type.
- Invalid FIX message structure.

### Business Rejections

Examples:

- Unsupported symbol.
- Invalid quantity.
- Invalid price.
- Exposure limit exceeded.

### Session Errors

Examples:

- Failed logon.
- Unexpected logout.
- Sequence-number mismatch.
- Session configuration error.

### Infrastructure Errors

Examples:

- Network failure.
- Socket failure.
- Storage failure.
- Unexpected internal exception.

Requirements:

- Do not convert every exception into a business rejection.
- Do not report acceptance after an infrastructure failure.
- Preserve enough context for diagnostics.
- Avoid exposing internal exception details to external FIX participants.
- Handle recoverable and unrecoverable failures differently.
- Do not silently discard messages.

## 19. Financial Integrity

FIX integration must preserve all financial domain guarantees.

Requirements:

- Use `decimal` for price and financial values.
- Preserve quantity as an integer.
- Do not round invalid price values silently.
- Do not modify exposure before successful validation.
- Do not modify exposure after rejection.
- Ensure duplicate messages cannot create duplicate financial effects.
- Ensure per-symbol exposure is isolated.
- Preserve atomicity under concurrent message processing.

The maximum absolute exposure per symbol is:

`100000000.00m`, a fixed domain constant, not an external setting.

The boundary is inclusive.

FIX message delivery and financial state commitment must be coordinated through the application layer.

## 20. FIX Session Security

Consider the security of FIX communication.

Requirements:

- Keep credentials outside source control.
- Restrict network access to authorized peers.
- Use secure transport when required by the deployment environment.
- Validate session identifiers.
- Avoid accepting unauthorized sessions.
- Protect message logs.
- Avoid exposing sensitive configuration.
- Apply network and resource limits where appropriate.

Do not assume that FIX session identifiers alone provide authentication or transport encryption.

Do not implement custom cryptographic mechanisms.

## 21. Testing Requirements

FIX integration must be testable independently of the financial domain.

### 21.1 Message Mapping Tests

Test:

- NewOrderSingle field mapping.
- Buy and Sell side mapping.
- Limit order type mapping.
- Decimal price preservation.
- Numeric multiple validation, including valid `1.100` and `1.1000` and invalid `1.101` and `1.105`.
- Quantity mapping.
- ExecutionReport accepted mapping.
- ExecutionReport rejected mapping.
- Rejection reason mapping.
- `ClOrdID` preservation.

### 21.2 Session Tests

Test:

- Initiator startup.
- Acceptor startup.
- Successful logon.
- Logout.
- Unexpected disconnect.
- Reconnection.
- Invalid configuration.
- Session shutdown.

### 21.3 Correlation Tests

Test:

- Multiple pending orders.
- Responses arriving out of order.
- Correct `ClOrdID` matching.
- Timeout behavior.
- Late ExecutionReport handling.
- Pending request cleanup.
- Concurrent response processing.

### 21.4 Duplicate Processing Tests

Test:

- Repeated `ClOrdID`.
- Retransmitted messages.
- Concurrent duplicate messages.
- Duplicate accepted order.
- Duplicate rejected order.
- Identical content reproduces the original result; conflicting content is rejected without changing original state.
- Concurrent same-ID requests with different symbols cannot both create financial effects.
- No duplicate exposure modification.

### 21.5 Integration Tests

Where possible, use real QuickFIX/n Initiator and Acceptor sessions in integration tests.

Clearly distinguish:

- Unit tests with mocked FIX dependencies.
- Message mapping tests.
- Session integration tests.
- End-to-end financial processing tests.

Do not claim full FIX interoperability based only on mocked messages.

## 22. Implementation Restrictions

Do not:

- Implement FIX parsing manually.
- Put business rules inside FIX callbacks.
- Put exposure calculations inside message handlers.
- Expose QuickFIX/n types to the Domain layer.
- Assume message transmission means order acceptance.
- Treat New acknowledgment as execution.
- Automatically resubmit timed-out orders.
- Ignore duplicate messages.
- Reset sequence numbers without justification.
- Swallow session errors.
- Log credentials.
- Modify exposure before successful validation.
- Use floating-point arithmetic for financial values.
- Introduce another FIX engine without justification.

## 23. FIX Compliance Checklist

Before considering FIX integration complete, verify:

- [ ] FIX Protocol 4.4 is used.
- [ ] QuickFIX/n is used for session management.
- [ ] OrderGenerator acts as Initiator.
- [ ] OrderAccumulator acts as Acceptor.
- [ ] Session configuration is externalized.
- [ ] SenderCompID and TargetCompID are consistent.
- [ ] Session lifecycle events are handled.
- [ ] NewOrderSingle fields are mapped correctly.
- [ ] ExecutionReport fields are mapped correctly.
- [ ] Accepted status uses `ExecType=0` and `OrdStatus=0`.
- [ ] Accepted unexecuted orders use `CumQty=0` and `LeavesQty=OrderQty` while their full notional is accounted immediately.
- [ ] Rejected status uses `ExecType=8` and `OrdStatus=8`.
- [ ] `ClOrdID` is preserved and used for correlation.
- [ ] Asynchronous responses are handled correctly.
- [ ] Out-of-order responses are supported.
- [ ] Timeouts are treated as unknown outcomes.
- [ ] Duplicate messages cannot modify exposure more than once.
- [ ] Session recovery behavior is defined.
- [ ] FIX callbacks remain independent of financial business logic.
- [ ] Financial calculations use `decimal`.
- [ ] Rejected orders do not modify exposure.
- [ ] Logging does not expose secrets.
- [ ] Automated tests cover message mapping and session behavior.
- [ ] Known integration limitations are documented.

## 24. Instructions for GitHub Copilot

When implementing or modifying FIX integration:

1. Inspect the existing QuickFIX/n implementation and session configuration.
2. Read the architecture, C#, .NET and financial domain instructions.
3. Verify QuickFIX/n API usage against the installed package version.
4. Follow FIX Protocol 4.4 semantics.
5. Keep FIX-specific code isolated from business logic.
6. Preserve `ClOrdID` across the complete request-response lifecycle.
7. Handle asynchronous responses and timeouts explicitly.
8. Prevent duplicate financial processing.
9. Do not invent unsupported FIX lifecycle behavior.
10. Add tests for message mapping, correlation and session behavior.
11. Do not modify financial rules without explicit authorization.
12. Report configuration assumptions, interoperability limitations and tests actually executed.

If a FIX requirement is ambiguous or conflicts with the approved financial domain model, stop and report the conflict instead of silently implementing an interpretation.
