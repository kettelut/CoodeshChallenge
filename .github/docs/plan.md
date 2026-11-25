# Execution Plan: FIX 4.4 Order Generator and Accumulator

## 1. Repository Analysis

- The repository contains `README.md`, `.gitignore`, and Copilot configuration under `.github`.
- `README.md` describes the planned applications; its run and test commands are not yet executable.
- The `.gitignore` covers common build outputs, local environment files and FIX runtime directories.
- Verify the installed .NET SDK and runtimes before implementation; do not assume a particular local version.
- No solution, source projects, test projects, build scripts, package lock convention, or CI configuration currently exist.
- No implementation files are to be created under the repository during this planning task.

## 2. Executive Architecture Recommendation

Use one Visual Studio solution containing two independently runnable applications, small domain/application projects shared only where justified, and no database or messaging infrastructure by default.

### OrderGenerator

- ASP.NET Core 10 web application using server-rendered Razor Pages or MVC for the smallest maintainable UI.
- A POST from the browser calls an application service, not QuickFIX/n directly from JavaScript.
- The service creates a unique `ClOrdID`, sends a FIX `NewOrderSingle`, and waits for the correlated `ExecutionReport` through an in-memory pending-request registry.
- QuickFIX/n runs behind an initiator service.

### OrderAccumulator

- Console/worker application or minimal ASP.NET Core host with a FIX acceptor.
- A focused application service validates and atomically evaluates/updates exposure.
- A `MessageCracker`-based QuickFIX/n application receives FIX 4.4 messages.
- Exposure is held in memory under one lock or serial gate. Order history may be retained in memory only for duplicate detection.

### Shared code

A shared domain project is justified only for:

- Symbol and side types.
- Input validation primitives.
- Exposure calculation and result types.
- FIX-neutral order and report contracts.

It should not expose QuickFIX/n types. If independent deployment boundaries make this project undesirable, duplicate the small DTO surface but keep one source of truth for validation and exposure rules. The proposed structure follows the requirements as a pragmatic two-project boundary plus one small domain project:

```text
OrderProcessing.sln
src/
  OrderGenerator/
    OrderGenerator.csproj
    Pages or Controllers/
    Application/
      IOrderSubmissionService.cs
      OrderSubmissionService.cs
      PendingOrderRegistry.cs
    Infrastructure/
      Fix/
  OrderAccumulator/
    OrderAccumulator.csproj
    Application/
    Infrastructure/
      Fix/
  OrderProcessing.Domain/
    Symbols.cs
    Side.cs
    Order.cs
    Exposure.cs
tests/
  OrderGenerator.Tests/
  OrderAccumulator.Tests/
  OrderProcessing.Domain.Tests/
  OrderProcessing.Integration.Tests/
  OrderProcessing.EndToEnd.Tests/
docs/
  architecture.md
  fix-44-session.md
```

A more detailed clean structure is not recommended unless the domain layer becomes more substantial. A pragmatic layered architecture is sufficient: web/API, application orchestration, domain rules, and FIX infrastructure.

## 3. Technology and Dependencies

- Target all projects and test projects to `net10.0`.
- Add a root `global.json` pinning the installed 10.0 SDK line only if a team needs reproducible SDK resolution.
- Use ASP.NET Core built-in dependency injection and configuration.
- Use QuickFIX/n packages: `QuickFIXn.Core` and the FIX 4.4 dictionary package/version supplied by the chosen package release. Add only after confirming NuGet compatibility at implementation time.
- Use Razor Pages or MVC rather than introducing a SPA framework.
- Use xUnit and FluentAssertions or the repository's selected test conventions.
- Use Coverlet/MSBuild coverage integration only if it does not require a separate pipeline dependency.
- Use `Microsoft.Extensions.Logging` and structured console logging.
- Use `IOptions<T>` for technical settings such as ports, sender/target IDs, timeouts, heartbeats, and FIX file paths. The challenge exposure limit is a domain constant, not configuration.
- Use `appsettings.json`, environment variables, and `dotnet run --launch-profile` for local configuration.
- Do not add a database, queue, Redis, container orchestration, or external scheduler.

### Copilot agent instruction loading

