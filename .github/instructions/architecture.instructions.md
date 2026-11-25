---
applyTo: "**/*.cs,**/*.csproj,**/*.sln,**/*.slnx"
---

# Architecture Instructions

## 1. Architectural Objectives

The OrderProcessing solution must provide a clear, maintainable and testable architecture for two .NET 10 applications:

- **OrderGenerator:** Web application responsible for collecting financial orders, validating user input and submitting orders through FIX Protocol 4.4.
- **OrderAccumulator:** Application responsible for receiving FIX orders, validating business rules, managing exposure per financial instrument and returning FIX ExecutionReports.

The architecture must prioritize:

- Separation of concerns.
- Explicit application and domain boundaries.
- Financial calculation correctness.
- Atomic exposure management.
- Idempotent order processing.
- Reliable FIX communication.
- Testability and maintainability.
- Minimal operational complexity.

Do not introduce architectural complexity without a concrete requirement.

## 2. Architectural Style

Use a pragmatic layered architecture with clear separation between:

- Presentation.
- Application.
- Domain.
- Infrastructure.

The Domain layer must remain independent of ASP.NET Core, QuickFIX/n, persistence frameworks and external infrastructure.

The Application layer coordinates use cases and defines the contracts required by the domain and infrastructure.

The Infrastructure layer implements external integrations, including FIX communication and persistence when necessary.

The Presentation layer handles HTTP requests, user interaction and response formatting.

Do not force every project to implement unnecessary abstractions or layers. Apply the architecture according to the responsibilities of each application.

## 3. Application Responsibilities

### 3.1 OrderGenerator

OrderGenerator is the FIX Initiator and web application.

Its responsibilities are:

- Present the order submission interface.
- Collect Symbol, Side, Quantity and Price.
- Validate user input on the server.
- Invoke the appropriate application use case.
- Construct and submit FIX NewOrderSingle messages through the FIX integration layer.
- Correlate asynchronous ExecutionReports with submitted orders.
- Present pending, accepted, rejected and unknown outcomes.
- Handle communication failures without misrepresenting order status.

The presentation layer must not:

- Reference QuickFIX/n types directly.
- Calculate or maintain financial exposure.
- Implement FIX session lifecycle management.
- Contain business rules.
- Directly manage FIX message correlation.

Prefer ASP.NET Core Razor Pages unless the approved architecture specifies another presentation approach.

### 3.2 OrderAccumulator

OrderAccumulator is the FIX Acceptor and financial order-processing application.

Its responsibilities are:

- Accept FIX 4.4 connections.
- Receive and validate NewOrderSingle messages.
- Translate FIX messages into application commands.
- Execute business validation.
- Calculate the projected exposure for the requested symbol.
- Enforce the exposure limit atomically.
- Prevent duplicate order processing.
- Generate the appropriate ExecutionReport.
- Maintain the order and exposure state according to the approved persistence strategy.

FIX message handlers must remain thin adapters. They must delegate business operations to application services and must not implement financial rules themselves.

## 4. Domain Boundaries

The Domain layer must represent financial concepts independently of transport and presentation concerns.

Recommended domain concepts include:

- Order.
- OrderSide.
- OrderStatus.
- Instrument or Symbol.
- Exposure.
- ExposureLimit.
- OrderValidationResult.
- OrderAcceptanceResult.

Use names that accurately represent the actual business behavior. Do not introduce entities or value objects without a meaningful responsibility.

Domain models must not depend on:

- QuickFIX/n.
- ASP.NET Core.
- HTTP request or response types.
- Database-specific entities.
- Infrastructure implementations.
- External service clients.

Use domain-specific types and enums instead of passing FIX tags or transport-specific values throughout the application.

## 5. Application Layer

The Application layer coordinates business use cases.

Typical use cases include:

- SubmitOrder.
- ProcessIncomingOrder.
- ValidateOrder.
- CalculateProjectedExposure.
- AcceptOrder.
- RejectOrder.
- RetrieveOrderStatus.

The exact use cases must follow the approved implementation plan.

Application services must:

- Coordinate domain behavior.
- Enforce the correct execution sequence.
- Define transaction or atomicity boundaries.
- Depend on abstractions rather than infrastructure implementations.
- Return explicit results.
- Avoid leaking transport-specific exceptions or types.

Do not introduce MediatR, CQRS frameworks or additional libraries unless they provide measurable value for this challenge.

## 6. Financial Rules and Precision

Financial calculations must use `decimal`.

Never use `double` or `float` for:

- Order prices.
- Exposure values.
- Financial totals.
- Financial comparisons.
- Limit validation.

The exposure formula is:

`Exposure = Sum(Buy Price × accepted OrderQty) - Sum(Sell Price × accepted OrderQty)`

The absolute exposure limit is:

`100000000.00m`, a fixed domain constant, not an `IOptions<T>`, appsettings, environment variable or user setting.

The following conditions must hold:

- Exposure of exactly `+100000000.00` is allowed.
- Exposure of exactly `-100000000.00` is allowed.
- Exposure above the positive limit is rejected.
- Exposure below the negative limit is rejected.
- Rejected orders must not modify exposure.
- Each symbol must have an independent exposure balance.

Use explicit decimal precision and avoid implicit conversions that could compromise financial accuracy.

### 6.1 Acceptance Versus Execution

The challenge specifies an exposure formula based on executed quantity but also requires accepted orders to return `ExecType=New`.

These are different FIX lifecycle events.

Each accepted order immediately contributes its full `Price × OrderQty` to exposure, atomically with acceptance. This challenge-specific accounting simplification is not an actual market execution: `New` remains unexecuted and no trade is reported.

## 7. Exposure Consistency and Concurrency

Exposure validation and state modification must be atomic per symbol.

The implementation must prevent concurrent requests from independently reading the same exposure and both being accepted when their combined effect exceeds the limit.

Requirements:

- Serialize exposure-changing operations for the same symbol.
- Serialize different symbols under the same global lock for the selected in-memory design.
- Calculate projected exposure before committing the operation.
- Update exposure only after successful validation.
- Ensure rejection leaves the previous exposure unchanged.
- Avoid locks held during unrelated network or I/O operations.
- Coordinate globally keyed `ClOrdID` identity and original-result registration with the per-symbol exposure transition in the same atomic consistency boundary, including conflicting requests for different symbols. Per-symbol locks alone cannot protect global identity.

Use one global lock and prepare an unpublished copy-on-write snapshot containing all exposure and original order results. Publish the completed snapshot with one state reference assignment; never mutate it after publication. A pre-commit failure leaves all state unchanged; a failure after publication may produce an unknown response, and an identical retry reproduces the committed result. Snapshot copying and serialization across symbols are accepted single-instance trade-offs.

## 8. Idempotency

Order processing must be idempotent.

Use FIX `ClOrdID (11)` as the primary client order correlation and duplicate-detection identifier.

Requirements:

- A previously processed `ClOrdID` must not modify exposure again.
- FIX retransmissions must not cause duplicate business processing.
- Identical content under the same `ClOrdID` returns or reproduces the original accepted or rejected result without another financial effect.
- Different content under the same `ClOrdID` is rejected as an identifier conflict without changing the original order or exposure.
- Concurrent submissions follow the same rules, with at most one financial transition.
- Idempotency checks and exposure updates must be coordinated atomically.
- Duplicate detection must account for the possibility of concurrent message delivery.

Do not assume that FIX session-level duplicate detection alone guarantees application-level idempotency.

## 9. FIX Integration Boundary

QuickFIX/n must be isolated within the Infrastructure or dedicated FIX integration layer.

The integration boundary must translate between FIX messages and application-level commands and results.

### 9.1 OrderGenerator

OrderGenerator acts as FIX Initiator.

It sends `NewOrderSingle (35=D)` with the required fields:

- `ClOrdID (11)`.
- `Symbol (55)`.
- `Side (54)`.
- `OrderQty (38)`.
- `OrdType (40)`.
- `Price (44)`.
- `TransactTime (60)`.

Use:

- Buy: `Side (54) = 1`.
- Sell: `Side (54) = 2`.
- Limit order: `OrdType (40) = 2`.

### 9.2 OrderAccumulator

OrderAccumulator acts as FIX Acceptor.

