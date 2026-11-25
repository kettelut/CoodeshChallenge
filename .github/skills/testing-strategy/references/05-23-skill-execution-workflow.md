# testing-strategy reference 5

## 23. Skill Execution Workflow

When applying this skill:

1. Inspect the actual repository structure and existing test projects.
2. Read the applicable architecture, C#, .NET, financial domain, FIX and security instructions.
3. Identify the business-critical behaviors and risks.
4. Map each requirement to one or more test scenarios.
5. Identify missing test coverage.
6. Prioritize financial integrity, atomicity, idempotency and FIX reliability.
7. Implement tests only within the authorized scope.
8. Execute focused tests during development.
9. Execute integration and end-to-end tests when the environment supports them.
10. Execute the complete test suite.
11. Investigate and report failures accurately.
12. Document gaps and limitations.
13. Provide an evidence-based final testing report.

### Expected final report

Include:

- Repository and test architecture overview.
- Testing strategy applied.
- Test scenarios implemented.
- Financial domain coverage.
- Concurrency and idempotency coverage.
- FIX Protocol coverage.
- Web and end-to-end coverage.
- Commands executed.
- Actual test results.
- Coverage metrics, if available.
- Known gaps and limitations.
- Critical risks.
- Final status: PASS, PASS WITH GAPS, FAIL or BLOCKED.

The final report must distinguish verified behavior from assumptions and untested scenarios.

---

## 24. Official References

- [Microsoft .NET Testing Documentation](https://learn.microsoft.com/en-us/dotnet/core/testing/)
- [xUnit Documentation](https://xunit.net/)
- [ASP.NET Core Integration Tests](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests)
- [Microsoft .NET Code Coverage](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-code-coverage)
- [QuickFIX/n Documentation](https://quickfixn.org/)

