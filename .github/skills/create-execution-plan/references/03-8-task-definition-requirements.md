# create-execution-plan reference 3

## 8. Task Definition Requirements

Every implementation task must include:

- Task ID.
- Title.
- Objective.
- Context.
- Scope.
- Explicit exclusions.
- Assigned agent.
- Dependencies.
- Expected files or components.
- Implementation requirements.
- Acceptance criteria.
- Required tests.
- Risks.
- Definition of Done.

Tasks must be sufficiently independent to allow parallel execution where safe.

Do not schedule concurrent modifications to the same files without an explicit coordination strategy.

## 9. Dependency Management

Identify dependencies between tasks.

Examples:

- Financial domain decisions must precede financial implementation.
- Financial domain contracts must be defined before FIX processing integration.
- OrderAccumulator processing contracts must be defined before complete FIX integration.
- FIX integration contracts must be available before complete OrderGenerator integration.
- Core implementation must exist before end-to-end testing.
- Functional behavior must be validated before final documentation.

Identify tasks that can run in parallel.

Examples:

- Security analysis can run in parallel with architecture analysis.
- Financial domain analysis can run in parallel with FIX protocol analysis.
- Documentation structure can be planned while implementation is underway.
- Independent test design can begin once contracts and requirements are sufficiently defined.

Do not parallelize tasks that depend on unfinished technical decisions.

## 10. Shared Contracts and Change Management

Define shared contracts before assigning work across application boundaries.

Consider:

- Order request representation.
- Order response representation.
- Order identifiers.
- Symbol and side representations.
- Processing status.
- Rejection information.
- FIX message mapping.

Avoid duplicated contracts with inconsistent semantics.

For every shared file or contract:

- Identify its owner.
- Identify dependent tasks.
- Define how changes are communicated.
- Avoid simultaneous conflicting edits.
- Require integration validation after changes.

## 11. Critical Risks

Create and maintain a risk register covering at least:

| Risk | Required mitigation |
|---|---|
| Acceptance confused with execution | Explicit architectural decision and documentation |
| Concurrent exposure updates exceed limits | Atomic per-symbol processing |
| Duplicate FIX messages modify exposure twice | Application-level idempotency |
| Financial precision errors | Decimal-only calculations |
| Out-of-order ExecutionReports | ClOrdID-based correlation |
| Timeout interpreted as rejection | Explicit unknown outcome |
| FIX session configuration mismatch | Validated configuration and integration tests |
| In-memory state loss | Documented persistence limitation |
| Multiple instances with independent exposure | Documented single-instance limitation or shared persistence |
| Excessive architecture complexity | Scope control and justification for dependencies |

Each risk must have:

- Probability.
- Impact.
- Severity.
- Mitigation.
- Responsible agent.
- Current status.

## 12. Quality Gates

Define explicit quality gates between phases.

### Architecture Gate

- Requirements understood.
- Ambiguities documented.
- Application boundaries defined.
- Financial semantics decided.
- FIX responsibilities defined.
- Technical decisions recorded.

### Domain Gate

- Financial rules implemented.
- Decimal precision preserved.
- Exposure limits enforced.
- Atomicity implemented.
- Idempotency implemented.
- Domain tests pass.

### Integration Gate

- FIX sessions established.
- Messages correctly mapped.
- ExecutionReports correctly generated.
- Correlation works.
- Rejections preserve exposure.
- Integration tests pass.

### Frontend Gate

- Order form works.
- Server-side validation is enforced.
- Order submission is integrated.
- All relevant order statuses are presented.
- HTTP tests pass.

### Final Quality Gate

- Release build succeeds.
- Relevant automated tests pass.
- Financial boundaries are verified.
- FIX behavior is validated.
- Security findings are addressed or documented.
- README instructions are accurate.
- Known limitations are explicit.

Do not mark a gate as passed without evidence.

## 13. Execution Plan Output

Produce the final execution plan in a structured format containing:

1. Executive summary.
2. Repository assessment.
3. Requirements matrix.
4. Assumptions and unresolved questions.
5. Recommended architecture.
6. Critical technical decisions.
7. Project structure.
8. Implementation phases.
9. Detailed task breakdown.
10. Task dependency graph.
11. Parallel execution opportunities.
12. Agent assignment matrix.
13. Shared contracts and ownership.
14. Risk register.
15. Testing strategy.
16. Quality gates.
17. Final delivery checklist.

For each task, provide enough detail for an implementation agent to execute it without redefining the architecture.

Use Mermaid diagrams when they improve understanding:

- Architecture diagram.
- Task dependency graph.
- Order processing sequence.
- FIX communication sequence.

Do not use diagrams as a substitute for explicit task descriptions.

## 14. Final Validation of the Plan

Before presenting the plan, verify:

- Every challenge requirement is covered.
- Every requirement has acceptance criteria.
- Every financial rule has corresponding tests.
- Exposure boundary behavior is explicit.
- Concurrency and idempotency are addressed.
- FIX messages contain the required fields.
- Initiator and Acceptor responsibilities are clear.
- Acceptance and execution semantics are not conflated.
- Asynchronous correlation is addressed.
- Security requirements are included.
- Documentation requirements are included.
- Dependencies are correctly ordered.
- Agent responsibilities do not conflict.
- Parallel tasks do not create unsafe shared-file modifications.
- No unnecessary infrastructure has been introduced.
- Known limitations are documented.
- The plan reflects the actual repository state.

## 15. Final Report

Present the execution plan with:

- Recommended implementation sequence.
- Critical decisions requiring approval.
- Tasks that can begin immediately.
- Tasks blocked by unresolved decisions.
- Parallel work opportunities.
- Highest-priority risks.
- Required quality gates.
- Estimated complexity per phase.

Use relative complexity levels rather than inventing precise implementation durations.

Conclude with one of the following statuses:

- `READY FOR IMPLEMENTATION`
- `READY WITH DOCUMENTED ASSUMPTIONS`
- `BLOCKED BY ARCHITECTURAL DECISIONS`
- `BLOCKED BY MISSING REQUIREMENTS`

Do not modify the repository as part of this planning task.
