# Create Execution Plan

## Objective

Analyze the current repository, challenge requirements, architecture and existing technical decisions to produce a detailed, actionable execution plan for implementing the OrderProcessing solution.

The plan must be sufficiently precise for specialized GitHub Copilot agents to execute tasks independently without making conflicting assumptions or duplicating work.

## Operating Rules

- Operate in planning mode.
- Do not modify application source code.
- Do not create or modify project files.
- Do not install dependencies.
- Do not execute implementation tasks.
- Do not assume that planned functionality already exists.
- Inspect the actual repository before defining tasks.
- Distinguish confirmed facts from assumptions and recommendations.
- Identify missing requirements and technical ambiguities.
- Reuse existing project conventions whenever appropriate.
- Keep the implementation proportional to the challenge scope.
- Avoid unnecessary infrastructure and abstractions.

## Project Context

The challenge consists of two C# applications targeting .NET 10.

### OrderGenerator

A web application responsible for:

- Displaying an order submission form.
- Validating user input.
- Creating FIX Protocol 4.4 NewOrderSingle messages.
- Sending orders to OrderAccumulator through a FIX Initiator session.
- Receiving asynchronous ExecutionReports.
- Presenting accepted, rejected, pending or unknown order outcomes.

### OrderAccumulator

An application responsible for:

- Receiving FIX messages through a FIX Acceptor session.
- Validating incoming orders.
- Applying financial business rules.
- Calculating projected exposure.
- Enforcing per-symbol exposure limits.
- Generating FIX ExecutionReports.
- Maintaining the relevant order and exposure state.

### Challenge Requirements

Supported symbols:

- PETR4
- VALE3
- VIIA4

Order fields:

- Symbol.
- Side: Buy or Sell.
- Quantity: positive integer less than 100,000.
- Price: positive decimal, less than 1,000, with increments of 0.01.

Exposure formula:

`Exposure = Sum(accepted Buy Price * OrderQty) - Sum(accepted Sell Price * OrderQty)`

Acceptance atomically adds the full signed notional; this accounting simplification is not a market execution. The challenge does not implement market execution.

Maximum absolute exposure per symbol:

`100_000_000.00m`, a domain constant, not an external configuration value.

Validate price numerically using `Price > 0`, `Price < 1000` and `Price % 0.01m == 0`: `1.100` is valid, `1.101` is not.

Boundary behavior:

- Exposure equal to R$100,000,000.00 is valid.
- Exposure equal to -R$100,000,000.00 is valid.
- Any projected exposure outside these boundaries must be rejected.

FIX behavior:

- OrderGenerator acts as Initiator.
- OrderAccumulator acts as Acceptor.
- OrderGenerator sends NewOrderSingle.
- OrderAccumulator responds with ExecutionReport.
- Accepted order: `ExecType = 0`, `OrdStatus = 0`, `CumQty = 0`, `LeavesQty = OrderQty`.
- Rejected order: `ExecType = 8`, `OrdStatus = 8`.
- Rejected orders must not affect exposure.

QuickFIX/n documentation:

https://quickfixn.org/

## 1. Repository Assessment

Inspect the repository before proposing implementation tasks.

Identify:

- Existing solution files.
- Existing projects.
- Target frameworks.
- Current directory structure.
- Existing source code.
- Existing tests.
- Existing NuGet dependencies.
- Existing FIX configuration.
- Existing documentation.
- Git repository status.
- Existing Copilot agents and instructions.
- Existing architectural decisions.

Determine whether the repository is:

- Empty.
- Partially initialized.
- Already implemented.
- Containing incomplete or conflicting implementations.

Do not assume the repository structure matches any previous proposal.

Identify reusable components and existing work that should be preserved.

## 2. Requirements Analysis

Translate the challenge description into explicit functional and non-functional requirements.

For each requirement, identify:

- Unique identifier.
- Description.
- Responsible application.
- Relevant business or technical layer.
- Dependencies.
- Acceptance criteria.
- Required tests.
- Potential risks.

Separate requirements into:

- Functional.
- Financial.
- FIX protocol.
- Integration.
- Security.
- Operational.
- Documentation.
- Testing.

Identify ambiguous or incomplete requirements.

### Established Decision: Acceptance vs. Execution

Document and plan the required immediate full-notional exposure update as an atomic part of acceptance. It is not a market execution. Preserve `New` with `CumQty = 0` and `LeavesQty = OrderQty`; do not reopen this decision as an implementation choice.

## 3. Architecture Analysis

Use the solution architect agent when actually available and delegation is permitted; otherwise perform the permitted read-only analysis directly and report the missing delegation or independent review. Evaluate:

- Application boundaries.
- Domain responsibilities.
- Application services.
- Infrastructure responsibilities.
- FIX integration boundaries.
- Shared contracts.
- Dependency injection.
- Configuration.
- Error handling.
- State management.
- Testing boundaries.

The architecture should remain pragmatic.

Avoid introducing:

- Microservices beyond the two required applications.
- Message brokers.
- Distributed caches.
- Kubernetes.
- Unnecessary databases.
- Unnecessary abstractions.
- Additional infrastructure without a demonstrated requirement.

The domain must not depend directly on ASP.NET Core, QuickFIX/n or infrastructure-specific implementations.

## 4. Financial Domain Analysis

Use the financial domain specialist agent when available and permitted; otherwise perform the permitted read-only analysis directly and report the limitation. Define:

- Order validation.
- Symbol validation.
- Side validation.
- Quantity validation.
- Price validation.
- Monetary precision.
- Notional calculation.
- Exposure calculation.
- Exposure limit enforcement.
- Atomicity.
- Concurrency behavior.
- Idempotency.
- Rejection behavior.
- State consistency.

Mandatory technical requirements:

- Use `decimal` for financial calculations.
- Never use `double` or `float` for monetary values.
- Enforce exposure limits independently per symbol.
- Ensure exposure validation and modification are atomic.
- Ensure rejected orders do not modify exposure.
- Prevent duplicate processing using `ClOrdID`.
- Ensure concurrent orders cannot bypass exposure limits.
- Preserve accepted and business-rejected original results: identical content reproduces the original outcome; conflicting reuse is rejected without overwriting it or changing exposure.
- Coordinate globally keyed `ClOrdID`, including different or invalid symbols, atomically with result registration and exposure updates; concurrent duplicates produce at most one financial transition.

Evaluate in-memory state versus persistence.

For an in-memory implementation, explicitly document:

- State is lost after application restart.
- Multiple application instances do not share exposure state.
- No distributed consistency is provided.

Do not introduce persistence unless justified by the challenge or architecture.

