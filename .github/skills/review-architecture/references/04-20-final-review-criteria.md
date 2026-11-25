# review-architecture reference 4

## 20. Final Review Criteria

Use `ARCHITECTURE APPROVED` when:

- Application responsibilities are clearly separated.
- Dependency direction is correct.
- Financial business rules are protected.
- FIX integration does not contaminate the domain.
- Concurrency and idempotency are adequately addressed.
- No Critical or High architectural findings remain.
- The design is appropriate for the challenge scope.

Use `ARCHITECTURE APPROVED WITH RECOMMENDATIONS` when:

- Mandatory architectural requirements are satisfied.
- No Critical or High architectural findings remain.
- Remaining findings are non-blocking improvements.
- Relevant limitations are documented.

Use `ARCHITECTURE REQUIRES CHANGES` when:

- Critical or High architectural violations exist.
- Financial correctness is compromised.
- Application boundaries are fundamentally incorrect.
- The architecture prevents reliable implementation of mandatory requirements.

Use `ARCHITECTURE REVIEW BLOCKED` when:

- Essential repository content cannot be inspected.
- Required dependencies or implementation evidence are unavailable.
- The architecture cannot be evaluated reliably.

## Final Instructions

- Review the actual implementation, not the intended design.
- Prioritize financial correctness, dependency direction and FIX boundaries.
- Be pragmatic about architectural complexity.
- Do not introduce infrastructure without justification.
- Do not modify repository files.
- Do not implement fixes.
- Do not confuse personal preferences with architectural defects.
- Support every significant finding with evidence.
- Clearly distinguish defects, risks and recommendations.

The final deliverable is an independent architectural assessment with actionable findings and an explicit verdict.
