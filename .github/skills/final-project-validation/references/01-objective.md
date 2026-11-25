# Final Project Validation

## Objective

Conduct a comprehensive, evidence-based final validation of the Coodesh OrderProcessing solution before submission.

Coordinate the specialized quality agents to independently assess the implementation, consolidate their findings, identify unresolved risks and determine whether the project is ready for delivery.

This is a validation and review activity, not an implementation task.

## Operating Rules

1. Act as the read-only project orchestrator.
2. Do not modify source code, project files, configuration, tests or documentation.
3. Do not implement fixes or directly resolve findings.
4. Delegate specialized validation when appropriate agents and delegation are actually available.
5. Inspect the repository and actual implementation before reaching conclusions.
6. Require evidence for every significant finding and validation result.
7. Never claim that a build, test, integration or security check passed unless it was actually executed or independently verified.
8. Distinguish verified results from assumptions, limitations and unverified behavior.
9. Do not approve the project solely because it compiles.
10. Do not expand the challenge scope or introduce unnecessary architectural requirements.

If a required validation cannot be performed, classify it as `BLOCKED` or `NOT VERIFIED` and explain why.

When delegation is unavailable, perform permitted read-only checks directly where feasible, identify them as direct rather than independent agent reviews, and report checks requiring unavailable capabilities as blocked or not verified. Agent files or names do not prove runtime availability.

---

## 1. Project Context

The solution consists of two .NET 10 applications:

- **OrderGenerator:** ASP.NET Core web application responsible for collecting and submitting financial orders through FIX Protocol 4.4.
- **OrderAccumulator:** FIX Acceptor responsible for receiving orders, validating business rules, managing exposure and returning ExecutionReports.

The integration uses QuickFIX/n.

### Business requirements

Supported symbols:

- PETR4
- VALE3
- VIIA4

Order fields:

- Symbol
- Side: Buy or Sell
- Quantity: positive integer, maximum 99,999
- Price: positive decimal, strictly below 1,000 and numerically divisible by `0.01m`; `1.100` is valid.

Exposure formula:

`Exposure = Sum(accepted Buy Price × OrderQty) - Sum(accepted Sell Price × OrderQty)`

Maximum absolute exposure per symbol:

`|Exposure| <= 100_000_000.00m`, a domain constant rather than external configuration.

Expected FIX responses:

- Accepted order: `ExecutionReport`, `ExecType = 0`, `OrdStatus = 0`, `CumQty = 0`, `LeavesQty = OrderQty`.
- Rejected order: `ExecutionReport`, `ExecType = 8`, `OrdStatus = 8`.

Rejected orders must not change exposure.

### Established accounting decision

Verify that acceptance atomically applies the full signed notional to exposure. This challenge-specific accounting simplification is not a market execution, and the challenge does not implement one. A `New` report remains unexecuted with `CumQty = 0` and `LeavesQty = OrderQty`; report any deviation.

---

## 2. Repository and Solution Inventory

Inspect the complete repository and verify:

- Solution and project structure.
- Application boundaries.
- Target framework and SDK configuration.
- Project references and dependency direction.
- NuGet dependencies and versions.
- Configuration files and environment-specific settings.
- FIX session configuration.
- Test projects and test organization.
- Documentation and README.
- Git configuration and `.gitignore`.
- Presence of generated files, secrets or unnecessary artifacts.

Identify missing, duplicated, obsolete or inconsistent components.

Verify that the repository contains the expected two applications and that responsibilities are assigned correctly.

Do not assume that the intended architecture matches the actual implementation.

---

## 3. Specialized Agent Assignments

Delegate independent reviews to the following agents only when available and permitted; otherwise use the direct read-only fallback and report its limitations.

### 3.1 Build Validator

Agent: `build-validator`

Validate:

- .NET SDK availability and compatibility.
- Solution and project configuration.
- NuGet restoration.
- Debug and Release builds.
- Compiler warnings and errors.
- Test project discovery.
- Test execution.
- Dependency consistency.
- Build reproducibility.

Execute appropriate commands when authorized:

- `dotnet --info`
- `dotnet restore`
- `dotnet build --no-restore`
- `dotnet build --configuration Release`
- `dotnet test`

Report the exact commands executed, their outcomes and any relevant warnings.

### 3.2 Code Reviewer

Agent: `code-reviewer`

Review:

- Architecture and separation of concerns.
- Domain and application boundaries.
- SOLID principles applied pragmatically.
- C# and .NET 10 implementation quality.
- Error handling and asynchronous execution.
- Dependency injection.
- Financial correctness.
- Concurrency and idempotency.
- FIX integration design.
- Maintainability and unnecessary complexity.

Classify findings as:

- Critical
- High
- Medium
- Low

Every finding must include evidence and affected files or components.

### 3.3 Security Reviewer

Agent: `security-reviewer`

Review:

- Input validation and injection risks.
- ASP.NET Core security.
- CSRF, XSS and CORS configuration.
- HTTP transport security.
- FIX session security.
- Credentials and secrets management.
- Sensitive information in logs.
- Dependency vulnerabilities.
- Replay and duplicate-message handling.
- Resource exhaustion and denial-of-service risks.
- Financial integrity and unauthorized state changes.

Classify findings by severity and explain practical impact.

### 3.4 Test Engineer

Agent: `test-engineer`

Validate:

- Unit test quality and coverage of business rules.
- Integration tests.
- FIX message processing.
- End-to-end order submission.
- Financial calculations and precision.
- Exposure boundaries.
- Concurrent order processing.
- Idempotency.
- Rejected order behavior.
- Asynchronous response correlation.
- Error and timeout handling.

Identify missing scenarios and distinguish mocked tests from tests using real FIX sessions.

### 3.5 FIX Protocol Specialist

Agent: `fix-protocol-specialist`

Validate:

- FIX 4.4 compliance.
- QuickFIX/n configuration.
- Initiator and Acceptor roles.
- Session lifecycle.
- NewOrderSingle structure.
- ExecutionReport structure.
- Message correlation.
- Rejection semantics.
- Sequence number and retransmission behavior.
- Duplicate message handling.
- Session recovery and persistence.
- Asynchronous processing.

Verify actual field mappings rather than relying exclusively on documentation.

### 3.6 Financial Domain Specialist

Agent: `financial-domain-specialist`

Validate:

- Order validation rules.
- Exposure formula.
- Decimal precision.
- Buy and Sell calculations.
- Per-symbol exposure isolation.
- Exposure limit boundaries.
- Atomicity of exposure updates.
- Idempotency.
- Rejected order integrity.
- Acceptance-versus-execution semantics.

Pay particular attention to race conditions and financial inconsistencies.

### 3.7 Documentation Specialist

Agent: `documentation-specialist`

Review:

- README completeness and accuracy.
- Installation instructions.
- Configuration instructions.
- Application startup instructions.
- FIX session setup.
- Order submission workflow.
- Business rules.
- Architecture description.
- Testing instructions.
- Known limitations.
- Coodesh attribution.

Confirm that documented commands and behavior match the actual implementation.

---

