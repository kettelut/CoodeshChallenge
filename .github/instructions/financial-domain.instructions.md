---
applyTo: "**/*.cs"
---

# Financial Domain Instructions

## 1. Purpose and Scope

These instructions define the financial domain rules and implementation standards for the OrderProcessing challenge.

The solution processes financial orders through two applications:

- **OrderGenerator:** collects and submits financial orders through FIX Protocol 4.4.
- **OrderAccumulator:** receives orders, validates business rules, manages exposure and returns processing results.

Financial correctness is a primary requirement. Every implementation must preserve monetary precision, exposure consistency, order integrity and deterministic business behavior.

Do not introduce financial behavior that is not supported by the challenge requirements or an explicitly approved architectural decision.

## 2. Supported Financial Instruments

The solution supports exactly these symbols:

- `PETR4`
- `VALE3`
- `VIIA4`

Symbol validation must be explicit.

Requirements:

- Reject unsupported symbols.
- Do not silently replace or normalize an unsupported symbol.
- Use a consistent representation across HTTP, application, domain and FIX boundaries.
- Keep exposure independent for each symbol.

Do not assume that all instruments have identical business rules beyond those explicitly defined by the challenge.

## 3. Order Structure

An order must contain the following information:

| Field | Description |
|---|---|
| ClOrdID | Unique client order identifier |
| Symbol | Financial instrument |
| Side | Buy or Sell |
| Quantity | Requested order quantity |
| Price | Limit price |
| TransactTime | Order transaction timestamp |

The domain must represent these values using appropriate strongly typed properties.

Avoid passing unvalidated primitive values throughout the application.

## 4. Order Side

The supported order sides are:

- `Buy`
- `Sell`

FIX representation:

| Domain value | FIX Side (54) |
|---|---:|
| Buy | 1 |
| Sell | 2 |

Side must be validated before financial processing.

Do not treat an unknown or missing side as Buy or Sell by default.

## 5. Quantity Rules

Order quantity must satisfy:

- Integer value.
- Greater than zero.
- Strictly less than 100,000.

Therefore, the valid interval is:

`1 <= Quantity <= 99,999`

Reject:

- Zero.
- Negative values.
- Fractional quantities.
- Values equal to or greater than 100,000.
- Values that cannot be represented safely by the domain type.

Use an appropriate integral type.

Do not use decimal or floating-point types for quantities unless a future business requirement explicitly introduces fractional units.

## 6. Price Rules

Order price must satisfy:

- Greater than zero.
- Strictly less than 1,000.
- Multiple of 0.01.

Therefore:

`0 < Price < 1000`

Validate the numeric decimal value with `Price % 0.01m == 0`, not the number of characters after the decimal separator.

Valid examples:

- `0.01`
- `1.10`, `1.100` and `1.1000`
- `10.50`
- `999.99`

Invalid examples:

- `0`
- `-10.00`
- `1000.00`
- `1.101` and `1.105`
- `10.005`

Use `decimal` for price representation and validation.

Do not use `double` or `float`.

Do not round invalid input silently to make it valid.

## 7. Monetary Precision

All financial calculations must use C# `decimal`.

This applies to:

- Order price.
- Order notional value.
- Exposure.
- Projected exposure.
- Exposure limits.
- Financial comparisons.

Never use binary floating-point arithmetic for financial calculations.

Avoid implicit conversions between decimal and floating-point types.

Use explicit decimal literals where appropriate.

Example:

`decimal maximumExposure = 100000000.00m;`

Avoid culture-dependent parsing and formatting.

Use invariant or explicitly configured culture handling at system boundaries.

Do not apply rounding unless a business rule explicitly requires it.

## 8. Order Notional Value

The notional value of an order is:

`OrderNotional = Price × Quantity`

Use decimal arithmetic.

Example:

- Price: R$ 25.50.
- Quantity: 1,000.
- Notional: R$ 25,500.00.

The calculation must not lose precision.

Each accepted order contributes its full notional to exposure immediately. This accounting is not an actual market execution.

## 9. Exposure Definition

Exposure is maintained independently for each symbol.

The challenge formula is:

`Exposure = Sum(Buy Price × accepted OrderQty) - Sum(Sell Price × accepted OrderQty)`

Interpretation:

- Buy operations increase exposure.
- Sell operations decrease exposure.
- The full accepted order quantity determines the challenge accounting impact, without implying a market execution.
- Each symbol has an independent exposure balance.

Example:

| Operation | Price | Quantity | Exposure impact |
|---|---:|---:|---:|
| Buy PETR4 | R$ 10.00 | 1,000 | +R$ 10,000.00 |
| Buy PETR4 | R$ 12.00 | 500 | +R$ 6,000.00 |
| Sell PETR4 | R$ 11.00 | 200 | -R$ 2,200.00 |

Result:

`Exposure = 10000 + 6000 - 2200 = R$ 13,800.00`

A transaction involving VALE3 must not modify PETR4 exposure.

## 10. Exposure Limit

The maximum absolute exposure per symbol is:

`100000000.00m` (R$ 100,000,000.00), a fixed domain constant that must not be configurable.

The valid exposure interval is inclusive:

`-100000000.00 <= Exposure <= 100000000.00`

Examples:

| Projected exposure | Result |
|---:|---|
| R$ 99,999,999.99 | Accept |
| R$ 100,000,000.00 | Accept |
| R$ 100,000,000.01 | Reject |
| -R$ 99,999,999.99 | Accept |
| -R$ 100,000,000.00 | Accept |
| -R$ 100,000,000.01 | Reject |

The validation must use the absolute value of the projected exposure.

Do not reject an exposure that is exactly equal to either limit.

## 11. Projected Exposure Validation

Before modifying exposure, calculate the projected exposure.

For a Buy operation:

`ProjectedExposure = CurrentExposure + OrderFinancialImpact`

For a Sell operation:

`ProjectedExposure = CurrentExposure - OrderFinancialImpact`

Where:

`OrderFinancialImpact = Price × Quantity`

The order may be accepted only if:

`Abs(ProjectedExposure) <= MaximumExposure`

The projected exposure must use the full accepted order quantity.

Do not modify current exposure before validation succeeds.

## 12. Acceptance Versus Execution

Order acceptance and order execution are different financial lifecycle events.

The challenge requires:

- Accepted order: `ExecType (150) = 0` — New.
- Rejected order: `ExecType (150) = 8` — Rejected.

For this challenge, acceptance atomically updates exposure using the full order quantity and submitted price. This accounting simplification is not a market execution, and the challenge does not implement one.

An accepted unexecuted order has `ExecType=0`, `OrdStatus=0`, `CumQty=0` and `LeavesQty=OrderQty`. Do not report a fill or imply a trade.

## 13. Atomicity and Concurrency

Global `ClOrdID` identity and original-result registration must be coordinated atomically with per-symbol exposure validation and modification.

The following sequence must be protected as one logical operation:

1. Coordinate the global `ClOrdID` and check its existing content and result.
2. Identify the symbol and retrieve its current exposure.
3. Calculate and validate projected exposure.
4. Commit the accepted operation, exposure update and original result together; preserve a business rejection result consistently without changing exposure.

Two concurrent orders for the same symbol must not independently validate against the same outdated exposure and both be accepted when their combined impact exceeds the limit.

Requirements:

- Use one global lock for identity and all symbols. Prepare an unpublished copy-on-write snapshot of exposure and original results and publish it with one state reference assignment; never mutate a published snapshot.
- Accept serialization of different symbols and snapshot-copying costs for this single-instance challenge.
- Do not hold locks during unrelated network or I/O operations.
- Ensure any failure before publication leaves the complete state unchanged. A failure after publication may leave the caller with an unknown outcome; an identical retry returns the committed result.
- The same identifier arriving concurrently with different symbols must not pass separate per-symbol identity checks; per-symbol locks alone are insufficient.

A thread-safe dictionary alone does not guarantee atomicity across the complete financial operation.

## 14. Idempotency

Each order must be processed at most once for financial exposure purposes.

Use `ClOrdID (11)` as the primary client order identifier.

Requirements:

- Detect previously processed order identifiers.
- Prevent duplicate exposure updates.
- Handle concurrent duplicate submissions safely.
- Ensure retransmissions do not create additional financial effects.
- Return or reproduce the original accepted or rejected result for identical content under the same `ClOrdID`.
- Reject reuse with different content as a conflict without changing the original order or exposure.
- Define the compared order content consistently and compare decimal prices numerically: `1.10` and `1.1000` alone are not conflicting content.
- Apply the same rules concurrently, allowing at most one financial transition.
- Coordinate duplicate detection with exposure updates.

Do not rely exclusively on FIX session-level duplicate detection.

A duplicate request must never increase or decrease exposure a second time.

## 15. Order Validation and Rejection

An order must be rejected when it violates any required business rule.

Possible rejection reasons include:

- Unsupported symbol.
- Invalid side.
- Invalid quantity.
- Invalid price.
- Exposure limit exceeded.
- Conflicting reuse of an identifier with different order content.