The [Copilot CLI documentation](https://docs.github.com/en/copilot/how-tos/copilot-cli/customize-copilot/create-custom-agents-for-cli) supports `include-custom-instructions: true` for custom subagents. The [VS Code custom agent reference](https://code.visualstudio.com/docs/agents/reference/custom-agent-file-reference) states that SDK conversion does not forward this property. Therefore each agent explicitly reads the repository instructions, this plan and applicable path-specific instructions as a cross-harness fallback, without expanding its tool or editing permissions. Verify actual discovery and instruction loading in the selected runtime; a profile alone is not proof.

## 4. FIX 4.4 Message Specification

### FIX session settings

OrderAccumulator is the acceptor:

```text
ConnectionType=acceptor
SocketAcceptPort=<configured-port>
StartDay/StartTime=only if a scheduled session is required
UseDataDictionary=Y
DataDictionary=spec/FIX44.xml
SenderCompID=<accumulator-id>
TargetCompID=<generator-id>
HeartBtInt=<configured-positive-value>
ResetOnLogon=N initially
FileLogPath=<ignored runtime directory>
FileStorePath=<ignored runtime directory>
ReconnectInterval=<positive-value>
ValidateFieldsHaveValues=Y
ValidateFieldsOutOfOrder=Y
ValidateUnorderedGroupFields=Y
```

OrderGenerator is the initiator:

```text
ConnectionType=initiator
SocketConnectHost=<accumulator-host>
SocketConnectPort=<configured-port>
UseDataDictionary=Y
DataDictionary=spec/FIX44.xml
SenderCompID=<generator-id>
TargetCompID=<accumulator-id>
HeartBtInt=<same-or-compatible-positive-value>
ReconnectInterval=<positive-value>
ResetOnLogon=N initially
FileLogPath=<ignored runtime directory>
FileStorePath=<ignored runtime directory>
```

Sequence numbers and persistent stores must be consistent across both sides. The first implementation may use `ResetOnLogon=Y` in a controlled local test configuration, but not as the production assumption. Sequence reset, persistence, and reconnect behavior must be explicitly documented.

### NewOrderSingle fields

The `NewOrderSingle` message must contain:

| Tag | FIX 4.4 field | Required semantics |
|---|---|---|
| 35 | MsgType | `D` for NewOrderSingle |
| 11 | ClOrdID | Unique, generated per web submission |
| 55 | Symbol | `PETR4`, `VALE3`, or `VIIA4` |
| 54 | Side | `1` Buy or `2` Sell |
| 38 | OrderQty | Integer greater than zero and less than 100000 |
| 44 | Price | Decimal greater than zero and less than 1000, numeric multiple of `0.01` |
| 40 | OrdType | `2` Limit |
| 60 | TransactTime | UTC FIX timestamp |

The web form must enforce the complete business validation before transmission. The FIX acceptor must repeat validation because FIX callers must never trust the web layer.

### ExecutionReport fields

Accepted:

| Tag | FIX 4.4 field | Value |
|---|---|---|
| 37 | OrderID | A server-generated unique order ID |
| 17 | ExecID | A unique execution/report ID |
| 11 | ClOrdID | Original client order identifier |
| 150 | ExecType | `0` New |
| 39 | OrdStatus | `0` New |
| 55 | Symbol | Original symbol |
| 54 | Side | Original side |
| 38 | OrderQty | Original quantity |
| 44 | Price | Original price |
| 60 | TransactTime | Server UTC timestamp |
| 151 | LeavesQty | Original OrderQty; no market execution has occurred |
| 14 | CumQty | `0`; no market execution has occurred |
| 6 | AvgPx | `0` when no market execution has occurred |

Rejected:

| Tag | FIX 4.4 field | Value |
|---|---|---|
| 37 | OrderID | Server order identifier |
| 17 | ExecID | Unique report identifier |
| 11 | ClOrdID | Original client order identifier |
| 150 | ExecType | `8` Rejected |
| 39 | OrdStatus | `8` Rejected |
| 103 | OrdRejReason | Appropriate FIX enum value |
| 58 | Text | Human-readable, non-sensitive reason |

Use `OrdRejReason` values only for semantics supported by FIX 4.4; do not invent custom meanings. Likely values are:

- `0` Broker/Exchange option for unsupported or invalid business values.
- `1` Unknown symbol.
- `6` Duplicate order.
- `11` Unsupported order characteristic for an invalid type.

The domain text in tag 58 must explain the actual rule, such as exposure limit exceeded or field format invalid.

Only a protocol-valid order that fails a business rule or the exposure limit receives a business-rejected ExecutionReport. A malformed or protocol-incompatible message is handled through the applicable FIX 4.4 Session Reject or Business Message Reject where appropriate; the engine may reject it before application processing. A disconnect, timeout or unavailable session is a communication failure or unknown outcome, not a business rejection.

### FIX semantics

This assignment accepts an order and, in the simplified model, immediately applies the order notional to exposure. This is not a real trade execution or market fill. The plan distinguishes:

- **Acceptance**: order passes validation and exposure limit.
- **Market execution**: a separate event that this challenge does not implement.
- **Exposure update**: accepted notional is atomically added to or subtracted from symbol exposure exactly once.

The `New` report remains unexecuted even though the full submitted notional contributes to challenge exposure.

## 5. Financial Exposure Design

- Represent monetary values as `decimal`, not `float` or `double`; represent integral `OrderQty` as `int`.
- Validate the numeric price with `price > 0m`, `price < 1000m`, and `price % 0.01m == 0m`, not the textual scale. For example, `1.100` is valid.
- Compute notional as `quantity * price`. The maximum valid notional is below `100000 * 1000 = 100,000,000` per individual order. Use `decimal`.
- Store exposure as `Dictionary<Symbol, decimal>` with a starting value of zero.
- The exposure limit is the domain constant `100_000_000m`, not an option or environment setting.
- Buy contribution is positive: `exposure + quantity * price`.
- Sell contribution is negative: `exposure - quantity * price`.
- Reject when `Math.Abs(candidateExposure) > limit`.
- Equal to limit remains accepted.
- Keep exposure independent per symbol.
- Reject orders do not call the exposure update operation.

A single lock protects the entire in-memory state, including all symbol exposures and the global `ClOrdID` result registry. Use an unpublished copy-on-write snapshot containing both. Never mutate the published snapshot. The single assignment of the completed snapshot under the lock is the commit point:

```text
acquire global lock
  read published state
  if ClOrdID is known:
    return the original result for identical content, otherwise return a conflict
  validate the order and projected exposure
  prepare the accepted or rejected result and a new unpublished snapshot
  for acceptance, put the candidate exposure in the new snapshot
  put the original content and result in the new snapshot
  publish the complete snapshot with one state reference assignment
release lock
send the response outside the lock
```

Store the identifying order content and original accepted or rejected result with each seen `ClOrdID` in the same in-memory state. Identical repeats reproduce that result; conflicting reuse is rejected without changing the original state. Compare prices by numeric value, so `1.10` and `1.100` are equal, and do not compare FIX transport metadata as order content. Concurrent duplicates must yield only one financial transition, even across symbols.

All fallible preparation, allocation and business-result construction required to commit the snapshot happen before publication. A pre-commit exception discards the candidate snapshot and leaves both exposure and identity history unchanged; no success or business rejection is invented for the technical failure. Report construction or transmission after publication can still fail: the outcome is then unknown to the caller, and retrying the same identifier and content returns the committed result without another transition. The global lock serializes even unrelated symbols and snapshot copying costs time and memory, acceptable for this single-instance challenge. No network or other unrelated I/O occurs while holding the lock. A process restart still loses this in-memory state.

For a restart, in-memory exposure and duplicate state are lost unless FIX session recovery and an explicit persisted store are added. A database is not recommended for the assignment; document this restart limitation and the trade-off.

## 6. Concurrency and Consistency

- Serialize exposure validation and mutation in one application-service method.
- Never use a read-then-write pair outside the lock.
- Do not maintain a separate accepted-order counter or exposure cache that can diverge.
- Confirm with stress tests that simultaneous buy/sell orders cannot bypass the limit.
- If multiple accumulator instances are introduced later, the in-memory lock is not distributed. Keep the deployment as a single acceptor instance; document that Redis/Kafka/database is not being used.
- Fix the same quantity/price conversion and order parsing before the lock.
- Duplicate checking is performed under the lock and before notional calculation.
- `ClOrdID` is unique per original submit. A newly generated ExecutionReport has a unique `ExecID`; any protocol-level retransmission follows FIX resend semantics.
- `OrderID` may be random/server-generated or sequential under concurrency, but must remain stable for the original order and uniquely correlate to the request.

## 7. Web/API Correlation and Failure Handling

The browser flow should be:

1. Razor/MVC validates form fields.
2. `OrderSubmissionService` creates `ClOrdID = GUID` or timestamp-safe server ID.
3. The service registers a `TaskCompletionSource` for a FIX-neutral order result in `PendingOrderRegistry` under `ClOrdID`.
4. The FIX initiator sends `NewOrderSingle`.
5. `ExecutionReport` callbacks normalize tag 11 `ClOrdID`, find the pending request, and complete it.
6. The web request awaits the response or a configurable timeout.
7. The UI renders accepted/rejected separately and includes relevant report fields.

Asynchronously await the correlated response with a bounded timeout. The pending registry must handle disconnects and concurrent submissions, support one completion per `ClOrdID`, and remove entries after completion or timeout. A separate polling or server-push flow is optional only if a concrete requirement justifies it.

Failure handling:

- Disconnect: mark pending submissions as unavailable and log the session ID.
- `Logon`: mark FIX connectivity available.
- `Logout`: mark unavailable and leave recovery to QuickFIX/n.
- Timeout: report a timeout, keep no false accepted/rejected status, and avoid accidentally reporting a late ExecutionReport as a new browser response.
- Communication exception: return a clear backend error and structured log.
- Unknown or duplicate report: log and drop safely.

## 8. QuickFIX/n Processing

- Implement `IApplication` for session lifecycle callbacks and inherit `MessageCracker` for typed FIX 4.4 callbacks.
- Use QuickFIX/n typed message classes for `NewOrderSingle` and `ExecutionReport`.
- The acceptor `FromApp` callback gets the order and posts it to the application service.
- Send reports through `Session.SendToTarget(report, sessionID)`.
- Implement `ToApp`, `FromAdmin`, `ToAdmin`, `OnCreate`, `OnLogon`, `OnLogout`, and session callbacks needed for logging and pending state.
- Add FIX 4.4 data dictionary validation with appropriate message and field validation enabled.
- Configure replay/store directories and runtime logs outside source control.
- Add host shutdown handling to stop the initiator/acceptor cleanly.
- Do not hard-code session IDs or counters.

Official references:

- [QuickFIX/n project](https://quickfixengine.org/n)
- [QuickFIX/n example applications](https://github.com/connamara/quickfixn/tree/master/Examples)
- [QuickFIX/n configuration tutorial](http://quickfixn.org/tutorial/configuration)
- [QuickFIX/n receiving-messages tutorial](http://quickfixn.org/tutorial/receiving-messages)
- [FIX 4.4 specification reference](https://www.fixtrading.org/standings/fix-standard/)

## 9. Persistence and Restart Behavior

The plan uses in-memory storage only. No database or external persistence mechanism will be introduced.

### In memory

- Exposure per symbol.
- Seen `ClOrdID` values and their outcomes.
- Pending web submissions.
- Accepted order history needed only for duplicate detection.

### QuickFIX/n persistence

- Session message stores and sequence numbers must remain between restarts for reliability.
- FIX store/log directories must be configured and excluded from source control.
- Recovery and sequence mismatch behavior must be tested.

### Trade-offs

- Restarting the accumulator resets exposure and duplicate history.
- Restarting the generator may leave pending browser submissions unresolved.
- Reused sequence numbers or mismatched stores can prevent logon.
- This loss of state is acceptable for the technical challenge and will be documented as a known limitation.
- A database or durable snapshot will not be added unless restart continuity is introduced as a later requirement.

## 10. Detailed Phased Execution Plan

The phases below are planned, not implemented. Run focused tests during each implementation phase; phase 7 adds cross-component evidence rather than deferring all testing.

### Phase 0 - Repository and requirements inventory

**Dependencies:** None.

**Deliverables:** Current repository and SDK inventory, challenge requirements matrix, assumptions and unresolved risks. Verify existing files and preserve user changes.

**Acceptance:** Requirements and actual repository state are documented without presenting planned projects as implemented.

### Phase 1 - Architecture and technical decisions

**Dependencies:** Phase 0.

**Deliverables:** Approved boundaries, justified project structure and contracts, FIX 4.4 roles and package/dictionary decisions, single-lock snapshot transition, error classifications and in-memory restart limitations. Decide optional SDK pinning and shared projects only when justified.

**Acceptance:** Dependencies are acyclic; technical decisions preserve the challenge rules without unnecessary infrastructure.

### Phase 2 - Detailed implementation planning

**Dependencies:** Phase 1.

**Deliverables:** Ordered work packages, ownership, explicit dependencies, test matrix and measurable acceptance criteria for phases 3-8.

**Acceptance:** Each task has a verifiable deliverable and does not require work from a later phase.

### Phase 3 - OrderAccumulator foundation and FIX Acceptor infrastructure

**Dependencies:** Phase 2.

**Deliverables:** Buildable .NET 10 accumulator host, dependency injection, configuration and logging, QuickFIX/n Acceptor lifecycle, FIX 4.4 dictionary, isolated session store/log paths, example configuration and focused host/session tests. Validate package compatibility and dictionary licensing.

**Acceptance:** The host starts and stops cleanly and the Acceptor can establish a session with an isolated test peer. It does not yet implement complete order processing, financial decisions or ExecutionReports.

### Phase 4 - Financial domain and atomic state

**Dependencies:** Phase 2. Phase 3 supplies the host; domain development may proceed independently of its session implementation.

**Deliverables:** FIX-neutral contracts, numeric validation, signed per-symbol exposure, global identity and original-result registry, one global lock and single copy-on-write snapshot publication. Add deterministic boundary, idempotency, concurrency and injected-failure tests.

**Acceptance:** An accepted order applies its full signed notional once; a business rejection leaves exposure unchanged and is retained for replay; conflicts preserve the original state. Every failure before publication leaves the full published state unchanged; a failure after publication is an unknown outcome and an identical retry reproduces the committed result.

### Phase 5 - NewOrderSingle processing and ExecutionReports

**Dependencies:** Phases 3 and 4.

**Deliverables:** Typed FIX 4.4 message validation and mapping, domain service invocation, unique identifiers, accepted and business-rejected ExecutionReports, protocol-level error mapping and focused integration tests.

**Acceptance:** Valid protocol orders invoke the domain; business rejections produce `ExecType=8`, `OrdStatus=8`, reason and text. Malformed or incompatible FIX messages use the applicable session or business-message rejection mechanism, not an indiscriminate order ExecutionReport. Accepted reports are New with `CumQty=0` and `LeavesQty=OrderQty`.

### Phase 6 - OrderGenerator, frontend and FIX Initiator

**Dependencies:** Phases 2 and 5.

**Deliverables:** ASP.NET Core form and server validation, typed Initiator/session lifecycle, server-generated `ClOrdID`, thread-safe pending registry, report correlation, timeout/disconnect states and focused web/initiator tests.

**Acceptance:** A valid browser submission sends a FIX order and displays its correlated result or an explicit unknown/communication failure; independent submissions have distinct identifiers. Isolated local development may use HTTP loopback with non-sensitive data; network-accessible environments and production require HTTPS, and credentials or sensitive information must never be sent over HTTP.

### Phase 7 - End-to-end integration and tests

**Dependencies:** Phases 5 and 6.

**Deliverables:** Isolated real QuickFIX/n session, browser-to-accumulator tests, business and protocol rejection tests, duplicate/conflict, fault injection, concurrency, timeout, disconnect/reconnect and sequence-store tests.

**Acceptance:** The full matrix passes with measured results; accepted and rejected reports, exposure and failure states match the business and FIX semantics. Test ports, stores and synchronization are isolated and bounded.

### Phase 8 - Security, quality, documentation and final validation

**Dependencies:** Phase 7.

**Deliverables:** Security and code reviews, build/test evidence, verified README setup and run instructions, FIX configuration and recovery limitations, `.gitignore` review and Coodesh attribution.

**Acceptance:** A clean environment can execute the documented commands; required tests pass and outstanding risks or unavailable checks are explicitly reported. No secrets or runtime data are committed.

## 11. Testing Matrix

### Unit scenarios

| Area | Inputs | Expected result |
|---|---|---|
| Symbol | PETR4 | Valid |
| Symbol | VALE3 | Valid |
| Symbol | VIIA4 | Valid |
| Symbol | OTHER | Reject unknown symbol |
| Quantity | 1 | Valid |
| Quantity | 99999 | Valid |
| Quantity | 0 | Reject |
| Quantity | -1 | Reject |
| Quantity | 100000 | Reject |
| Price | 0.01 | Valid |
| Price | 999.99 | Valid |
| Price | 1000.00 | Reject |
| Price | 0.001 | Reject precision |
| Price | 1.1, 1.10, 1.100, 1.1000 | Accept; each is numerically a multiple of 0.01 |
| Price | 1.101, 1.105 | Reject; not multiples of 0.01 |
| Side | 1 Buy | Positive contribution |
| Side | 2 Sell | Negative contribution |

### Exposure scenarios

| Initial/scope | Order | Expected exposure |
|---|---|---|
| PETR4 0 | Buy 1000 @ 100.00 | +100000 |
| PETR4 +100000 | Sell 1000 @ 100.00 | 0 |
| PETR4 0 | Sell 10000 @ 100.00 | -1000000 |
| PETR4 0 | Buy 50000 @ 1000? | Reject, but price must be less than 1000 |
| PETR4 +99,999,000 | Buy 100 @ 10.00 | +100,000,000 accepted |
| PETR4 +99,999,000 | Buy 101 @ 10.00 | Reject; candidate `100,000,010` |
| PETR4 -99,999,000 | Sell 100 @ 10.00 | -100,000,000 accepted |
| PETR4 -99,999,000 | Sell 101 @ 10.00 | -100,000,010 reject |
| Any symbol | Repeated accepted ClOrdID | No exposure change |
| VALE3 20m | PETR4 buy 20m | Independent and accepted |

Use deterministic decimal tests to verify exact notional arithmetic without rejecting numerically valid values for their textual scale.

### Concurrency scenarios

1. 100 simultaneous buy orders for PETR4 with individually positive notional.
2. 100 simultaneous sell orders for VALE3.
3. 100 mixed buy/sell orders for VIIA4.
4. Orders for three different symbols, all at the limit boundary.
5. 1000 independent ClOrdID values, including exact-limit and over-limit candidates.
6. Duplicate delivery of the same ClOrdID while concurrent exposure changes occur.
7. Verify exposure never exceeds `100000000` in absolute terms after every operation.
8. Inject failures before exposure preparation, during processing and immediately before result registration or snapshot publication; assert the complete published exposure and identity state remains unchanged.
9. Retry after each pre-commit failure and verify one decision and at most one financial transition. Simulate a lost response after publication and verify an identical retry reproduces the committed result.
10. Race identical and conflicting identifiers across symbols, including invalid symbols, and assert one committed original decision.

### FIX integration scenarios

- Initiator and acceptor establish a FIX 4.4 session.
- Valid NewOrderSingle produces ExecType New and correlated ClOrdID.
- Over-limit NewOrderSingle produces ExecType Rejected and OrdRejReason/text.
- Protocol-valid orders with unsupported symbols or business-invalid side, quantity, or price produce business-rejected ExecutionReports; protocol-invalid field representations are handled at the protocol boundary.
- An identical repeated ClOrdID reproduces the original outcome with no additional exposure change; different order content is rejected as a conflict.
- Session logout/reconnect and sequence recovery are tested.
- Malformed FIX messages or missing mandatory protocol fields use the applicable FIX/session rejection mechanism without financial state mutation; do not require an order ExecutionReport for every protocol failure.
- Timeout, disconnection and unavailable session remain distinct from business rejection.

### End-to-end scenarios

1. Submit valid buy PETR4 order through the web form.
2. Confirm FIX transmission and accepted report.
3. Confirm UI distinguishes accepted and report fields.
4. Submit over-limit sell/buy order and confirm rejected UI state.
5. Redeliver an identical order with the same ClOrdID and confirm no additional exposure; separate browser submissions use new identifiers.
6. Stop the acceptor and submit; confirm communication state and timeout rather than false acceptance.
7. Restart the acceptor and confirm no stale exposure unless persistence/snapshot is implemented.

## 12. Architectural Risks and Mitigations

| Risk | Mitigation |
|---|---|
| Price parsing uses locale or floating point | Appropriate decimal parsing and numeric multiple-of-0.01 validation |
| Concurrent exposure updates bypass limit | Global lock and a single publication of the complete prepared snapshot |
| Duplicate FIX redelivery changes exposure twice | Commit identity, original result and exposure in the same snapshot |
| Failure between exposure and result updates | Prepare an unpublished snapshot; publish it once only after all fallible work succeeds |
| Correlation is lost or delayed | Unique ClOrdID and thread-safe pending registry |
| Late report follows timeout | Expired or unknown correlation is logged, never shown as accepted |
| Session disconnect is mistaken for rejection | Distinguish session connectivity from ExecutionReport |
| Session sequence mismatch blocks startup | Persistent store paths, documented reset procedure, recovery tests |
| Restart loses exposure | Document the in-memory limitation; do not add a database because state persistence is outside this challenge |
| QuickFIX/n field/enum misuse | Typed FIX 4.4 classes, dictionary validation, integration tests |
| Rejected order is counted as exposure | Domain result gate only updates for accepted outcomes |
| UI submit doubles an order | Prevent accidental concurrent submissions; a new original submission receives a new ClOrdID |
| Independent applications drift in validation | Share a small domain project or share validation specs tested identically |
| Overengineering | Reject database, queues, Redis, Kubernetes, and distributed patterns unless requirements change |

## 13. Definition of Done

- Both applications run on .NET 10 and communicate over FIX 4.4.
- Valid web orders become NewOrderSingle messages and receive correlated ExecutionReports.
- Accepted and rejected states are clearly distinguishable in the UI.
- All mandatory fields and enum values are mapped according to FIX 4.4.
- Each symbol maintains independent exposure using exact decimal arithmetic.
- Orders at the exposure limit are accepted; orders above the limit in absolute terms are rejected.
- Rejected orders and identical duplicate deliveries do not add another exposure effect; conflicting identifier reuse leaves original state unchanged.
- Concurrent tests prove that exposure cannot exceed the limit.
- Session disconnection, reconnection, timeout, and communication error states are handled.
- Unit, concurrency, FIX integration, and end-to-end tests pass.
- `.gitignore` excludes build, IDE, runtime store/log, and secret-like local files.
- README documents clone, restore, build, run, configure, test, and known limitations.
- No database or messaging infrastructure is introduced; all challenge state is held in memory.

## 14. Open Questions and Assumptions

### Assumptions

- A real external exchange is not involved.
- An accepted limit order contributes its full submitted notional immediately to exposure accounting, without implying market execution.
- The application runs as a single FIX acceptor instance, with no distributed deployment.
- In-memory exposure and duplicate state may be reset on restart, by technical challenge requirement.
- FIX sequence stores persist locally and are reset explicitly for test environments.
- Razor Pages or MVC is preferred over a JavaScript SPA for simplicity.
- The application should expose one HTTP submission endpoint and one fixed web form.
- HTTP is permitted only for isolated local development with non-sensitive data; network-accessible environments and production require HTTPS. Never transmit credentials or sensitive information over HTTP.

### Open questions

- Is there a required host and port, or are configurable loopback settings acceptable?
- Which `OrdRejReason` should be assigned for business-rule rejections that have no exact FIX enum? Tag 58 will carry the unambiguous text.
- Is a sequential `OrderID` acceptable, or must it be random/GUID-style?
- Is the FIX 4.4 data dictionary bundled or should QuickFIX/n package defaults be used?
- Does the evaluator expect the applications to use a specific frontend or process launch convention?
