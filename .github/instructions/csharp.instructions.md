---
applyTo: "**/*.cs"
---

# C# Development Instructions

## 1. General Standards

Use modern C# features supported by .NET 10 and follow established .NET conventions.

Prioritize:

- Correctness and predictable behavior.
- Readability and maintainability.
- Explicit dependencies.
- Strong typing.
- Separation of concerns.
- Testability.
- Performance where it materially affects the application.

Write production-quality code. Avoid unnecessary abstractions, premature optimization and excessive complexity.

Follow the existing repository conventions unless they conflict with correctness, security or approved architectural decisions.

## 2. Language and Framework

- Target .NET 10.
- Use the latest C# language version supported by the target framework.
- Enable nullable reference types.
- Enable implicit usings where appropriate.
- Use built-in .NET capabilities before introducing external dependencies.
- Prefer standard library APIs over custom implementations when they provide equivalent behavior.
- Use `async` and `await` for asynchronous I/O operations.
- Avoid blocking asynchronous execution with `.Result`, `.Wait()` or equivalent synchronous waits.

Use modern language features when they improve clarity, not merely because they are available.

## 3. Naming Conventions

Follow standard C# naming conventions:

| Element | Convention | Example |
|---|---|---|
| Namespace | PascalCase | `OrderProcessing.Domain` |
| Class | PascalCase | `OrderService` |
| Interface | I + PascalCase | `IOrderService` |
| Method | PascalCase | `CalculateExposure` |
| Property | PascalCase | `OrderQuantity` |
| Public constant | PascalCase | `MaximumExposure` |
| Private field | `_camelCase` | `_orderRepository` |
| Local variable | camelCase | `projectedExposure` |
| Parameter | camelCase | `orderId` |
| Enum | PascalCase | `OrderSide` |
| Enum member | PascalCase | `Buy` |

Use descriptive names that express intent.

Avoid:

- Unclear abbreviations.
- Generic names such as `data`, `obj` or `manager` without meaningful context.
- Boolean names that do not clearly express a condition.
- Names that expose implementation details unnecessarily.

## 4. Types and Nullability

Use strong typing and nullable reference types.

Requirements:

- Avoid `object` when a concrete type is available.
- Avoid unnecessary casts.
- Avoid `dynamic` unless explicitly justified.
- Represent optional values explicitly.
- Validate external input at application boundaries.
- Do not suppress nullable warnings without a documented reason.
- Avoid returning `null` when an explicit result type is more appropriate.

Use `record` for immutable data structures when its semantics are suitable.

Use classes for entities with identity, lifecycle or mutable behavior.

Use `readonly` and immutable structures where they improve correctness.

## 5. Financial Data and Numeric Precision

Financial values must use `decimal`.

Never use `double` or `float` for financial calculations.

This applies to:

- Order prices.
- Exposure values.
- Financial totals.
- Limit comparisons.
- Multiplication of price by quantity.

Example:

`decimal orderValue = price * quantity;`

Do not convert financial values to floating-point types during serialization, calculation or comparison.

Use explicit precision and scale where required by the application contract.

Validate decimal values before processing.

The exposure limit is a fixed domain constant, not external configuration:

`100000000.00m`

The accepted range is inclusive:

`-100000000.00m <= exposure <= 100000000.00m`

Do not use approximate equality for financial comparisons.

Avoid culture-dependent parsing and formatting at integration boundaries. Use invariant or explicitly configured culture handling.

## 6. Validation

Validate input at application boundaries.

The financial order rules are:

- Symbol must be one of `PETR4`, `VALE3` or `VIIA4`.
- Side must be Buy or Sell.
- Quantity must be a positive integer.
- Quantity must be less than 100,000.
- Price must be greater than zero.
- Price must be less than 1,000.
- Price must be a multiple of 0.01.

Use numeric decimal validation `price % 0.01m == 0`, not textual scale: `1.100m` and `1.1000m` are valid; `1.101m` and `1.105m` are not.

Validation must be performed on the server, regardless of client-side validation.

Do not rely exclusively on UI validation or FIX message structure validation.

Use explicit validation results for expected business failures.

Do not use exceptions as the normal mechanism for reporting invalid user input or business rule rejection.

## 7. Classes and Methods

Keep classes focused on a single responsibility.

Methods should:

- Have a clear purpose.
- Avoid excessive length.
- Avoid unrelated operations.
- Use meaningful parameter names.
- Return explicit results.
- Avoid unexpected side effects.

Prefer composition over inheritance unless inheritance represents a genuine domain or technical relationship.

Do not create interfaces for every class automatically.

Introduce abstractions when they provide meaningful benefits, such as:

- Dependency inversion.
- External integration boundaries.
- Test isolation.
- Alternative implementations.
- Clear application contracts.

Avoid static service classes for logic that requires dependencies or state.

## 8. Dependency Injection

Use the built-in .NET dependency injection container.

Requirements:

- Register dependencies in the application composition root.
- Depend on interfaces when an abstraction is justified.
- Avoid service locator patterns.
- Avoid resolving dependencies manually from `IServiceProvider` inside business logic.
- Use constructor injection as the default.
- Keep constructors free of business operations.
- Choose service lifetimes deliberately.