It receives and processes NewOrderSingle messages and returns ExecutionReports.

Accepted order acknowledgment:

- `ExecType (150) = 0` — New.
- `OrdStatus (39) = 0` — New.

Rejected order:

- `ExecType (150) = 8` — Rejected.
- `OrdStatus (39) = 8` — Rejected.

ExecutionReports must contain the appropriate identifiers and order information, including:

- `OrderID (37)`.
- `ExecID (17)`.
- `ClOrdID (11)`.
- `Symbol (55)`.
- `Side (54)`.
- `OrderQty (38)`.
- `Price (44)`.
- `CumQty (14)`.
- `LeavesQty (151)`.
- `AvgPx (6)`.
- `TransactTime (60)`.

For a newly accepted but unexecuted order:

- `CumQty (14) = 0`.
- `LeavesQty (151) = OrderQty (38)`.

Rejected reports should include `OrdRejReason (103)` and `Text (58)` when appropriate.

Do not return a successful ExecutionReport before the business operation has been successfully committed.

## 10. Asynchronous Communication

FIX responses may arrive asynchronously.

OrderGenerator must correlate responses using `ClOrdID (11)`.

The implementation must:

- Use thread-safe correlation state.
- Support multiple pending orders.
- Handle responses arriving out of order.
- Complete the appropriate pending operation.
- Remove completed correlation entries safely.
- Apply a configurable timeout.
- Handle disconnects and session failures.

A timeout means the outcome is unknown, not necessarily rejected.

Do not automatically resubmit an order after a timeout. A retry may create a duplicate financial operation.

The user interface must distinguish:

- Pending.
- Accepted.
- Rejected.
- Unknown outcome.

## 11. Persistence and State Management

Choose the simplest persistence strategy that satisfies the challenge requirements.

An in-memory implementation may be appropriate if the solution is explicitly scoped to a single running instance and does not require state recovery.

If in-memory state is selected, document:

- Exposure is reset when the application restarts.
- Orders are not durable across restarts unless separately persisted.
- Multiple OrderAccumulator instances do not share exposure state.
- The solution does not provide distributed consistency.

Do not introduce a database, message broker, cache or distributed lock without a concrete requirement.

If persistence is required, define the transaction boundaries for order registration, idempotency and exposure updates.

## 12. Dependency Injection and Configuration

Use built-in .NET dependency injection unless an additional container is justified.

Register dependencies through the composition root of each application.

Configuration must be externalized where appropriate, including:

- FIX BeginString.
- SenderCompID.
- TargetCompID.
- Connection host and port.
- Session schedule.
- Heartbeat interval.
- Reconnection settings.
- Message persistence settings.
- Order processing timeout.

The challenge exposure limit is a fixed domain constant and must not be externalized.

Do not hardcode credentials, passwords, private keys or environment-specific secrets.

Use strongly typed configuration objects and validate critical settings during application startup.

## 13. Error Handling

Keep error handling explicit and consistent.

Distinguish between:

- Invalid user input.
- Business rule rejection.
- FIX protocol validation failure.
- FIX session failure.
- Network failure.
- Internal application failure.
- Unknown order outcome.

Business rejections must be represented as valid business results, not as unhandled infrastructure exceptions.

Infrastructure failures must not be reported as business rejections unless the business contract explicitly defines that behavior.

Never modify exposure when order processing fails.

Avoid swallowing exceptions or returning generic success responses after failures.

## 14. Project Dependencies

Dependencies must point inward toward stable business abstractions.

Recommended dependency direction:

- Presentation → Application.
- Infrastructure → Application and Domain.
- Application → Domain.
- Domain → no external application or infrastructure layer.

The Domain layer must not reference the Infrastructure or Presentation projects.

The Application layer must not reference QuickFIX/n or ASP.NET Core presentation types.

Shared contracts may be introduced when genuinely needed by both applications. Avoid coupling the applications through shared implementation details.

## 15. Testing Requirements

Architecture decisions must support automated testing.

The solution must provide appropriate coverage for:

