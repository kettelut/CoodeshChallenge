---
name: create-execution-plan
description: "Create a comprehensive, dependency-aware execution plan for implementing the Coodesh OrderProcessing challenge."
disable-model-invocation: true
---

# Create Execution Plan

## Purpose

Create a dependency-aware implementation plan without implementing applications.

## When to use

Use when asked to plan the Coodesh solution or a substantial phase.

## Essential project rules

- Use the approved phases 0-8 and their actual dependencies; phase 3 establishes Acceptor infrastructure, not complete order processing.
- Plan atomic snapshot publication, financial fault injection, protocol rejection mapping, real FIX integration and documented final validation.

Read the [repository instructions](../../copilot-instructions.md), applicable path-specific instructions and the [approved execution plan](../../docs/plan.md).

## Procedure

Inspect the repository and requirements; identify assumptions, ownership, dependencies, deliverables, acceptance criteria and tests. Follow phases 0-8 in the approved plan. Delegate only when available and permitted; otherwise perform read-only planning and report limits.

## Detailed references

Read the relevant reference files below when the task needs their detailed rules, examples, checklists or test matrices. They are not loaded automatically.

- [Objective to 4. Financial Domain Analysis](references/01-objective.md)
- [5. FIX Protocol Analysis to 7. Agent Assignment](references/02-5-fix-protocol-analysis.md)
- [8. Task Definition Requirements to 15. Final Report](references/03-8-task-definition-requirements.md)
