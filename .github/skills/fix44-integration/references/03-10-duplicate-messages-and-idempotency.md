# fix44-integration reference 3

## 10. Duplicate Messages and Idempotency

FIX supports message retransmission and sequence recovery.

An application must not assume that every received message represents a new business operation.

### Required Protection

Use stable identifiers to prevent duplicate financial processing.

For this challenge, `ClOrdID` is the globally keyed business order identifier in the accumulator.

Verify that:

- Identical content returns or reproduces the original accepted or rejected result, with no second financial effect.
- Different content with the same identifier is rejected as a conflict without overwriting the original or changing exposure.
- Idempotency and result registration are atomic with acceptance and exposure updates, including concurrent reuse across different or invalid symbols.
- Retransmitted messages do not update exposure twice.
- Concurrent duplicates cannot both create new financial operations; identical duplicates reproduce the original outcome.
- Duplicate detection is thread-safe.
- Conflicting reuse of ClOrdID is handled explicitly.
- Duplicate ExecutionReports do not complete pending requests incorrectly.
- Message-level and business-level duplicate detection are not confused.

### Message-Level Versus Business-Level Duplicates

Message-level duplicate handling concerns FIX session sequence numbers and retransmission.

Business-level idempotency concerns whether the same logical order is processed more than once.

Both concerns must be considered.

Do not rely exclusively on QuickFIX/n session behavior to protect financial state.

---

## 11. Message Validation and Error Handling

Separate protocol validation from business validation.

### Protocol-Level Validation

Examples:

- Missing mandatory fields.
- Invalid field types.
- Invalid enumerations.
- Malformed message structure.
- Incorrect message direction.
- Unsupported message type.

### Business-Level Validation

Examples:

- Unsupported symbol.
- Invalid quantity.
- Invalid price.
- Invalid price precision.
- Exposure limit exceeded.
- Conflicting reuse of a business identifier.

### Technical Failures

Examples:

- Session unavailable.
- Network interruption.
- Message persistence failure.
- Unexpected application exception.
- Processing timeout.

### Error Mapping

Use the appropriate FIX response mechanism.

Do not generate a business ExecutionReport for every protocol or infrastructure failure.

Use session-level Reject or BusinessMessageReject when appropriate to the actual failure and FIX 4.4 semantics.

Use ExecutionReport with rejected status for business-level order rejection.

Do not expose internal stack traces or sensitive infrastructure details through Text fields.

---

## 12. Session Reliability and Recovery

Evaluate reliability across connection failures and process restarts.

### Required Considerations

- Sequence number persistence.
- Message store behavior.
- ResendRequest processing.
- Duplicate message detection.
- Session recovery.
- Heartbeat monitoring.
- Reconnection behavior.
- Pending request reconciliation.
- Application shutdown.
- Unexpected process termination.

### Message Persistence

Understand the difference between:

- In-memory message storage.
- File-based message storage.
- Durable business order state.
- Durable financial exposure state.

FIX session persistence does not automatically provide durable financial accounting.

Do not assume that restoring sequence numbers restores business state.

### Restart Behavior

If business state is in-memory, document:

- Exposure loss after restart.
- Loss of processed-order identifiers.
- Loss of order history.
- Pending request recovery limitations.
- Potential reconciliation requirements.

Do not claim full recovery when only FIX session sequence numbers are persisted.

---

## 13. Asynchronous Processing and Thread Safety

QuickFIX/n callbacks may execute in protocol-managed threads.

Treat callback execution as concurrent.

### Rules

- Avoid blocking callback threads.
- Avoid synchronous waits on asynchronous tasks.
- Do not perform long-running operations directly inside callbacks.
- Protect shared mutable state.
- Use thread-safe correlation structures.
- Handle exceptions at callback boundaries.
- Ensure background processing failures are observed.
- Avoid uncontrolled fire-and-forget operations.
- Preserve message ordering where business rules require it.

### Application Processing

Translate incoming messages into internal requests and delegate processing to application services.

If processing is asynchronous, define how the callback waits for or receives the result without blocking protocol infrastructure unnecessarily.

Do not assume that changing a callback to `async void` makes the integration safe.

---

## 14. Configuration and Security

FIX configuration is security-sensitive.

Verify:

- CompIDs are configured correctly.
- Credentials are externalized.
- Session endpoints are restricted.
- Sensitive values are not logged.
- Network communication is protected appropriately.
- File store permissions are restricted.
- Log directories are protected.
- Message input is validated.
- Reconnection behavior is controlled.
- Session access is limited to authorized participants.

Do not commit real credentials or private certificates.

For production environments, evaluate transport encryption and peer authentication according to the deployment requirements.

---

## 15. Logging and Observability

Log relevant FIX session and application events.

Useful events include:

- Session created.
- Logon received.
- Logon completed.
- Logout received.
- Session disconnected.
- NewOrderSingle sent.
- NewOrderSingle received.
- ExecutionReport sent.
- ExecutionReport received.
- Message rejected.
- Processing failed.
- Correlation timeout.
- Duplicate message detected.

Include correlation identifiers when useful.

Avoid logging:

- Passwords.
- Authentication secrets.
- Private keys.
- Sensitive personal information.
- Unnecessary full financial payloads.

Distinguish session logs from business audit records.

A FIX message log is not automatically a complete business audit trail.

---

## 16. Testing Strategy

FIX integration requires multiple levels of testing.

### Unit Tests

Test:

- FIX-to-application mapping.
- Application-to-FIX mapping.
- Field validation.
- Side conversion.
- Order type conversion.
- ExecutionReport construction.
- Rejection mapping.
- ClOrdID generation.
- Correlation behavior.

These tests should not require a live FIX session.

### Session Integration Tests

Use actual QuickFIX/n Initiator and Acceptor instances when practical.

Test:

- Successful Logon.
- Session disconnection.
- Reconnection.
- NewOrderSingle transmission.
- NewOrderSingle reception.
- Accepted ExecutionReport.
- Rejected ExecutionReport.
- Sequence number handling.
- Session shutdown.

### Correlation Tests

Test:

- Correct ClOrdID matching.
- Out-of-order responses.
- Duplicate responses.
- Immediate response after sending.
- Timeout.
- Session loss while waiting.
- Pending request cleanup.

### Reliability Tests

Test:

- Retransmitted messages.
- Identical and conflicting business identifiers, including concurrent submissions and preserved rejected results.
- Concurrent incoming orders.
- Unexpected callback exceptions.
- Application restart behavior.
- Message persistence and recovery where configured.

### End-to-End Tests

Verify the complete flow:

1. Submit an order through OrderGenerator.
2. Send NewOrderSingle.
3. Receive the message in OrderAccumulator.
4. Execute financial validation.
5. Atomically update exposure by the full signed notional when accepted; leave it unchanged when rejected.
6. Generate ExecutionReport.
7. Receive the response in OrderGenerator.
8. Correlate using ClOrdID.
9. Present the final result.

Distinguish mocked protocol tests from actual QuickFIX/n session tests.

---

