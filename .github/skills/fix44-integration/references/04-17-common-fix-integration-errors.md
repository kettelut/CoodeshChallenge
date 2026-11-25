# fix44-integration reference 4

## 17. Common FIX Integration Errors

Avoid:

- Mixing FIX protocol code with domain logic.
- Constructing raw FIX strings unnecessarily.
- Using incorrect Side values.
- Using incorrect OrdType values.
- Omitting mandatory fields.
- Generating invalid ExecutionReports.
- Treating `ExecType = New` as a trade execution.
- Using ClOrdID inconsistently.
- Registering pending requests after transmission.
- Blocking callback threads.
- Using unsafe shared dictionaries.
- Treating timeout as rejection.
- Automatically resubmitting uncertain orders.
- Ignoring retransmissions.
- Relying exclusively on session-level duplicate handling.
- Resetting sequence numbers without understanding the consequences.
- Assuming session persistence guarantees business recovery.
- Swallowing callback exceptions.
- Logging sensitive session data.
- Claiming integration success based only on mocked tests.

---

## 18. FIX Integration Review Checklist

### Session Configuration

- [ ] FIX.4.4 is configured consistently.
- [ ] Initiator and Acceptor roles are correct.
- [ ] SenderCompID and TargetCompID match.
- [ ] Endpoints are configured correctly.
- [ ] Session lifecycle is handled.
- [ ] Heartbeat settings are appropriate.
- [ ] Sequence number strategy is explicit.
- [ ] Message store behavior is understood.

### NewOrderSingle

- [ ] ClOrdID is unique.
- [ ] Symbol is supported.
- [ ] Side values are correct.
- [ ] OrderQty is valid.
- [ ] OrdType is correct.
- [ ] Price is valid.
- [ ] TransactTime is populated.
- [ ] Mandatory fields are validated.

### ExecutionReport

- [ ] OrderID is populated.
- [ ] ExecID is populated.
- [ ] ClOrdID is preserved.
- [ ] ExecType is correct.
- [ ] OrdStatus is correct.
- [ ] CumQty reflects actual execution.
- [ ] LeavesQty reflects remaining quantity.
- [ ] Rejection reason is appropriate.
- [ ] Execution semantics are consistent.

### Reliability

- [ ] Correlation is thread-safe.
- [ ] Pending requests are cleaned up.
- [ ] Timeouts are handled explicitly.
- [ ] Duplicate messages are safe.
- [ ] Retransmissions are considered.
- [ ] Callback exceptions are handled.
- [ ] Session recovery limitations are documented.

### Architecture

- [ ] FIX types do not leak into the domain.
- [ ] Handlers delegate business processing.
- [ ] Message translation is explicit.
- [ ] Configuration is externalized.
- [ ] Application lifecycle is managed.
- [ ] Financial state is protected independently of FIX session state.

### Testing

- [ ] Message mapping has unit tests.
- [ ] ExecutionReport mapping has unit tests.
- [ ] Correlation has automated tests.
- [ ] Timeout behavior is tested.
- [ ] Duplicate handling is tested.
- [ ] Real session integration is tested when available.
- [ ] End-to-end processing is verified.

---

## 19. Official Documentation

Use the official QuickFIX/n documentation as the primary reference:

- QuickFIX/n: https://quickfixn.org/
- QuickFIX/n documentation: https://quickfixn.org/tutorial/
- FIX Trading Community: https://www.fixtrading.org/

When implementation details depend on the installed QuickFIX/n version, verify the applicable API and documentation.

Do not assume that examples from another version are directly compatible.

Use the FIX 4.4 data dictionary corresponding to the configured protocol version.

---

## 20. Skill Execution Guidelines

When implementing FIX integration:

1. Inspect the existing solution and installed QuickFIX/n version.
2. Identify the Initiator and Acceptor responsibilities.
3. Verify session identifiers and configuration.
4. Implement the application lifecycle.
5. Create NewOrderSingle messages using typed FIX fields.
6. Validate incoming messages.
7. Translate protocol messages into internal application contracts.
8. Delegate business processing to application services.
9. Generate ExecutionReports according to actual order status.
10. Implement thread-safe asynchronous correlation.
11. Handle duplicate messages and retransmissions.
12. Add integration and reliability tests.
13. Document configuration and recovery limitations.

When reviewing FIX integration:

1. Inspect session configuration.
2. Trace message creation and reception.
3. Verify FIX 4.4 field semantics.
4. Verify ExecutionReport status consistency.
5. Inspect correlation and pending request management.
6. Review duplicate handling and idempotency.
7. Examine session lifecycle and recovery.
8. Verify callback thread safety.
9. Inspect security and logging.
10. Report findings with concrete evidence.

The integration is acceptable only when protocol semantics, message mapping, session behavior and business processing boundaries are consistent and independently verifiable.
