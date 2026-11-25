---
applyTo: "**/*.md"
---

# Documentation Instructions

## 1. General Principles

All project documentation must be accurate, consistent, maintainable and aligned with the actual implementation.

Documentation must describe what the solution does, how it works, how to configure it and how to execute it.

Prioritize:

- Technical accuracy.
- Clear and concise language.
- Consistent terminology.
- Reproducible instructions.
- Explicit assumptions and limitations.
- Practical information for developers and reviewers.

Do not document features that have not been implemented as if they were available.

Do not claim that commands, tests, integrations or configurations work unless they have been verified.

## 2. Language and Writing Style

The primary language for project documentation is English.

Use professional technical English suitable for software engineers and technical reviewers.

Requirements:

- Use consistent technical terminology.
- Prefer direct and unambiguous sentences.
- Avoid marketing language and exaggerated claims.
- Avoid unnecessary introductory explanations.
- Explain technical decisions when they affect implementation or operation.
- Use correct names for .NET, C#, FIX Protocol and QuickFIX/n concepts.

Keep documentation focused on the challenge requirements.

## 3. Markdown Formatting

Use standard GitHub Flavored Markdown.

Follow these conventions:

- Use one H1 heading per document.
- Organize sections with hierarchical headings.
- Use bullet lists for related information.
- Use numbered lists for sequential procedures.
- Use tables for structured comparisons and configuration references.
- Use inline code formatting for identifiers, commands, paths and configuration keys.
- Use fenced code blocks only when displaying actual source code, commands or configuration examples.
- Use descriptive link text.
- Keep line lengths and paragraphs readable.
- Avoid excessive nested lists.
- Avoid decorative formatting that reduces readability.

Do not create malformed Markdown or unnecessarily complex formatting.

When editing existing Markdown files, preserve valid content and formatting outside the requested scope.

## 4. README Requirements

The root `README.md` must be written in English and provide all information necessary to understand, install, configure, run and evaluate the solution.

It must include:

### 4.1 Project Title

Use a clear project title that identifies the OrderProcessing challenge.

### 4.2 Description

Provide a concise one-sentence description explaining the purpose of the solution.

### 4.3 Technologies

List the technologies, languages, frameworks and relevant libraries actually used.

Examples may include:

- C#.
- .NET 10.
- ASP.NET Core.
- FIX Protocol 4.4.
- QuickFIX/n.
- Automated testing frameworks.

Do not list technologies that are not present in the implementation.

### 4.4 Architecture Overview

Explain the responsibilities of:

- OrderGenerator.
- OrderAccumulator.
- FIX Initiator.
- FIX Acceptor.
- Domain and application components.
- FIX integration components.

Include a diagram only when it materially improves understanding and accurately represents the implementation.

### 4.5 Prerequisites

Document the actual prerequisites, such as:

- .NET 10 SDK.
- Git.
- Required runtime dependencies.
- Network requirements for FIX communication.

Do not invent prerequisites.

### 4.6 Installation

Provide reproducible instructions for:

- Cloning the repository.
- Restoring dependencies.
- Building the solution.
- Configuring the applications.

Commands must match the actual solution and project paths.

### 4.7 Configuration

Explain relevant configuration settings, including FIX session parameters.

Document:

- Setting name.
- Purpose.
- Expected format.
- Example value when safe.
- Whether the setting is required.

Never include real credentials, passwords, private keys or production connection details.

Use placeholders for sensitive values.

### 4.8 Execution

Explain how to start OrderAccumulator and OrderGenerator.

Describe the required startup order and the expected behavior when the FIX session connects.

Include actual commands and project paths verified against the repository.

### 4.9 Order Submission

Explain how to submit an order and describe the supported fields:

- Symbol.
- Side.
- Quantity.
- Price.

Document valid symbols and input restrictions.

Explain how accepted and rejected responses are presented.

### 4.10 Business Rules

Document:

- Supported symbols: `PETR4`, `VALE3` and `VIIA4`.
- Buy and Sell operations.
- Quantity restrictions.
- Price restrictions.
- Financial precision.
- Exposure formula.
- Maximum absolute exposure of R$100,000,000 per symbol.
- Acceptance and rejection behavior.
- Duplicate order handling.

Describe the limit as the fixed domain constant `100000000.00m`. Price validity is a numeric `decimal` multiple of `0.01m`, not a textual decimal-place count; `1.100` and `1.1000` are valid.

