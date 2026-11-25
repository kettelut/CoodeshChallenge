# dotnet-project-architecture reference 4

## 18. Architecture Review Checklist

Use this checklist during architecture design and review.

### Solution Structure

- [ ] Project boundaries are meaningful.
- [ ] Responsibilities are clearly assigned.
- [ ] Dependencies are consistent.
- [ ] Circular dependencies do not exist.
- [ ] Naming is consistent.

### Domain

- [ ] Business rules are independent of infrastructure.
- [ ] Entities protect invariants.
- [ ] Value objects represent appropriate concepts.
- [ ] Monetary calculations use decimal.
- [ ] Financial state changes are consistent.

### Application

- [ ] Use cases have clear responsibilities.
- [ ] Application services coordinate domain operations.
- [ ] Contracts are explicit.
- [ ] Technical concerns do not leak into business workflows.

### Infrastructure

- [ ] External integrations are isolated.
- [ ] Implementations follow appropriate contracts.
- [ ] Configuration is externalized.
- [ ] Resource lifecycles are managed.

### Presentation

- [ ] Presentation components remain thin.
- [ ] Server-side validation exists.
- [ ] Business rules are not duplicated.
- [ ] External models are not exposed unnecessarily.

### Reliability

- [ ] Financial updates are atomic.
- [ ] Duplicate processing is prevented.
- [ ] Timeouts are handled explicitly.
- [ ] Errors are classified correctly.
- [ ] Recovery limitations are documented.

### Quality

- [ ] Domain logic is independently testable.
- [ ] Integration boundaries can be tested.
- [ ] Critical business scenarios have automated tests.
- [ ] Architecture decisions are documented when necessary.

---

## 19. Decision-Making Guidelines

When evaluating alternative architectures, compare them using:

1. Correctness.
2. Complexity.
3. Maintainability.
4. Testability.
5. Reliability.
6. Security.
7. Operational cost.
8. Alignment with actual requirements.

Prefer the option that satisfies the requirements with the fewest unnecessary moving parts.

For each significant recommendation, explain:

- The current problem.
- Why it matters.
- The proposed approach.
- Its expected benefit.
- Its implementation cost.
- Any trade-offs.

Do not recommend architectural changes based only on personal preference.

---

## 20. Skill Execution Guidelines

When this skill is used:

1. Understand the actual requirements.
2. Inspect the existing repository before proposing a structure.
3. Identify application boundaries and dependencies.
4. Trace critical workflows across layers.
5. Evaluate domain independence.
6. Review infrastructure integration boundaries.
7. Examine state management, concurrency and consistency.
8. Evaluate testing and security implications.
9. Identify unnecessary complexity.
10. Produce evidence-based architectural recommendations.

For architecture reviews, remain read-only unless a separate implementation task explicitly authorizes modifications.

For implementation tasks, follow the approved architecture and the repository's applicable instructions.

The final architectural recommendation must be practical, traceable to requirements and appropriate to the project's scope.