Validation should occur before committing financial state.

Rejection requirements:

- Return an explicit business rejection result.
- Include a meaningful rejection reason.
- Do not modify exposure.
- Do not partially register an accepted order.
- Preserve the previous exposure state.
- Generate a FIX Rejected ExecutionReport when the request is processed through FIX.

Do not use infrastructure exceptions as substitutes for normal business rejection.

## 16. Financial State Consistency

The financial state must remain internally consistent.

At minimum, the implementation must maintain a reliable relationship between:

- Processed orders.
- Order statuses.
- Exposure balances.
- Duplicate detection records.

A failure during processing must not leave exposure modified without a corresponding accepted operation.

For persistent implementations, use transactions or equivalent consistency guarantees.

For in-memory implementations, ensure the operation is protected by the appropriate synchronization mechanism and does not expose partially updated state.

## 17. Symbol Isolation

Exposure must be isolated by symbol.

Example:

| Symbol | Exposure |
|---|---:|
| PETR4 | R$ 50,000.00 |
| VALE3 | -R$ 20,000.00 |
| VIIA4 | R$ 10,000.00 |

A Buy or Sell order for one symbol must not affect the exposure of another symbol.

Concurrency control should also respect symbol isolation.

Do not use a single undifferentiated exposure balance for all instruments.

## 18. Error and Result Semantics

Financial processing must distinguish between:

- Accepted.
- Rejected.
- Pending.
- Unknown outcome.
- Technical failure.

A timeout or FIX disconnection does not prove that an order was rejected.

An unknown outcome must not be treated as a successful operation or automatically resubmitted.

The application must preserve enough correlation information to reconcile the order status when communication is restored.

Business results should be explicit and should not depend on parsing exception messages.

## 19. FIX Domain Mapping

The domain must not depend on FIX tag numbers or QuickFIX/n classes.

The integration layer is responsible for mapping between FIX messages and domain/application representations.

Relevant NewOrderSingle fields:

| Field | FIX Tag | Meaning |
|---|---:|---|
| ClOrdID | 11 | Client order identifier |
| Symbol | 55 | Financial instrument |
| Side | 54 | Buy or Sell |
| OrderQty | 38 | Requested quantity |
| OrdType | 40 | Order type |
| Price | 44 | Limit price |
| TransactTime | 60 | Transaction timestamp |

Relevant ExecutionReport fields:

| Field | FIX Tag | Meaning |
|---|---:|---|
| OrderID | 37 | Order identifier |
| ExecID | 17 | Execution report identifier |
| ClOrdID | 11 | Client order identifier |
| ExecType | 150 | Execution report type |
| OrdStatus | 39 | Current order status |
| CumQty | 14 | Cumulative executed quantity |
| LeavesQty | 151 | Remaining quantity |
| AvgPx | 6 | Average execution price |
| OrdRejReason | 103 | Rejection reason |
| Text | 58 | Additional information |

Translate FIX values into validated application types before executing business rules.

## 20. Persistence Strategy

The persistence strategy must be explicit.

If in-memory state is used:

- Exposure is lost when the process restarts.
- Processed order history is not durable unless separately persisted.
- Idempotency records are lost unless separately persisted.
- Multiple application instances do not share financial state.
- The solution does not provide distributed consistency.

These limitations must be documented.

If persistence is introduced, ensure that order registration, idempotency and exposure updates are transactionally consistent.

Do not introduce a database or distributed infrastructure without a concrete requirement.

## 21. Domain Modeling

Use domain concepts that express financial behavior.

Potential domain types include:

- `Order`.
- `OrderSide`.
- `OrderStatus`.
- `OrderIdentifier`.
- `InstrumentSymbol`.
- `OrderQuantity`.
- `Money`.
- `Exposure`.
- `ExposureLimit`.
- `OrderAcceptanceResult`.
- `OrderRejectionReason`.

These are suggestions, not mandatory classes.

Create value objects when they enforce meaningful invariants or prevent invalid states.

Avoid creating abstractions that only wrap primitive types without providing additional guarantees.

Domain behavior must remain independent of:

- QuickFIX/n.
- ASP.NET Core.
- HTTP contracts.
- Database frameworks.
- Infrastructure services.

## 22. Testing Requirements

Financial domain tests are mandatory for critical business behavior.

### 22.1 Order Validation

Test:

- Every supported symbol.
- Unsupported symbols.
- Buy and Sell.
- Zero quantity.
- Negative quantity.
- Maximum valid quantity.
- Quantity equal to 100,000.
- Zero price.
- Negative price.
- Price equal to 1,000.
- Valid cent precision.
- Invalid fractional cent precision.
- Numerically valid `1.10`, `1.100` and `1.1000`, and invalid `1.101` and `1.105`.

### 22.2 Exposure Calculation

Test:

- Buy increases exposure.
- Sell decreases exposure.
- Multiple Buy operations.
- Multiple Sell operations.
- Mixed Buy and Sell operations.
- Independent symbols.
- Positive exposure.
- Negative exposure.
- Zero exposure.
- Decimal precision.

### 22.3 Exposure Boundaries

Test:

- Exactly positive R$100,000,000.00.
- Exactly negative R$100,000,000.00.
- R$100,000,000.01.
- -R$100,000,000.01.
- Values immediately inside both limits.
- Orders that would cross the limit.

### 22.4 Atomicity and Concurrency

Test:

- Concurrent Buy operations for the same symbol.
- Concurrent Sell operations for the same symbol.
- Concurrent operations with different symbols.
- Combined exposure exceeding the limit.
- No partial exposure update after rejection.
- Consistency between accepted orders and exposure.

### 22.5 Idempotency

Test:

- Duplicate `ClOrdID`.
- Concurrent duplicate submissions.
- Concurrent same `ClOrdID` with identical content and with conflicting content, including different symbols.
- FIX retransmission.
- Duplicate request after acceptance.
- Duplicate request after rejection.
- Equal numeric prices with different textual scale do not create an identifier conflict by themselves.
- Original result reproduced for identical content; conflicting reuse rejected without modifying original state or exposure.
- No duplicate financial impact.

### 22.6 Failure Scenarios

Test:

- Failure before exposure validation.
- Failure during processing.
- Failure before state commitment.
- FIX communication failure.
- Timeout with unknown outcome.
- Recovery and reconciliation behavior when supported.

Do not claim coverage for scenarios that have not actually been tested.

## 23. Financial Domain Anti-Patterns

Avoid:

- Using `double` or `float` for financial values.
- Rounding invalid prices silently.
- Using floating-point tolerances for exposure comparisons.
- Updating exposure before validation.
- Modifying exposure after rejection.
- Sharing exposure across different symbols.
- Treating challenge exposure accounting as a real market execution.
- Allowing concurrent operations to bypass exposure limits.
- Processing duplicate orders more than once.
- Treating timeouts as rejections.
- Coupling domain rules to FIX message types.
- Hiding business decisions inside infrastructure handlers.
- Using exceptions for expected business rejection.
- Claiming financial consistency without atomic state management.

## 24. Financial Domain Compliance Checklist

Before completing any financial domain implementation, verify:

- [ ] Only supported symbols are accepted.
- [ ] Side is explicitly validated.
- [ ] Quantity is an integer between 1 and 99,999.
- [ ] Price is greater than zero and less than 1,000.
- [ ] Price is a multiple of 0.01.
- [ ] Financial calculations use `decimal`.
- [ ] Exposure immediately accounts for each accepted order's full notional without claiming a market execution.
- [ ] Buy operations increase exposure.
- [ ] Sell operations decrease exposure.
- [ ] Exposure is independent per symbol.
- [ ] The absolute exposure limit is R$100,000,000.00.
- [ ] The exposure boundary is inclusive.
- [ ] Projected exposure is validated before commitment.
- [ ] Exposure validation and updates are atomic.
- [ ] Rejected orders do not modify exposure.
- [ ] Duplicate orders cannot create additional financial effects.
- [ ] Acceptance and execution are not confused.
- [ ] Unknown outcomes are handled explicitly.
- [ ] Financial state remains consistent after failures.
- [ ] Persistence limitations are documented.
- [ ] Automated tests cover critical financial rules.

## 25. Instructions for GitHub Copilot

When implementing or modifying financial domain code:

1. Inspect the existing domain model and application flow.
2. Read the architecture and C# instructions.
3. Follow approved financial business decisions.
4. Use `decimal` for every monetary calculation.
5. Preserve exposure atomicity.
6. Preserve idempotency guarantees.
7. Keep financial rules independent of FIX and HTTP infrastructure.
8. Never invent execution semantics.
9. Do not modify exposure after a rejected operation.
10. Add tests for every relevant financial behavior change.
11. Document assumptions and limitations.
12. Report unresolved business ambiguities before implementing unsupported behavior.

If financial requirements are ambiguous or contradictory, stop and report the issue instead of silently selecting an interpretation.