Use:

- Singleton for stateless or safely shared services.
- Scoped for request-oriented services and scoped application operations.
- Transient for lightweight services without shared state.

Do not register mutable financial state as a singleton without explicitly addressing thread safety and lifecycle requirements.

## 9. Asynchronous Programming

Use asynchronous APIs for network, file and database operations.

Requirements:

- Use `async Task` or `async Task<T>` where appropriate.
- Avoid `async void`, except for framework-required event handlers.
- Propagate cancellation tokens through asynchronous call chains.
- Avoid unnecessary `Task.Run` for I/O-bound operations.
- Avoid blocking asynchronous execution.
- Handle cancellation explicitly.
- Do not create unobserved background tasks.

Use `ConfigureAwait(false)` only when appropriate for the application context and existing conventions.

For asynchronous FIX communication, do not assume that sending a message means the order has been accepted.

The response must be correlated with the corresponding `ClOrdID`.

## 10. Error Handling

Handle expected and unexpected errors differently.

Expected failures include:

- Invalid order fields.
- Unsupported symbols.
- Invalid order side.
- Exposure limit exceeded.
- Conflicting reuse of an order identifier.
- Business rule violations.

Represent expected failures with explicit result types or established application conventions.

Unexpected failures include:

- Infrastructure exceptions.
- Unexpected FIX session failures.
- Internal programming errors.
- Unexpected dependency failures.

Requirements:

- Do not swallow exceptions.
- Do not catch `Exception` without a specific handling or propagation strategy.
- Preserve the original exception when rethrowing.
- Do not expose internal exception details to end users.
- Log failures with useful context.
- Never report an order as accepted when processing failed or its outcome is unknown.

Do not convert infrastructure failures into business rejections without an explicit business rule.

## 11. Concurrency and Thread Safety

Financial state must remain consistent under concurrent processing.

Requirements:

- Protect shared mutable state.
- Use atomic operations for exposure validation and updates.
- Serialize operations affecting the same symbol.
- Use one global lock for the in-memory financial state, including identity and all symbols.
- Ensure duplicate detection and exposure updates cannot race.
- Coordinate globally keyed `ClOrdID`, original result registration and the per-symbol exposure transition atomically, even when concurrent requests name different symbols; per-symbol locks alone are insufficient.
- Do not hold locks during unrelated network or I/O operations.
- Avoid unsafe shared collections.

Prepare a complete unpublished snapshot of exposure and original order results; publish it once after all fallible work succeeds, and never mutate the published snapshot. Pre-commit failure leaves state unchanged; after commit, a lost response is unknown and an identical retry returns the committed result. Do not hold the lock during network or unrelated I/O.

Use appropriate synchronization primitives, such as:

- `lock`.
- `SemaphoreSlim`.
- `ConcurrentDictionary<TKey, TValue>`.
- Other synchronization mechanisms justified by the implementation.

A concurrent collection alone does not guarantee atomicity across multiple business operations.

For per-symbol exposure, the validation and state update must be part of the same critical operation.

## 12. Idempotency

Order processing must prevent duplicate financial effects.

Use `ClOrdID (11)` as the primary order correlation and duplicate-detection identifier.

Requirements:

- Detect previously processed identifiers.
- Prevent duplicate exposure updates.
- Coordinate duplicate detection with exposure modification.
- Handle concurrent duplicate submissions safely.
- Define deterministic behavior for retransmitted messages.
- Identical content under the same `ClOrdID` returns or reproduces the original accepted or rejected result; different content is rejected as a conflict without changing original state.
- Concurrent duplicates follow the same rules and produce at most one financial transition.

Do not rely exclusively on FIX session-level duplicate detection.

## 13. Collections and LINQ

Use LINQ when it improves readability.

Avoid:

- Excessive chained transformations.
- Multiple enumerations of expensive sequences.
- Unnecessary materialization with `ToList()` or `ToArray()`.
- Complex LINQ expressions that obscure business rules.
- Allocations that provide no meaningful benefit.

Prefer explicit loops when they make financial calculations, state changes or validation easier to understand and verify.

Use immutable collection types when appropriate.

## 14. Logging and Observability

Use `ILogger<T>` for application logging.

Requirements:

- Use structured logging.
- Include relevant identifiers such as `ClOrdID`.
- Use appropriate log levels.
- Avoid logging secrets or sensitive authentication material.
- Avoid excessive logging of complete FIX messages.
- Ensure rejected orders and infrastructure failures are distinguishable.
- Include enough context to investigate processing failures.

Prefer message templates with named properties.

Example:

`logger.LogInformation("Order {ClOrdID} accepted for symbol {Symbol}", clOrdId, symbol);`

Do not use string interpolation for structured log messages.

## 15. Serialization and Data Contracts

Use explicit contracts at application and integration boundaries.

Requirements:

- Avoid exposing domain entities directly through HTTP APIs.
- Avoid coupling public contracts to infrastructure models.
- Validate deserialized input.
- Handle missing and invalid fields explicitly.
- Use appropriate serialization settings.
- Avoid culture-dependent financial serialization.
- Preserve decimal precision.

FIX tags and QuickFIX/n message types must remain within the FIX integration boundary.

Translate transport-specific data into application-level types before invoking business logic.

## 16. Resource Management

Use `IDisposable` and `IAsyncDisposable` correctly.

Requirements:

- Dispose resources owned by the class.
- Do not dispose dependencies managed by dependency injection unless ownership is explicit.
- Use `using` or `await using` for disposable resources.
- Avoid unmanaged resource leaks.
- Ensure FIX sessions and network resources are shut down correctly.
- Avoid unnecessary resource allocation in high-frequency processing paths.

## 17. Security

Follow secure coding practices.

- Never hardcode credentials or secrets.
- Never commit sensitive configuration.
- Validate all external input.
- Avoid SQL injection and command injection.
- Avoid unsafe deserialization.
- Do not expose stack traces or internal exception details through public responses.
- Avoid logging secrets.
- Use secure cryptographic APIs when cryptography is required.
- Keep dependencies updated.
- Avoid disabling security checks to make integration tests pass.

Do not implement custom cryptographic algorithms.

## 18. Comments and Documentation

Write comments that explain intent, constraints or non-obvious decisions.

Avoid comments that merely repeat the code.

Use XML documentation for public APIs when it adds value or is required by repository conventions.

Document:

- Non-obvious financial rules.
- Concurrency guarantees.
- Idempotency behavior.
- Important integration assumptions.
- Known limitations.
- Workarounds required by external libraries.

Keep comments synchronized with the implementation.

## 19. Testing and Testability

Write code that can be tested independently.

Requirements:

- Keep business logic independent of infrastructure.
- Inject external dependencies.
- Avoid unnecessary static dependencies.
- Avoid hidden global state.
- Make time-dependent behavior controllable where necessary.
- Use deterministic financial calculations.

Tests must cover:

- Valid and invalid orders.
- Quantity and price boundaries.
- Decimal precision.
- Positive and negative exposure.
- Exact exposure limits.
- Exposure rejection.
- Independent symbols.
- Concurrent order processing.
- Duplicate `ClOrdID`.
- Failure without exposure modification.
- FIX message mapping.
- Asynchronous response correlation.

Do not claim tests have passed unless they were actually executed.

## 20. Performance

Prioritize correctness before optimization.

Avoid:

- Unnecessary object allocations in frequently executed paths.
- Repeated expensive calculations.
- Blocking operations.
- Unbounded collections.
- Uncontrolled background tasks.
- Excessive synchronization.

Optimize only when there is evidence of a meaningful performance problem.

Do not sacrifice financial integrity, idempotency or thread safety for performance.

## 21. Code Quality Restrictions

Do not introduce:

- Unnecessary design patterns.
- Excessive inheritance.
- Generic abstractions without real use cases.
- Duplicate validation logic.
- Business rules inside controllers or FIX handlers.
- Infrastructure dependencies inside the Domain layer.
- Floating-point arithmetic for financial values.
- Silent exception handling.
- Unnecessary third-party packages.
- Unjustified global mutable state.

Avoid large classes that combine presentation, business logic, persistence and integration responsibilities.

## 22. Implementation Checklist

Before completing a C# implementation, verify:

- [ ] Code targets .NET 10.
- [ ] Nullable reference types are handled correctly.
- [ ] Naming follows C# conventions.
- [ ] Classes and methods have clear responsibilities.
- [ ] Dependencies are explicit and appropriately injected.
- [ ] Financial calculations use `decimal`.
- [ ] Input validation is performed at the correct boundaries.
- [ ] Business rules are independent of infrastructure.
- [ ] Shared state is thread-safe.
- [ ] Exposure validation and updates are atomic.
- [ ] Duplicate orders cannot modify exposure more than once.
- [ ] FIX-specific types remain inside the integration layer.
- [ ] Asynchronous operations are handled correctly.
- [ ] Cancellation is propagated where appropriate.
- [ ] Errors are handled without hiding failures.
- [ ] Logs contain useful structured context.
- [ ] Sensitive information is not exposed.
- [ ] Automated tests cover relevant behavior.
- [ ] No unnecessary dependencies or abstractions were introduced.

## 23. Instructions for GitHub Copilot

When creating or modifying C# code:

1. Inspect the existing implementation before making changes.
2. Read the relevant architecture and repository instructions.
3. Follow approved technical decisions and implementation plans.
4. Preserve existing conventions where appropriate.
5. Keep changes focused on the requested responsibility.
6. Avoid unrelated refactoring.
7. Do not introduce dependencies without justification.
8. Do not weaken financial or concurrency guarantees.
9. Add or update tests for changed behavior.
10. Build and test when authorized and when the environment permits.
11. Report any assumptions or unresolved ambiguities.
12. Clearly identify files changed and validation actually performed.

If a requirement is ambiguous or conflicts with an approved architecture decision, report the conflict instead of silently inventing business behavior.