The documented rules must match the actual implementation.

### 4.11 FIX Protocol

Describe the FIX 4.4 communication flow.

Document the relevant messages:

- `NewOrderSingle (35=D)`.
- `ExecutionReport (35=8)`.

Explain the meaning of the accepted and rejected statuses:

- Accepted: `ExecType (150) = 0`, `OrdStatus (39) = 0`, `CumQty (14) = 0` and `LeavesQty (151) = OrderQty (38)`.
- Rejected: `ExecType (150) = 8`, `OrdStatus (39) = 8`.

Document the role of `ClOrdID (11)` in order correlation and idempotency.

Identical content with the same `ClOrdID` returns or reproduces the original accepted or rejected result without another financial effect; different content is rejected as a conflict. Document atomic coordination of global identity, original result and per-symbol exposure, including concurrent requests for different symbols.

Document the selected global lock and single copy-on-write snapshot publication, its serialization and memory costs, and the difference between pre-commit failures and unknown outcomes after commit. Distinguish business-rejected ExecutionReports from applicable FIX protocol rejects and communication failures.

HTTP loopback is permitted only for isolated local development using non-sensitive test data; network-accessible environments and production require HTTPS. Credentials and sensitive information must never be transmitted over HTTP.

Do not describe an accepted New order as an executed trade.

### 4.12 Testing

Explain how to execute the automated tests.

Document the actual test coverage and distinguish:

- Unit tests.
- Integration tests.
- FIX protocol tests.
- End-to-end tests.

Clearly identify mocked dependencies and external infrastructure requirements.

### 4.13 Limitations and Assumptions

Document relevant implementation limitations, including:

- Acceptance versus execution semantics.
- Persistence strategy.
- In-memory state behavior.
- Restart behavior.
- Single-instance limitations.
- FIX session recovery limitations.
- Any unsupported functionality.

Do not hide limitations that affect financial consistency or operational behavior.

### 4.14 Attribution

When publishing the challenge in a personal GitHub repository, include the following attribution:

