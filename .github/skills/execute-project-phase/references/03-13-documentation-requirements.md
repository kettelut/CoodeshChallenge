# execute-project-phase reference 3

## 13. Documentation Requirements

Documentation must reflect the actual implementation.

When the phase affects documentation, verify:

- README remains accurate.
- Installation instructions work.
- Configuration examples are safe.
- Execution commands are correct.
- FIX session requirements are documented.
- Financial rules are documented.
- The acceptance accounting simplification and its distinction from market execution are explicit.
- Known limitations are documented.
- Test commands are accurate.

The README must be written in English.

When applicable, include:

`This is a challenge by [Coodesh](https://coodesh.com/).`

Do not claim features or operational capabilities that have not been implemented.

## 14. Quality Gates

Apply the relevant gates for the current phase.

### Architecture Gate

- Requirements are understood.
- Architectural boundaries are respected.
- Technical decisions are documented.
- Dependencies are justified.

### Domain Gate

- Financial rules are implemented.
- Decimal precision is preserved.
- Exposure limits are enforced.
- Atomicity is guaranteed.
- Idempotency is implemented.
- Domain tests pass.

### FIX Gate

- FIX sessions work.
- Messages are mapped correctly.
- ExecutionReports are correct.
- Correlation works.
- Duplicate processing is prevented.
- Integration tests pass.

### Frontend Gate

- Order form works.
- Server-side validation is enforced.
- Submission reaches the application boundary.
- Statuses are presented correctly.
- HTTP integration tests pass.

### Quality Gate

- Relevant build succeeds.
- Relevant tests pass.
- Critical review findings are addressed.
- Security issues are addressed or documented.
- No known critical regression remains.

A gate must be marked:

- `PASSED`
- `PASSED WITH WARNINGS`
- `FAILED`
- `BLOCKED`
- `NOT APPLICABLE`

Provide evidence for each result.

## 15. Handling Blockers and Deviations

When a task is blocked:

1. Identify the blocking condition.
2. Determine which dependency is missing.
3. Identify the responsible agent or decision owner.
4. Explain the impact.
5. Propose a resolution.
6. Prevent dependent tasks from proceeding incorrectly.

When an implementation deviates from the approved plan:

- Identify the original decision.
- Describe the deviation.
- Explain why it occurred.
- Assess architectural and financial impact.
- Request or record the required approval.
- Update the relevant documentation.

Do not silently change shared contracts or architectural assumptions.

## 16. Scope Control

Keep implementation aligned with the Coodesh challenge.

Before introducing a new dependency, framework or infrastructure component, evaluate:

- Is it explicitly required?
- Does it solve a demonstrated problem?
- Does it introduce operational complexity?
- Can the requirement be met with existing .NET capabilities?
- Is the maintenance cost justified?

Do not introduce the following without explicit justification:

- RabbitMQ.
- Kafka.
- Redis.
- Kubernetes.
- Distributed services.
- Complex persistence infrastructure.
- Unnecessary authentication systems.
- Unnecessary abstractions.

Prefer the simplest architecture that correctly satisfies the requirements.

## 17. Phase Completion Report

At the end of execution, produce a structured report.

### Phase Summary

- Phase identifier.
- Phase name.
- Execution status.
- Objectives.
- Overall result.

### Task Results

For every task, report:

- Task ID.
- Description.
- Assigned agent.
- Status.
- Modified files.
- Acceptance criteria result.
- Tests executed.
- Remaining issues.

Use these statuses:

- `COMPLETED`
- `COMPLETED WITH WARNINGS`
- `IN PROGRESS`
- `BLOCKED`
- `FAILED`
- `NOT STARTED`

### Validation Results

Report:

- Build status.
- Test results.
- Code review status.
- Security review status.
- FIX validation status where applicable.
- Documentation status where applicable.

Include actual commands and results when available.

### Risks and Limitations

Report:

- Unresolved risks.
- Known limitations.
- Architectural deviations.
- Security findings.
- Technical debt introduced.
- Follow-up actions.

### Next Phase Readiness

Identify:

- Completed prerequisites.
- Missing prerequisites.
- Required decisions.
- Recommended next tasks.

Conclude with one of the following statuses:

- `PHASE COMPLETED`
- `PHASE COMPLETED WITH WARNINGS`
- `PHASE PARTIALLY COMPLETED`
- `PHASE BLOCKED`
- `PHASE FAILED`

Do not declare a phase completed if mandatory acceptance criteria remain unmet.

## 18. Final Execution Principles

The project must progress through controlled, verifiable increments.

Prioritize:

1. Correctness over implementation speed.
2. Financial integrity over convenience.
3. Explicit contracts over implicit assumptions.
4. Deterministic tests over manual confidence.
5. Independent quality review over self-approval.
6. Minimal architecture over unnecessary complexity.
7. Evidence-based completion over optimistic status reporting.

The orchestrator coordinates the work; available authorized specialized agents implement and validate their assigned responsibilities. Report unavailable capabilities and blocked tasks.

Do not modify application source code directly as the orchestrator.
