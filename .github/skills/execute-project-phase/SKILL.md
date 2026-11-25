---
name: execute-project-phase
description: "Execute an approved implementation phase of the Coodesh OrderProcessing project by coordinating specialized agents, managing dependencies and validating deliverables."
disable-model-invocation: true
---

# Execute Project Phase

## Purpose

Coordinate one approved implementation phase and its evidence.

## When to use

Use when explicitly asked to execute an approved phase of the project.

## Essential project rules

- The orchestrator remains read-only. Delegate implementation only to authorized agents actually available in the harness; otherwise report blocked work.
- Preserve the approved phase dependencies and distinguish business rejection, protocol errors and communication failures.

Read the [repository instructions](../../copilot-instructions.md), applicable path-specific instructions and the [approved execution plan](../../docs/plan.md).

## Procedure

Inspect the approved plan and phase prerequisites, scope and existing changes. Coordinate only available authorized agents, validate deliverables and report blockers; the orchestrator remains read-only and must not fabricate delegated work or test results.

## Detailed references

Read the relevant reference files below when the task needs their detailed rules, examples, checklists or test matrices. They are not loaded automatically.

- [Objective to 7. Execution Strategy](references/01-objective.md)
- [8. Critical Financial Requirements to 12. Architecture and Code Quality](references/02-8-critical-financial-requirements.md)
- [13. Documentation Requirements to 18. Final Execution Principles](references/03-13-documentation-requirements.md)