This is a challenge by [Coodesh](https://coodesh.com/).

## 5. Architecture Documentation

Architecture documentation must reflect the actual project structure and dependency relationships.

Document:

- Application responsibilities.
- Layer boundaries.
- Domain concepts.
- Application use cases.
- Infrastructure integrations.
- FIX communication flow.
- State management.
- Concurrency strategy.
- Idempotency strategy.
- Configuration boundaries.

Do not document proposed architecture as implemented architecture.

If the implementation differs from the original design, update the documentation to reflect the final solution and explain relevant deviations.

## 6. Technical Decision Documentation

Document significant architectural and technical decisions when they affect correctness, maintainability or operation.

Relevant decisions include:

- Acceptance versus execution semantics.
- Exposure calculation and update strategy.
- Concurrency and atomicity.
- Idempotency and duplicate detection.
- Persistence and restart behavior.
- FIX session configuration.
- Asynchronous response correlation.
- Error handling and timeout behavior.

Each decision should include:

1. Context.
2. Decision.
3. Alternatives considered.
4. Consequences.
5. Known limitations.

Avoid creating decision records for trivial implementation details.

## 7. Configuration Documentation

Configuration documentation must be synchronized with the actual application configuration.

For every important setting, explain:

- Configuration key.
- Purpose.
- Expected value or format.
- Default value, if one exists.
- Whether it is mandatory.
- Environment-specific considerations.

Distinguish between:

- Application configuration.
- FIX session configuration.
- Environment variables.
- Secrets.

Never publish sensitive values.

Do not assume that a configuration key exists without inspecting the source code and configuration files.

## 8. FIX Documentation Accuracy

FIX documentation must follow FIX Protocol 4.4 semantics.

Use correct message names, message types and tag identifiers.

Relevant NewOrderSingle fields include:

| Field | FIX Tag | Description |
|---|---:|---|
| ClOrdID | 11 | Client order identifier |
| Symbol | 55 | Financial instrument |
| Side | 54 | Buy or Sell |
| OrderQty | 38 | Order quantity |
| OrdType | 40 | Order type |
| Price | 44 | Limit price |
| TransactTime | 60 | Transaction timestamp |

Relevant ExecutionReport fields include:

| Field | FIX Tag | Description |
|---|---:|---|
| OrderID | 37 | Order identifier |
| ExecID | 17 | Execution report identifier |
| ClOrdID | 11 | Client order identifier |
| ExecType | 150 | Execution report type |
| OrdStatus | 39 | Current order status |
| CumQty | 14 | Cumulative executed quantity |
| LeavesQty | 151 | Remaining quantity |
| AvgPx | 6 | Average execution price |
| OrdRejReason | 103 | Order rejection reason |
| Text | 58 | Additional explanatory text |

Do not confuse:

- Order acceptance.
- Order rejection.
- Order execution.
- Order cancellation.
- Order replacement.

Only document lifecycle events supported by the implementation.

## 9. Financial Documentation Accuracy

Financial documentation must be mathematically consistent with the code.

The exposure formula is:

`Exposure = Sum(Buy Price × accepted OrderQty) - Sum(Sell Price × accepted OrderQty)`

The maximum absolute exposure is:

`R$100,000,000.00`

Document that the boundary is inclusive.

Financial examples must use decimal values and must not imply floating-point calculations.

State the challenge decision: acceptance immediately accounts for the full order notional atomically; this is not a real market execution and does not change FIX execution quantities.

## 10. Code Documentation

Documentation inside source code must explain non-obvious behavior and important constraints.

Prefer meaningful names and self-explanatory code over excessive comments.

Use XML documentation for public APIs when appropriate.

Document:

- Public contracts.
- Important domain invariants.
- Non-obvious calculations.
- Concurrency guarantees.
- Idempotency behavior.
- External integration assumptions.
- Workarounds and known limitations.

Do not leave outdated comments or TODO statements that imply completed functionality.

## 11. Diagrams and Visual Documentation

Use diagrams only when they improve technical understanding.

Suitable diagrams include:

- High-level architecture.
- FIX communication sequence.
- Order processing lifecycle.
- Exposure validation flow.
- Application dependency relationships.

Diagrams must:

- Match the actual implementation.
- Use consistent component names.
- Show relevant boundaries.
- Avoid unnecessary complexity.
- Remain maintainable in the repository.

Mermaid diagrams may be used when supported by GitHub Markdown rendering.

## 12. Documentation Maintenance

Whenever code or configuration changes affect documented behavior:

1. Identify the impacted documentation.
2. Verify the actual implementation.
3. Update the relevant sections.
4. Remove obsolete instructions.
5. Verify commands and paths.
6. Check Markdown formatting.
7. Ensure examples remain consistent with the code.

Do not rewrite unrelated documentation unnecessarily.

Avoid duplicated information across multiple files. Prefer one authoritative location and link to it from other documents.

## 13. Documentation Security

Before publishing documentation, verify that it does not contain:

- FIX credentials.
- Passwords.
- API keys.
- Private keys.
- Personal access tokens.
- Internal production endpoints.
- Sensitive customer information.
- Confidential operational details.

Use safe placeholders and clearly identify example values.

Do not expose internal infrastructure details unless they are necessary and authorized for the challenge.

## 14. Documentation Quality Checklist

Before considering documentation complete, verify:

- [ ] Documentation is written in English.
- [ ] Project descriptions match the implementation.
- [ ] Technologies and dependencies are accurate.
- [ ] Architecture descriptions reflect actual project boundaries.
- [ ] Installation commands are correct.
- [ ] Configuration instructions are complete.
- [ ] Execution instructions are reproducible.
- [ ] FIX message descriptions follow FIX 4.4 semantics.
- [ ] Financial formulas and limits are correct.
- [ ] Acceptance and execution semantics are explicit.
- [ ] Idempotency and concurrency behavior are documented.
- [ ] Tests and execution commands are accurate.
- [ ] Known limitations are disclosed.
- [ ] No secrets or sensitive information are present.
- [ ] Markdown formatting is valid.
- [ ] No obsolete or contradictory instructions remain.
- [ ] Coodesh attribution is included when applicable.

## 15. Instructions for GitHub Copilot

When creating or modifying documentation:

1. Inspect the implementation before documenting behavior.
2. Read the relevant architecture and repository instructions.
3. Use the actual project structure and configuration.
4. Do not invent features, commands, paths or dependencies.
5. Do not claim that unverified commands or tests succeeded.
6. Keep documentation focused and technically accurate.
7. Preserve unrelated content.
8. Update documentation when implementation changes invalidate existing instructions.
9. Check all code examples and configuration snippets.
10. Report the documentation files changed and any information that could not be verified.

If the implementation is incomplete or ambiguous, explicitly document the uncertainty instead of presenting an assumption as an established fact.
