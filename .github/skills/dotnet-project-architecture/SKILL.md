---
name: dotnet-project-architecture
description: Architectural guidance for designing, analyzing and reviewing maintainable .NET 10 solutions, including layered architecture, Clean Architecture principles, dependency direction, application boundaries, domain modeling, dependency injection, infrastructure integration and pragmatic architectural decisions.
---

# Dotnet Project Architecture

## Purpose

Design and review pragmatic .NET 10 application boundaries.

## When to use

Use for architecture, layering, dependency direction, state management or integration decisions.

## Essential project rules

- Keep domain rules independent of QuickFIX/n and avoid unnecessary infrastructure.
- Use the approved global lock and single copy-on-write snapshot publication; state loss after restart and serialization across symbols are explicit trade-offs.

Read the [repository instructions](../../copilot-instructions.md), applicable path-specific instructions and the [approved execution plan](../../docs/plan.md).

## Procedure

Inspect the actual solution and trace the relevant flow. Keep QuickFIX/n outside the domain, choose justified boundaries and assess correctness, reliability, testing, security and trade-offs.

## Detailed references

Read the relevant reference files below when the task needs their detailed rules, examples, checklists or test matrices. They are not loaded automatically.

- [Purpose to 4. Application Layer](references/01-purpose.md)
- [5. Infrastructure Layer to 10. External Integration Architecture](references/02-5-infrastructure-layer.md)
- [11. Persistence and Transaction Boundaries to 17. Common Architectural Anti-Patterns](references/03-11-persistence-and-transaction-boundaries.md)
- [18. Architecture Review Checklist to 20. Skill Execution Guidelines](references/04-18-architecture-review-checklist.md)
