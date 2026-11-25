# create-execution-plan reference 2

## 5. FIX Protocol Analysis

Use the FIX protocol specialist agent when available and permitted; otherwise perform the permitted read-only analysis directly and report the limitation. Define:

- Initiator and Acceptor responsibilities.
- FIX session configuration.
- Required message fields.
- Message validation.
- ExecutionReport mapping.
- Session lifecycle.
- Asynchronous response correlation.
- Error handling.
- Duplicate message behavior.
- Retransmission handling.
- Recovery behavior.
- Integration test strategy.

### NewOrderSingle

Required fields:

| Field | Tag |
|---|---:|
| ClOrdID | 11 |
| Symbol | 55 |
| Side | 54 |
| OrderQty | 38 |
| OrdType | 40 |
| Price | 44 |
| TransactTime | 60 |

Expected values:

- Buy: Side `1`.
- Sell: Side `2`.
- Limit: OrdType `2`.

### ExecutionReport

Relevant fields:

| Field | Tag |
|---|---:|
| OrderID | 37 |
| ExecID | 17 |
| ClOrdID | 11 |
| Symbol | 55 |
| Side | 54 |
| ExecType | 150 |
| OrdStatus | 39 |
| OrderQty | 38 |
| Price | 44 |
| CumQty | 14 |
| LeavesQty | 151 |
| AvgPx | 6 |
| TransactTime | 60 |
| OrdRejReason | 103 |
| Text | 58 |

Accepted order:

- `ExecType = 0` (New).
- `OrdStatus = 0` (New).
- `CumQty = 0`.
- `LeavesQty = OrderQty`.

Rejected order:

- `ExecType = 8` (Rejected).
- `OrdStatus = 8` (Rejected).

Do not confuse protocol-level acceptance with trade execution.

### Asynchronous Correlation

Use `ClOrdID` as the primary correlation identifier.

The plan must address:

- Thread-safe pending request tracking.
- Multiple simultaneous requests.
- Responses arriving out of order.
- Duplicate ExecutionReports.
- Unknown response identifiers.
- Request timeouts.
- Session disconnections.
- Pending request cleanup.
- Late responses.

A timeout must not be treated as proof of rejection.

Do not automatically resubmit an order after an unknown outcome.

## 6. Implementation Planning

Use the implementation planner agent when available and permitted; otherwise produce the plan directly in read-only mode and report the limitation.

Organize the implementation into phases.

### Approved phases and dependencies

Follow the detailed deliverables, verification and dependencies in the approved execution plan:

| Phase | Objective | Dependencies |
|---|---|---|
| 0 | Repository inventory and requirements analysis | None |
| 1 | Architecture and technical decisions | 0 |
| 2 | Detailed implementation planning | 1 |
| 3 | OrderAccumulator foundation and FIX Acceptor infrastructure | 2 |
| 4 | Financial domain, validation, exposure, atomicity and idempotency | 2; 3 supplies the host |
| 5 | NewOrderSingle processing, domain integration and ExecutionReports | 3 and 4 |
| 6 | OrderGenerator, frontend and FIX Initiator | 2 and 5 |
| 7 | End-to-end integration and tests | 5 and 6 |
| 8 | Security, quality, documentation and final validation | 7 |

Phase 3 must not implement complete order processing. Phase 4 includes fault-injection,
concurrency and duplicate tests. Phase 5 distinguishes business and protocol rejection.
Phase 8 includes verified README instructions and the required attribution:
`This is a challenge by [Coodesh](https://coodesh.com/).`

### Detailed planning checklist

- Inventory existing implementation and SDK availability; map requirements, assumptions, constraints and risks.
- Define solution and project boundaries, .NET 10, nullable reference types, common build properties, dependency injection, configuration, logging, test projects and `.gitignore`, only when justified.
- Plan symbol and side models, validation, decimal calculations, signed exposure, limits, atomicity, idempotency, rejection behavior and domain tests.
- Plan the accumulator host, application service, validation orchestration, state management, error handling and application tests.
- Configure QuickFIX/n, isolated Acceptor and Initiator sessions, lifecycle and test stores; plan NewOrderSingle mapping, ExecutionReports, correlation, timeout, disconnect and duplicate handling.
- Plan the form, required fields, client and server validation, submission service, pending/accepted/rejected/unknown feedback and web tests.
- Validate both applications and the FIX session end to end, including accepted and rejected orders, exposure, limits, idempotency, concurrency, disconnect and timeout.
- Review architecture, financial correctness, FIX behavior, security, dependency vulnerabilities and build configurations; fix confirmed defects and rerun affected tests.
- Produce test, code review, security and build reports. Verify README prerequisites, installation, configuration, execution, order submission, FIX setup, rules, accounting semantics, limitations, test commands and Coodesh attribution.

## 7. Agent Assignment

Use specialized agents according to their responsibilities only when delegation and the required capabilities are actually available. A file or agent name does not prove availability. Otherwise perform permitted read-only planning directly and report any unavailable independent review; do not assign implementation to a read-only agent.

### Orchestration

`project-orchestrator`

Responsibilities:

- Coordinate analysis and implementation.
- Manage task dependencies.
- Delegate work.
- Consolidate findings.
- Track risks.
- Validate phase completion.
- Avoid conflicting modifications.

The orchestrator must remain read-only.

### Planning Agents

`solution-architect`

- Architecture definition.
- Application boundaries.
- Dependency rules.
- Technical decisions.

`financial-domain-specialist`

- Financial rules.
- Exposure calculations.
- Atomicity.
- Idempotency.
- Financial test scenarios.

`fix-protocol-specialist`

- FIX 4.4.
- QuickFIX/n.
- Session behavior.
- Message structures.
- Correlation and reliability.

`implementation-planner`

- Task decomposition.
- Dependency ordering.
- Implementation sequence.
- Acceptance criteria.

Planning agents must remain read-only.

### Implementation Agents

`implementation-agent`

- Domain models.
- Application services.
- Business logic.
- Financial processing.

`fix-integration-agent`

- QuickFIX/n.
- FIX Initiator.
- FIX Acceptor.
- Message mapping.
- Session lifecycle.
- Correlation.

`frontend-agent`

- OrderGenerator web interface.
- Order submission.
- Input validation.
- User-facing order status.

Implementation agents must modify only their assigned scope.

### Quality Agents

`test-engineer`

- Automated test design.
- Unit tests.
- Integration tests.
- Concurrency tests.
- FIX tests.
- End-to-end tests.

`code-reviewer`

- Correctness.
- Architecture compliance.
- Maintainability.
- Financial integrity.
- Implementation quality.

`build-validator`

- Restore.
- Build.
- Test execution.
- Project configuration.
- Dependency consistency.

`security-reviewer`

- Security review.
- Configuration review.
- Input validation.
- Secrets.
- Vulnerabilities.

`documentation-specialist`

- README.
- Architecture documentation.
- Setup and execution guides.
- Operational documentation.

Quality and review agents must remain read-only unless their specific role explicitly authorizes changes.

