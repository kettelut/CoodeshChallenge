---
name: testing-strategy
description: Comprehensive testing strategy for .NET 10 applications, covering unit, integration, end-to-end, financial domain, concurrency, idempotency, FIX Protocol 4.4, ASP.NET Core and reliability testing, with emphasis on deterministic execution, meaningful coverage and measurable quality gates.
---

# Testing Strategy

## Purpose

Plan, implement and validate risk-oriented tests for the challenge.

## When to use

Use for unit, concurrency, idempotency, FIX, web, integration or end-to-end testing.

## Essential project rules

- Cover numeric price increments, both exposure boundaries, business rejection and original `ClOrdID` results using `decimal`.
- Inject failures before and during preparation and before publication; assert the full published state and retry. Also test response loss after commit, concurrent duplicates and cross-symbol conflicts.
- Distinguish protocol-level rejection and communication failure from business-rejected ExecutionReports; do not claim real FIX interoperability from mocks.

Read the [repository instructions](../../copilot-instructions.md), applicable path-specific instructions and the [approved execution plan](../../docs/plan.md).

## Procedure

Map actual behavior to deterministic cases; validate observable outcomes and complete state. Use isolated real FIX sessions when testing interoperability, run authorized focused checks and report unexecuted tests rather than claiming success.

## Detailed references

Read the relevant reference files below when the task needs their detailed rules, examples, checklists or test matrices. They are not loaded automatically.

- [1. Purpose to 6. Unit Testing Strategy](references/01-1-purpose.md)
- [7. Concurrency and Atomicity Testing to 9. Integration Testing](references/02-7-concurrency-and-atomicity-testing.md)
- [10. OrderGenerator Web Testing to 14. Determinism and Asynchronous Testing](references/03-10-ordergenerator-web-testing.md)
- [15. Regression Testing to 22. Anti-Patterns to Avoid](references/04-15-regression-testing.md)
- [23. Skill Execution Workflow to 24. Official References](references/05-23-skill-execution-workflow.md)
