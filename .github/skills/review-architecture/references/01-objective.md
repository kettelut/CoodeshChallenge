# Architecture Review

## Objective

Conduct a comprehensive, evidence-based architectural review of the Coodesh OrderProcessing solution.

Evaluate whether the implemented architecture is correct, coherent, maintainable and appropriate for the Coodesh challenge.

Identify architectural violations, unnecessary complexity, hidden coupling, reliability risks and opportunities for improvement.

The review must assess the actual repository implementation rather than assuming that the proposed architecture was followed.

## Operating Rules

1. Work as a senior software architect and technical reviewer.
2. Operate in read-only mode.
3. Do not modify source code, project files, configuration or documentation.
4. Do not implement fixes or generate patches.
5. Inspect the actual solution structure and implementation before making conclusions.
6. Support findings with concrete evidence, including file paths, classes, interfaces and dependency relationships.
7. Distinguish confirmed architectural violations from recommendations and potential future risks.
8. Avoid introducing architectural complexity that is not justified by the challenge.
9. Do not require design patterns, abstractions or technologies merely because they are commonly used.
10. Prioritize correctness, simplicity, separation of concerns and financial integrity.

---

## 1. Project Context

The solution contains two .NET 10 applications.

### OrderGenerator

Responsible for:

- Providing the web interface for submitting financial orders.
- Validating user input.
- Creating order submission requests.
- Acting as a FIX Initiator.
- Sending FIX 4.4 NewOrderSingle messages.
- Receiving and presenting ExecutionReports.
- Correlating asynchronous responses with submitted orders.

### OrderAccumulator

Responsible for:

- Acting as a FIX Acceptor.
- Receiving FIX 4.4 NewOrderSingle messages.
- Validating incoming orders.
- Applying financial business rules.
- Maintaining exposure per symbol.
- Enforcing the exposure limit.
- Generating accepted or rejected ExecutionReports.

### Technology constraints

- C#.
- .NET 10.
- ASP.NET Core.
- QuickFIX/n.
- FIX Protocol 4.4.

Do not introduce additional infrastructure unless the actual requirements justify it.

---

## 2. Architectural Objectives

Evaluate whether the architecture achieves the following objectives:

- Clear separation between presentation, application, domain and infrastructure.
- Independence of financial business rules from external frameworks.
- Correct separation between OrderGenerator and OrderAccumulator.
- Well-defined communication through FIX 4.4.
- Explicit ownership of financial state.
- Correct dependency direction.
- High cohesion and low coupling.
- Testability without requiring unnecessary infrastructure.
- Predictable error handling.
- Reliable asynchronous communication.
- Appropriate handling of concurrency and idempotency.
- Maintainability without excessive abstraction.

Assess architectural decisions in the context of a focused challenge implementation rather than a large enterprise platform.

---

## 3. Solution Structure Review

Inspect the solution and evaluate:

- Solution organization.
- Project boundaries.
- Folder structure.
- Naming consistency.
- Project references.
- Dependency graph.
- Shared contracts and models.
- Separation of application-specific concerns.
- Test project organization.
- Configuration organization.

Verify that OrderGenerator and OrderAccumulator have distinct responsibilities.

Check whether shared projects introduce legitimate contracts or create unnecessary coupling between applications.

### Expected logical boundaries

A possible structure may include:

- Presentation.
- Application.
- Domain.
- Infrastructure.
- FIX integration.
- Tests.

This is a logical separation, not a mandatory folder or project structure.

Evaluate whether the implementation uses the simplest structure that preserves clear responsibilities.

Do not recommend splitting projects or assemblies without a concrete benefit.

---

## 4. Dependency Direction

Inspect project references, constructor dependencies, namespaces and service registrations.

Verify that:

- Domain logic does not depend on ASP.NET Core.
- Domain logic does not depend on QuickFIX/n.
- Domain logic does not depend on persistence implementations.
- Application use cases coordinate domain operations without owning transport concerns.
- Infrastructure implements external integrations.
- FIX handlers delegate business processing to application services.
- Presentation components invoke application use cases instead of implementing financial rules.
- Dependency injection is configured at application composition boundaries.

Identify:

- Circular dependencies.
- Infrastructure dependencies leaking into the domain.
- Framework-specific types crossing architectural boundaries.
- Unnecessary shared dependencies.
- Direct instantiation of infrastructure services.
- Service locator patterns.
- Hidden coupling through static state.

For every violation, explain its practical consequences.

---

## 5. Domain Architecture Review

Inspect the financial domain model.

Evaluate whether the implementation correctly represents:

- Financial instruments.
- Order identity.
- Order side.
- Order quantity.
- Order price.
- Order status.
- Exposure.
- Order acceptance and rejection.
- Rejection reasons.

Check whether business invariants are enforced in the appropriate domain or application boundary.

### Financial calculations

Verify the exposure formula:

`Exposure = Buy Notional - Sell Notional`

Where:

`Notional = Price × OrderQty` for each accepted order.

Verify that:

- Monetary values use `decimal`.
- Floating-point arithmetic is not used for financial calculations.
- Exposure is isolated by symbol.
- The absolute exposure limit is enforced.
- The limit is the domain constant `100_000_000.00m`, not external configuration.
- Price satisfies `Price > 0`, `Price < 1000` and `Price % 0.01m == 0` numerically; `1.100` is valid.
- Projected exposure is validated before acceptance.
- Rejected orders do not alter exposure; their original business-rejected results are retained.

Evaluate whether the domain model protects these invariants or relies on external callers to behave correctly.

### Domain independence

Verify that the domain does not require:

- FIX message types.
- ASP.NET Core request models.
- HTTP response types.
- Database-specific entities.
- QuickFIX/n session objects.

Identify any infrastructure-specific concerns embedded in domain entities or business services.

---

## 6. Critical Architectural Decision: Acceptance vs. Execution

Acceptance atomically applies the full signed `Price × OrderQty` to exposure. This challenge-specific accounting simplification is not a market execution, and the challenge does not implement one.

Verify that:

- The established accounting decision is explicit.
- Domain terminology is consistent.
- FIX messages reflect the actual order lifecycle.
- Exposure calculations use full accepted `OrderQty`.
- Accepted reports have `ExecType = 0`, `OrdStatus = 0`, `CumQty = 0` and `LeavesQty = OrderQty`; `AvgPx` does not imply a trade.
- Documentation explains the accounting simplification and its limitations.

Report deviations from this required challenge decision as findings.

---

