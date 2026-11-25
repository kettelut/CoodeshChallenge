---
name: review-architecture
description: "Perform a comprehensive architectural review of the Coodesh OrderProcessing solution, evaluating structural integrity, application boundaries, dependency direction, design decisions, scalability, maintainability and compliance with the challenge requirements."
disable-model-invocation: true
---

# Review Architecture

## Purpose

Review the challenge architecture and report evidence-based findings.

## When to use

Use when explicitly asked for an architectural review.

## Essential project rules

- Verify the selected single global lock and atomic snapshot publication, identity consistency, independent exposures and in-memory restart limitations.
- Check boundary isolation, protocol/business/technical error classification and report findings with concrete evidence.

Read the [repository instructions](../../copilot-instructions.md), applicable path-specific instructions and the [approved execution plan](../../docs/plan.md).

## Procedure

Inspect actual dependencies, domain and FIX boundaries, financial state, security, tests and runtime limitations. Remain read-only and distinguish verified findings from assumptions.

## Detailed references

Read the relevant reference files below when the task needs their detailed rules, examples, checklists or test matrices. They are not loaded automatically.

- [Objective to 6. Critical Architectural Decision: Acceptance vs. Execution](references/01-objective.md)
- [7. Application Layer Review to 12. Dependency Injection and Configuration](references/02-7-application-layer-review.md)
- [13. Frontend and Presentation Architecture to 19. Final Architecture Review Report](references/03-13-frontend-and-presentation-architecture.md)
- [20. Final Review Criteria to Final Instructions](references/04-20-final-review-criteria.md)
