# Order Processing System

OrderGenerator submits financial orders over FIX 4.4 to OrderAccumulator, which validates them, accounts for exposure and returns order decisions.

## Technologies and architecture

- C# and .NET 10; ASP.NET Core Razor Pages for the web form.
- QuickFIX/n 1.14.1 and FIX Protocol 4.4.
- xUnit and Microsoft.NET.Test.Sdk; ASP.NET Core MVC Testing for web integration tests.

`OrderGenerator` is the FIX initiator. Its Razor Page validates input, and its submission service generates a `ClOrdID`, sends a `NewOrderSingle` and waits for a correlated `ExecutionReport` in an in-memory pending registry. `OrderAccumulator` is the FIX acceptor: its adapter parses the order, calls the singleton `OrderProcessing.Domain` processor and sends the decision. QuickFIX/n types remain outside the domain.

```text
Browser → OrderGenerator (Razor Pages / FIX initiator)
                         │ NewOrderSingle (35=D)
                         ▼
          OrderAccumulator (FIX acceptor → domain processor)
                         │ ExecutionReport (35=8)
                         └──────────────────────→ OrderGenerator → Browser
```

The accumulator owns exposure and original order decisions in memory. Dependency injection hosts both FIX engines and the application services. There is no matching engine or market execution.

## Prerequisites and installation

Install the .NET 10 SDK and Git, and allow NuGet access for package restore. The local FIX TCP port and web port must be available.

```shell
git clone https://github.com/kettelut/CoodeshChallenge.git
cd CoodeshChallenge
dotnet restore OrderProcessing.sln
dotnet build OrderProcessing.sln --configuration Release
dotnet test OrderProcessing.sln --configuration Release
```

## Configuration

The checked-in local defaults are in `src/OrderAccumulator/appsettings.json` and `src/OrderGenerator/appsettings.json`. Both applications also accept environment variables using `__` for nested keys, for example `FixAcceptor__SocketAcceptPort` and `FixInitiator__SocketConnectPort`. Coordinate changes on both sides; do not commit secrets or environment-specific credentials.

| Setting | Acceptor: `FixAcceptor` | Initiator: `FixInitiator` |
|---|---|---|
| `SenderCompId` / `TargetCompId` | `ACCUMULATOR` / `GENERATOR` | `GENERATOR` / `ACCUMULATOR` |
| Host / port | `SocketAcceptHost=127.0.0.1`, `SocketAcceptPort=5001` | `SocketConnectHost=127.0.0.1`, `SocketConnectPort=5001` |
| `HeartBtInt` | `30` seconds | `30` seconds |
| `StartTime` / `EndTime` | `00:00:00` / `00:00:00` | `00:00:00` / `00:00:00` |
| `RuntimePath` | `runtime/order-accumulator` | `runtime/order-generator` |
| Other settings | — | `ReconnectIntervalSeconds=5`, `ResponseTimeoutSeconds=15`, `MaxPendingOrders=1000` |

These are application options used to construct the QuickFIX/n session, not a separate FIX configuration file. The code sets `BeginString=FIX.4.4`, dictionary validation and `ResetOnLogon`, `ResetOnLogout` and `ResetOnDisconnect` to `N`. IDs must be nonempty and distinct, ports must be valid, heartbeat and reconnect interval positive, and times in `HH:mm:ss` format. A nonempty runtime path is required. The initiator timeout must be 1–86,400 seconds and pending capacity positive.

The `QuickFIXn.FIX44` package supplies `DataDictionary/FIX44.xml`, copied to each application's output as `FIX44.xml`. The generator uses that output file. The accumulator generates a content-addressed `FIX44-exact-<hash>.xml` under its runtime directory and uses it as `DataDictionary`; see [FIX numeric handling](#fix-numeric-handling).

Each runtime directory contains a `store` (`FileStorePath`) and a `logs` directory (`FileLogPath`). Relative paths resolve against the application content root: the accumulator explicitly uses its output directory (`AppContext.BaseDirectory`), while the generator uses its web content root, normally `src/OrderGenerator` with the launch profile. Both engines disable QuickFIX/n full-message logging; application lifecycle, correlation and error events go to the configured .NET logging providers. Existing logs from other runs or test peers may still contain messages.

**FileStore persists FIX messages as well as sequence information and can contain sensitive data.** Restrict access to runtime stores and logs, and never publish them or commit them to source control. `.gitignore` excludes `runtime/`, `fix-store/`, `fix-logs/`, build output and several local secret-file patterns; an ignored path is not a substitute for access controls, and custom paths must also be kept out of source control.

