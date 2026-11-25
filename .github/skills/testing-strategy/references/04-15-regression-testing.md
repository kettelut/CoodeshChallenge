# testing-strategy reference 4

## 15. Regression Testing

Every identified defect must result in a regression test whenever practical.

Regression tests must cover:

- Incorrect exposure calculation.
- Exposure limit bypass.
- Floating-point precision issues.
- Incorrect Buy/Sell mapping.
- Duplicate order processing.
- Incorrect ClOrdID correlation.
- Incorrect ExecutionReport status.
- Incorrect rejection behavior.
- Concurrent processing failures.
- Invalid server-side validation.
- Timeout incorrectly treated as rejection.
- FIX session recovery defects.

Regression tests must reproduce the original defect before validating the correction.

---

## 16. Code Coverage and Quality Gates

Coverage is a diagnostic metric, not proof of correctness.

Prioritize meaningful coverage of:

- Financial calculations.
- Order validation.
- Exposure limit enforcement.
- Concurrency control.
- Idempotency.
- Rejection handling.
- FIX message construction.
- FIX message processing.
- Response correlation.
- Application workflows.

Suggested initial targets:

| Area | Suggested coverage |
|---|---:|
| Domain and financial rules | 90%+ |
| Application services | 85%+ |
| FIX integration logic | 80%+ |
| Web application logic | 75%+ |
| Overall solution | 80%+ |

These values are targets, not substitutes for risk-based test design.

Do not compromise test quality to reach a percentage.

Critical financial and idempotency scenarios must be tested even when line coverage is already high.

---

## 17. Build and Test Execution

Execute commands from the repository root or the appropriate solution directory.

Inspect the actual solution and project names before executing commands.

Recommended workflow:

    dotnet --info
    dotnet restore
    dotnet build --no-restore
    dotnet test
    dotnet test --configuration Release

For a specific test project:

    dotnet test path/to/TestProject.csproj

For detailed test output:

    dotnet test --logger "console;verbosity=normal"

For code coverage, when Coverlet is configured:

    dotnet test --collect:"XPlat Code Coverage"

Do not assume the solution uses a specific test runner, coverage collector or project layout without inspecting the repository.

### Execution rules

- Run tests after relevant implementation changes.
- Run focused tests during development.
- Run the complete suite before final delivery.
- Investigate failures instead of suppressing them.
- Distinguish compilation failures from test failures.
- Record the exact command and result.
- Do not report tests as passed if execution was skipped.
- Do not modify production code merely to make tests pass without understanding the defect.

---

## 18. Failure Analysis

When a test fails:

1. Identify the failing scenario.
2. Determine whether the failure is caused by production code, test code, environment or configuration.
3. Inspect the complete exception and relevant logs.
4. Reproduce the failure.
5. Verify whether the failure affects financial correctness or integration reliability.
6. Correct the root cause.
7. Add or update regression coverage.
8. Re-run the focused test.
9. Re-run the affected test suite.
10. Re-run the full suite when appropriate.

Never:

- Remove a failing test merely to obtain a green build.
- Weaken assertions without justification.
- Introduce arbitrary delays to hide race conditions.
- Ignore flaky tests.
- Treat skipped tests as passed.
- Hide integration failures behind mocks.

---

## 19. Testing In-Memory Implementations

If the solution uses in-memory exposure and idempotency state, test the documented limitations explicitly.

Validate:

- State consistency during application execution.
- Concurrent updates.
- Duplicate processing.
- Symbol isolation.
- State initialization.
- Behavior after service recreation.
- Behavior after application restart.

Document that in-memory state generally does not provide:

- Durable financial records.
- Recovery after process termination.
- Shared state across multiple application instances.
- Distributed locking.
- Cross-instance idempotency.

Do not claim production-grade financial durability when the implementation uses only process-local memory.

---

## 20. Test Reporting

Every test execution report must contain:

- Date and execution context when relevant.
- Executed command.
- Test projects included.
- Total tests discovered.
- Passed tests.
- Failed tests.
- Skipped tests.
- Build status.
- Integration test status.
- Coverage information, if available.
- Known limitations.
- Relevant failure details.

Use the following status values:

- **PASS:** All required tests executed successfully.
- **PASS WITH GAPS:** Tests passed, but relevant scenarios remain untested.
- **FAIL:** One or more required tests failed.
- **BLOCKED:** Execution could not be completed because of an environment, dependency or configuration issue.

Clearly distinguish:

- Unit tests.
- Integration tests using mocks.
- Integration tests using real QuickFIX/n sessions.
- End-to-end tests.
- Tests not executed.

Never represent mocked FIX communication as proof of real FIX interoperability.

---

## 21. Definition of Done

Testing is complete only when:

- [ ] All critical business rules have automated coverage.
- [ ] Monetary calculations use `decimal`.
- [ ] Exposure boundaries are validated.
- [ ] Buy and Sell calculations are validated.
- [ ] Symbol isolation is validated.
- [ ] Concurrent exposure updates are validated.
- [ ] Idempotency is validated under sequential and concurrent execution.
- [ ] Rejected orders do not modify exposure.
- [ ] FIX NewOrderSingle fields are validated.
- [ ] Accepted ExecutionReports are validated.
- [ ] Rejected ExecutionReports are validated.
- [ ] ClOrdID response correlation is validated.
- [ ] Timeout behavior is validated.
- [ ] Duplicate and retransmitted messages are validated.
- [ ] Web server-side validation is tested.
- [ ] Critical integration workflows are tested.
- [ ] Relevant regression tests exist.
- [ ] The complete test suite has been executed.
- [ ] Failures and skipped tests are documented.
- [ ] Known limitations are documented.
- [ ] No test result has been fabricated.

---

## 22. Anti-Patterns to Avoid

Do not:

- Use `double` or `float` for financial assertions.
- Test only happy paths.
- Ignore exposure boundary conditions.
- Ignore concurrency.
- Assume sequential idempotency tests prove concurrent safety.
- Mock QuickFIX/n in every integration test.
- Treat `ExecType=New` as proof of execution.
- Treat timeouts as business rejections.
- Use arbitrary sleeps to coordinate asynchronous tests.
- Depend on production services or credentials.
- Share mutable state between tests.
- Ignore flaky tests.
- Remove tests to hide defects.
- Measure quality exclusively through coverage.
- Claim successful validation without execution evidence.

---