- Domain calculations.
- Financial validation.
- Exposure boundaries.
- Independent symbol exposure.
- Concurrent order processing.
- Idempotency.
- Rejected order integrity.
- Application use cases.
- FIX message mapping.
- FIX response correlation.
- Session lifecycle behavior.
- HTTP input validation.
- End-to-end order submission.

Unit tests must not require an active FIX server or external infrastructure.

Integration tests must clearly identify which dependencies are real and which are mocked.

Do not claim real FIX interoperability based only on mocked message tests.

## 16. Security and Operational Concerns

Architecture and implementation must consider:

- Secure handling of FIX session credentials.
- Restricted network exposure.
- HTTPS for network-accessible environments and production; isolated local development may use HTTP loopback with non-sensitive test data. Never transmit credentials or sensitive information over HTTP.
- Server-side validation.
- Protection against malformed and oversized messages.
- Safe error responses.
- Sensitive data redaction in logs.
- Dependency vulnerability management.
- Resource limits and connection lifecycle management.

Do not log secrets or sensitive authentication material.

Logs should include correlation identifiers when appropriate, especially `ClOrdID`, without exposing credentials.

## 17. Scope Control

The solution is a focused technical challenge, not an enterprise trading platform.

Do not introduce the following without explicit justification:

- Microservices.
- Kafka or RabbitMQ.
- Redis.
- Kubernetes.
- Distributed event sourcing.
- Unnecessary CQRS frameworks.
- Complex generic repository abstractions.
- Excessive domain event infrastructure.
- Multiple persistence technologies.
- Unnecessary third-party dependencies.

Prefer a small, coherent solution that demonstrates correctness, financial integrity, FIX interoperability and maintainable code.

## 18. Architectural Decision Records

Document significant architectural decisions when they affect correctness, maintainability or operational behavior.

At minimum, record decisions concerning:

- Acceptance versus execution semantics.
- Exposure calculation and update strategy.
- Concurrency and atomicity.
- Idempotency and duplicate handling.
- Persistence and restart behavior.
- FIX session configuration.
- Application communication and response correlation.

Each decision should include:

- Context.
- Decision.
- Alternatives considered.
- Consequences.
- Known limitations.

Do not create documentation for trivial implementation choices.

## 19. Architecture Compliance Checklist

Before considering an architectural change complete, verify:

- [ ] OrderGenerator and OrderAccumulator have clearly separated responsibilities.
- [ ] OrderGenerator is the FIX Initiator.
- [ ] OrderAccumulator is the FIX Acceptor.
- [ ] Domain logic is independent of QuickFIX/n and ASP.NET Core.
- [ ] Application services coordinate use cases without depending on transport-specific types.
- [ ] Financial calculations use `decimal`.
- [ ] Exposure is isolated by symbol.
- [ ] Exposure validation and update are atomic.
- [ ] Rejected orders do not affect exposure.
- [ ] Duplicate orders cannot modify exposure more than once.
- [ ] FIX message mapping follows FIX 4.4 semantics.
- [ ] Accepted and rejected order states are represented correctly.
- [ ] Acceptance and execution semantics are explicitly documented.
- [ ] Asynchronous FIX responses are correlated using `ClOrdID`.
- [ ] Timeouts are not incorrectly interpreted as rejections.
- [ ] Persistence limitations are documented.
- [ ] Dependencies follow the approved architectural boundaries.
- [ ] Automated tests can validate the critical business rules.
- [ ] No unnecessary infrastructure or architectural complexity has been introduced.

## 20. Instructions for GitHub Copilot

When implementing or modifying code:

1. Inspect the existing solution before making architectural decisions.
2. Read the relevant project and repository instructions.
3. Follow approved architecture decisions and implementation plans.
4. Preserve existing conventions unless there is a documented reason to change them.
5. Keep changes focused on the assigned responsibility.
6. Do not modify unrelated projects or files.
7. Do not introduce new dependencies without justification.
8. Do not duplicate existing functionality.
9. Do not weaken financial, concurrency or idempotency guarantees.
10. Add or update tests for behavior changes.
11. Document relevant limitations and assumptions.
12. Report files changed, decisions made, tests executed and remaining risks.

If requirements conflict with these instructions or with an approved architectural decision, stop and report the conflict instead of silently choosing an interpretation.
