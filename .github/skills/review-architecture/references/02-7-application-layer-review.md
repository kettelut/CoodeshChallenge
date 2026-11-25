# review-architecture reference 2

## 7. Application Layer Review

Inspect application services, commands, handlers, use cases and interfaces.

Verify that:

- Each use case has a clear responsibility.
- Application services orchestrate domain operations.
- Business rules are not duplicated across application services.
- External transport concerns are not mixed with financial processing.
- Dependencies are injected.
- Asynchronous operations are handled consistently.
- Errors are represented clearly.
- Cancellation is supported where appropriate.
- Application services do not become oversized orchestration classes.

Evaluate whether the application layer provides meaningful boundaries or merely adds unnecessary indirection.

### Order processing workflow

Trace the complete processing path:

1. OrderGenerator receives an order request.
2. Input is validated.
3. A unique client order identifier is generated.
4. A FIX NewOrderSingle is created.
5. OrderAccumulator receives the message.
6. The message is translated into an application request.
7. Business validation is executed.
8. Projected exposure is evaluated.
9. The financial state is updated atomically when appropriate.
10. An ExecutionReport is generated.
11. OrderGenerator correlates the response.
12. The user receives the final status.

Identify missing, duplicated or incorrectly ordered responsibilities.

---

## 8. FIX Integration Architecture

Inspect the QuickFIX/n integration.

Verify that:

- OrderGenerator is the Initiator.
- OrderAccumulator is the Acceptor.
- FIX session management is isolated from financial domain logic.
- Message handlers are thin.
- Message-to-domain translation is explicit.
- Domain-to-ExecutionReport translation is explicit.
- FIX configuration is externalized.
- Session lifecycle is handled consistently.

### NewOrderSingle

Verify the architecture around:

- `ClOrdID (11)`
- `Symbol (55)`
- `Side (54)`
- `OrderQty (38)`
- `OrdType (40)`
- `Price (44)`
- `TransactTime (60)`

### ExecutionReport

Verify the architecture around:

- `OrderID (37)`
- `ExecID (17)`
- `ClOrdID (11)`
- `Symbol (55)`
- `Side (54)`
- `ExecType (150)`
- `OrdStatus (39)`
- `OrderQty (38)`
- `Price (44)`
- `CumQty (14)`
- `LeavesQty (151)`
- `AvgPx (6)`
- `TransactTime (60)`
- `OrdRejReason (103)`
- `Text (58)`

Confirm that FIX-specific types do not leak into the domain model.

### Correlation and asynchronous processing

Evaluate:

- Thread-safe pending request storage.
- Correlation through `ClOrdID`.
- Asynchronous response handling.
- Timeout behavior.
- Pending request cleanup.
- Disconnection behavior.
- Duplicate response handling.
- Message retransmission behavior.

A timeout must represent an unknown outcome unless the application has reliable evidence of rejection.

Automatic resubmission must not create duplicate financial processing.

---

## 9. Exposure Management and Concurrency Architecture

Inspect the component responsible for maintaining exposure.

Verify:

- Exposure is maintained independently for each symbol.
- Updates are atomic.
- Validation and mutation cannot be separated by concurrent requests.
- Identical globally keyed `ClOrdID` and content reproduce the original accepted or business-rejected result without another financial effect.
- Conflicting content is rejected without overwriting the original result or modifying exposure.
- Identifier coordination spans different or invalid symbols and is atomic with exposure updates and result registration; concurrent submissions yield one decision and original results or conflicts.
- Rejected orders cannot partially mutate state.
- Different symbols retain independent exposure but are serialized by the global lock.

Evaluate the approved global-lock strategy and one publication of a complete unpublished
copy-on-write snapshot containing exposure and original results. Different symbols have
independent exposures but are deliberately serialized. Check pre-commit failure isolation,
post-commit unknown outcomes and identical retry, copying costs and absence of network I/O
inside the lock.

Identify inconsistent shared state, race conditions, non-atomic publication, mutable
published snapshots, unclear ownership and idempotency mechanisms that are not thread-safe.

---

## 10. State Management and Persistence

Inspect how the applications manage:

- Orders.
- Exposure.
- Pending requests.
- FIX session state.
- Idempotency identifiers.
- Execution reports.

Determine whether state is:

- In-memory.
- Persisted.
- Reconstructed after restart.
- Shared between application instances.

Evaluate whether the selected strategy is appropriate for the challenge.

### In-memory architecture

If the solution uses in-memory state, verify that:

- State ownership is clear.
- Concurrent access is protected.
- Restart behavior is understood.
- Multi-instance limitations are documented.
- FIX retransmissions do not silently corrupt financial state.

Do not recommend a database, distributed cache or message broker unless a concrete requirement justifies it.

Explain the operational limitations of the selected approach.

---

## 11. Error Handling and Resilience

Evaluate the separation between:

- Business validation errors.
- FIX protocol errors.
- Session and network errors.
- Application errors.
- Infrastructure errors.
- Unexpected system failures.

Verify that:

- Errors are translated at the appropriate boundaries.
- Business rejection does not become an infrastructure failure.
- FIX handlers do not swallow exceptions silently.
- Errors are logged with useful context.
- Sensitive information is not exposed.
- Pending requests are not left indefinitely.
- Unknown outcomes are represented accurately.
- Retry behavior is safe and explicit.

Identify inconsistent exception handling and unnecessary defensive code.

---

## 12. Dependency Injection and Configuration

Inspect:

- Service registrations.
- Service lifetimes.
- Configuration binding.
- Options validation.
- FIX session settings.
- Environment-specific configuration.
- Secret management.
- Application startup and shutdown.

Verify that:

- Singleton services do not capture scoped dependencies incorrectly.
- Shared state has an appropriate lifetime.
- FIX sessions are initialized and disposed consistently.
- Configuration is not hardcoded unnecessarily.
- The challenge exposure limit remains a domain constant, not external configuration.
- Invalid configuration fails clearly during startup.
- Application composition is centralized and understandable.

Identify duplicated or scattered configuration responsibilities.

---

