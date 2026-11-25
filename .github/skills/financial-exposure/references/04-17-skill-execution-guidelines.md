# financial-exposure reference 4

## 17. Skill Execution Guidelines

When implementing financial exposure functionality:

1. Inspect the existing domain model and architecture.
2. Verify the established acceptance accounting and FIX lifecycle semantics.
3. Confirm all input constraints.
4. Use `decimal` for monetary calculations.
5. Implement projected exposure validation.
6. Enforce per-symbol isolation.
7. Protect validation and mutation atomically.
8. Implement thread-safe idempotency.
9. Ensure rejected orders do not change exposure and their business results are retained.
10. Integrate FIX through explicit translation boundaries.
11. Add deterministic unit and concurrency tests.
12. Document operational limitations.

When reviewing financial exposure functionality:

1. Trace the complete order processing path.
2. Identify where exposure is read and modified.
3. Verify numeric types and arithmetic.
4. Inspect synchronization boundaries.
5. Inspect duplicate detection and identifier scope.
6. Verify rejection behavior.
7. Verify FIX lifecycle semantics.
8. Inspect test coverage and actual results.
9. Report findings with concrete evidence.
10. Prioritize financial corruption and exposure-limit bypass risks.

The financial exposure implementation is acceptable only when its calculations, state transitions, concurrency behavior and idempotency guarantees follow the established challenge accounting rule.
