# dotnet-project-architecture reference 3

## 11. Persistence and Transaction Boundaries

Choose persistence according to the required consistency and recovery guarantees.

Possible approaches:

- In-memory state.
- Relational databases.
- Document databases.
- Distributed persistence.

Do not introduce persistence infrastructure without a justified requirement.

### In-Memory State

In-memory storage may be acceptable for a limited challenge or prototype.

Explicitly document:

- State loss after restart.
- Lack of shared state across instances.
- Recovery limitations.
- Concurrency guarantees.
- Idempotency limitations.

Do not present an in-memory implementation as production-ready financial persistence.

### Transactions

Define transaction boundaries around operations that must remain consistent.

For financial operations, validation and state mutation must be atomic.

If multiple resources must be updated, evaluate consistency guarantees and failure scenarios.

Avoid pretending that multiple independent operations form a transaction when they do not.

---

## 12. Error Handling and Observability

Separate business failures from technical failures.

Examples:

| Failure | Typical responsibility |
|---|---|
| Invalid input | Request/application validation |
| Business rule violation | Domain/application |
| Database unavailable | Infrastructure |
| FIX session disconnected | Integration |
| Unexpected exception | Application boundary |

Use structured logging with meaningful context.

Include correlation identifiers when useful.

Do not log:

- Passwords.
- Authentication tokens.
- Private keys.
- Sensitive personal data.
- Unnecessary financial information.

Use health checks and metrics when they provide operational value.

Do not introduce a complete observability platform for a small challenge unless required.

---

## 13. Asynchronous Processing and Reliability

Use asynchronous APIs for I/O-bound operations.

Follow these practices:

- Use `async` and `await` consistently.
- Propagate cancellation tokens where appropriate.
- Avoid `.Result` and `.Wait()` in asynchronous workflows.
- Avoid unobserved background tasks.
- Handle cancellation and timeouts explicitly.
- Avoid unnecessary thread creation.
- Define retry behavior according to operation semantics.

A timeout does not necessarily mean an operation failed.

For financial operations, an unknown outcome must be represented explicitly until the system can establish the actual state.

Never automatically retry a non-idempotent operation without an appropriate deduplication strategy.

---

## 14. Testing and Architectural Testability

Architecture must support independent verification of business behavior.

Recommended testing levels:

### Unit Tests

Test:

- Domain entities.
- Value objects.
- Financial calculations.
- Business invariants.
- Validation rules.
- Domain services.

Unit tests should not require databases, network connections or FIX sessions.

### Application Tests

Test:

- Use case orchestration.
- Dependency interactions.
- Business result mapping.
- Failure handling.
- Transaction coordination.

### Integration Tests

Test:

- Persistence implementations.
- FIX adapters.
- HTTP endpoints.
- External communication boundaries.

### End-to-End Tests

Test critical user workflows across the relevant application boundaries.

### Architectural Tests

Where useful, verify:

- Dependency direction.
- Forbidden references.
- Domain independence.
- Layer boundaries.
- Naming and structural conventions.

Do not introduce architectural testing frameworks unless they provide meaningful value.

---

## 15. Security Architecture

Consider security as a cross-cutting architectural requirement.

Evaluate:

- Trust boundaries.
- Authentication.
- Authorization.
- Input validation.
- Transport encryption.
- Secret management.
- Dependency security.
- Secure logging.
- Error disclosure.
- Replay protection.
- Resource exhaustion.
- Financial state mutation controls.

Security controls should be implemented at the correct boundary and must not depend exclusively on frontend validation.

External integration endpoints must not be trusted merely because they are internal network services.

---

## 16. Architecture Decision Records

Document significant decisions when they affect correctness, reliability, security or future maintenance.

An ADR should contain:

- Title.
- Status.
- Context.
- Decision.
- Alternatives considered.
- Consequences.
- Trade-offs.

Recommended decisions to document include:

- Layering strategy.
- Project boundaries.
- Persistence strategy.
- Financial accounting semantics.
- Concurrency strategy.
- Idempotency strategy.
- External integration boundaries.
- Error and recovery behavior.

Avoid creating ADRs for trivial implementation details.

---

## 17. Common Architectural Anti-Patterns

Identify and avoid:

- Business logic inside controllers.
- Domain entities depending on infrastructure.
- Infrastructure libraries referenced throughout the domain.
- God services.
- Anemic abstractions.
- Excessive interfaces.
- Unnecessary repositories.
- Generic repositories that hide important persistence behavior.
- Circular project references.
- Shared mutable global state.
- Duplicate business rules.
- Static service locators.
- Overengineered factories.
- Premature microservices.
- Unnecessary message brokers.
- Distributed architecture without a requirement.
- Excessive abstraction around simple operations.
- Direct exposure of infrastructure models to presentation.
- Unclear transaction boundaries.
- Non-atomic financial updates.

An anti-pattern should be reported only when evidence demonstrates an actual problem.

---