The local defaults use loopback and non-sensitive test data only. OrderGenerator has no operator authentication or authorization: HTTPS encrypts web traffic but does not control access. The sample FIX connection has neither TLS nor FIX authentication. Do not expose the web or FIX endpoints to a network without externally provided authorized access controls and authenticated, encrypted channels. HTTP loopback is allowed only in isolated development; network-accessible environments require HTTPS. Never send credentials or sensitive data over HTTP.

## Run and submit an order

From the repository root, start the acceptor first in one terminal:

```shell
dotnet run --project src/OrderAccumulator/OrderAccumulator.csproj --launch-profile OrderAccumulator
```

Then start the generator in another terminal:

```shell
dotnet run --project src/OrderGenerator/OrderGenerator.csproj --launch-profile http-local
```

Both profiles set their respective environment to `Development`. The generator connects to `127.0.0.1:5001`; look for the application log message `FIX session logged on` on both sides before submitting. Open [http://127.0.0.1:5080](http://127.0.0.1:5080) for the Portuguese-language form. Alternatively, the `https-local` profile uses `https://127.0.0.1:7080` and requires a usable local development certificate.

1. Select a symbol and `Compra (Buy)` or `Venda (Sell)`.
2. Enter an integer quantity and a BRL price using a decimal point, for example `100` and `10.50`.
3. Select **Enviar ordem** (Submit) and wait for the result. The page shows **Ordem aceita** or **Ordem rejeitada**, identifiers and, for rejection, the reason when available.
4. For another original order, use **Criar nova ordem**. Do not refresh or resubmit the used form.

For a reproducible exposure rejection on a freshly started accumulator, submit Buy `PETR4`, quantity `99999`, price `999.99`; then create a new order and submit Buy `PETR4`, quantity `3`, price `999.99`. The first order is within the limit and the second would exceed it. Local input validation can prevent invalid fields from being sent; it is not a business rejection from the accumulator.

Timeout (**Tempo de resposta esgotado**), unavailable session, disconnect, send failure and technical failure are not accepted or rejected reports. If sending may have started, the outcome can be unknown: reconcile the original `ClOrdID` before creating another order. There is no automatic order retry. The initiator does attempt session reconnection; a late report is not turned into a new browser response. A blocked synchronous FIX send can delay the HTTP response beyond its configured deadline.

If logon fails, check both processes, matching reversed IDs, host, port and existing sequence stores. Sequence mismatches may need coordinated session recovery with the peer; do not blindly delete stores, especially when an order outcome is unknown. Stop each process with Ctrl+C.

Independent Windows validation with .NET SDK 10.0.102 executed the two launch profiles above on the default ports with isolated temporary runtime stores. It established a real FIX logon and exercised the form with cookies and antiforgery, including accepted Buy and Sell orders and an exposure rejection. This does not establish behavior in other environments or validate the alternative HTTPS profile.

## Business rules

| Field | Rule |
|---|---|
| `ClOrdID` | Nonempty, non-whitespace identifier; generated on the server for each original web submission |
| Symbol | Exactly `PETR4`, `VALE3` or `VIIA4` |
| Side | Buy or Sell |
| Quantity | Positive integer, 1 through 99,999 |
| Order type | Limit |
| Price | `0 < Price < 1000` and `Price % 0.01m == 0` |

Price validation is numeric: `1.100` and `1.1000` are valid; `1.101` and `1.105` are not. Financial calculations use `decimal`. The accumulator independently validates incoming orders.

For each symbol, the processor starts at zero and immediately accounts for each accepted order's full notional:

```text
Order value = Price × OrderQty
Exposure = Sum(accepted Buy order values) - Sum(accepted Sell order values)
```

The inclusive maximum absolute exposure is the fixed domain constant `100_000_000.00m` (R$100,000,000.00) per symbol, not configuration. A candidate outside either limit is rejected without changing exposure. For example, from `99,999,000`, Buy `100 @ 10.00` is accepted at exactly `100,000,000`; Buy `101 @ 10.00` is rejected. From `-99,999,000`, Sell `101 @ 10.00` is rejected.

An identical request with the same usable `ClOrdID` reproduces the original accepted or rejected decision and stable `OrderID` without another exposure update. Content comparison includes symbol, side, quantity, price and order type, not FIX transport metadata; numeric values compare exactly, so `1.10` and `1.100` are equal. Missing price differs from a present price. Conflicting reuse is rejected without replacing the original decision or changing exposure. A blank identifier is rejected without registering replay history. A reproduced report has a new `ExecID`.

One global lock coordinates identifier detection, content comparison, original result storage and exposure validation/update, even across symbols. The processor prepares a copy-on-write snapshot and publishes it in one assignment. Failure before publication leaves state unchanged; a lost response after publication can leave the caller uncertain, while an identical retry against the same processor reproduces the committed result. The lock serializes different symbols and snapshot copying costs time and memory.

Acceptance is challenge-specific accounting, **not** a market fill. The entire signed notional affects exposure immediately even though the order remains New with no executed quantity.

## FIX 4.4 messages

`NewOrderSingle (35=D)` includes `ClOrdID (11)`, `Symbol (55)`, `Side (54; 1=Buy, 2=Sell)`, `OrderQty (38)`, `OrdType (40; 2=Limit)`, `Price (44)` and `TransactTime (60)`.

`ExecutionReport (35=8)` includes `OrderID (37)`, `ExecID (17)`, `ClOrdID (11)`, `Symbol (55)`, `Side (54)`, `ExecType (150)`, `OrdStatus (39)`, `CumQty (14)`, `LeavesQty (151)`, `AvgPx (6)` and `TransactTime (60)`. `OrderQty (38)` and any supplied `Price (44)` are included when exactly representable; rejection also includes `OrdRejReason (103)` and `Text (58)`.

| Decision | ExecType (150) | OrdStatus (39) | CumQty (14) | LeavesQty (151) | AvgPx (6) |
|---|---:|---:|---:|---:|---:|
| Accepted as New | 0 | 0 | 0 | OrderQty | 0 |
| Rejected | 8 | 8 | 0 | 0 | 0 |

Protocol-valid business-invalid orders receive a rejected ExecutionReport. Malformed or protocol-incompatible messages instead use the applicable FIX Session Reject or Business Message Reject; they are not necessarily order rejections. The generator does not correlate protocol rejects to pending orders, so a request may time out instead of receiving a business rejection. Communication failures are distinct from both.

### FIX numeric handling

The acceptor locally changes only the dictionary types of tags 38 and 44 to `STRING`, then parses their text as exact base-10 numbers before business validation. This avoids QuickFIX/n `decimal` rounding or overflow before the domain sees the input. Malformed or overlong numeric text is a protocol error. On a business rejection, an `OrderQty` or `Price` that cannot be represented exactly as `decimal` is omitted from the report and explained in `Text`, rather than sent rounded to a standard-dictionary peer. This can affect interoperability with peers requiring those fields.

## Testing

Run the complete solution with `dotnet test OrderProcessing.sln --configuration Release`, or run either project separately:

```shell
dotnet test tests/OrderProcessing.Domain.Tests/OrderProcessing.Domain.Tests.csproj --configuration Release
dotnet test tests/OrderProcessing.Integration.Tests/OrderProcessing.Integration.Tests.csproj --configuration Release
```

- `OrderProcessing.Domain.Tests` covers validation boundaries, exact numeric handling, signed exposure, duplicates and conflicts, concurrency, pre-publication failures and a simulated lost response.
- `OrderProcessing.Integration.Tests` uses real local QuickFIX/n sessions and temporary stores for message flow, acceptance, rejection, protocol errors, exact values, reconnection and sequence persistence. Web tests use ASP.NET Core's test host and real FIX components for form-to-report flows; some isolation and callback scenarios use controlled substitutes.

Integration tests need loopback sockets and writable temporary directories, but no external FIX service. The acceptor-restart fixture retains the same in-memory processor; it does not prove financial state survives a process restart. Independent Windows validation with .NET SDK 10.0.102 reported a Release build with no warnings or errors and passing domain and integration suites using `dotnet build OrderProcessing.sln --configuration Release --no-restore` and `dotnet test OrderProcessing.sln --configuration Release --no-restore`. Automated tests do not establish correctness for every concurrent interleaving or deployment environment.

## Limitations

- Exposure and duplicate history are in memory for one accumulator instance; process restart resets both. Multiple instances do not share financial state or a distributed lock.
- FIX message and sequence persistence is separate: it neither restores financial state nor guarantees deduplication after an accumulator restart. Preserve and reconcile session stores with the peer; recovery does not resolve every unknown order outcome.
- Original `ClOrdID` decision history grows in memory for the processor's lifetime and can exhaust memory. The in-memory consumed form-token table is limited to 10,000 unexpired tokens; capacity exhaustion blocks new submissions until entries expire. Pending web requests are also in memory.
- There is no market matching or execution, automatic order retry, FIX TLS or FIX authentication. The local FIX connection is not suitable for sensitive data.

## Attribution

This is a challenge by [Coodesh](https://coodesh.com/).

This product includes software developed by quickfixengine.org (http://www.quickfixengine.org/). See `src/OrderGenerator/THIRD-PARTY-NOTICES.txt` and `src/OrderAccumulator/THIRD-PARTY-NOTICES.txt`.
