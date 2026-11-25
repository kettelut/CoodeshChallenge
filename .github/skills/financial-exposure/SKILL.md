---
name: financial-exposure
description: Specialized guidance for modeling, calculating, validating and managing financial exposure in C# and .NET applications, including monetary precision, order lifecycle semantics, exposure limits, concurrency, atomicity, idempotency and financial consistency.
---

# Financial Exposure

## Purpose

Guide and review financial validation, exposure and idempotent order processing.

## When to use

Use for order rules, decimal arithmetic, limits, concurrency, failures or financial review.

## Essential project rules

- Use `decimal`, numeric `0.01m` price multiples and the inclusive fixed `100_000_000m` absolute limit per symbol.
- Acceptance applies full signed notional as accounting, not a market execution; business rejection leaves exposure unchanged.
- Under one global lock, prepare exposure and original `ClOrdID` result in one unpublished snapshot and publish it once. Pre-commit failure leaves state unchanged; a post-commit lost response is unknown and an identical retry reproduces the committed result.

Read the [repository instructions](../../copilot-instructions.md), applicable path-specific instructions and the [approved execution plan](../../docs/plan.md).

## Procedure

Trace inputs, projected exposure, identity and the published state. Test both signs and inclusive boundaries, sequential and concurrent duplicates, conflicts, pre-commit failures and retry.

## Detailed references

Read the relevant reference files below when the task needs their detailed rules, examples, checklists or test matrices. They are not loaded automatically.

- [Purpose to 5. Acceptance Versus Execution](references/01-purpose.md)
- [6. Per-Symbol Isolation to 11. Order Processing Workflow](references/02-6-per-symbol-isolation.md)
- [12. Error and Timeout Semantics to 16. Financial Exposure Review Checklist](references/03-12-error-and-timeout-semantics.md)
- [17. Skill Execution Guidelines to 17. Skill Execution Guidelines](references/04-17-skill-execution-guidelines.md)
