# Financial Exposure

## Purpose

Provide specialized guidance for implementing and reviewing financial exposure calculations and order processing.

Use this skill when:

- Implementing exposure calculations.
- Processing Buy and Sell orders.
- Validating financial order limits.
- Managing exposure per financial instrument.
- Handling concurrent financial operations.
- Implementing idempotent order processing.
- Reviewing financial consistency.
- Defining order acceptance and execution semantics.
- Testing financial business rules.
- Investigating exposure calculation defects.

The primary objective is to preserve financial correctness under normal, concurrent, duplicated and failure-prone processing conditions.

---

## 1. Financial Domain Context

The Coodesh OrderProcessing challenge defines two applications:

- **OrderGenerator:** submits financial orders through FIX Protocol 4.4.
- **OrderAccumulator:** receives orders, validates business rules, maintains exposure and generates ExecutionReports.

Supported financial instruments:

- PETR4
- VALE3
- VIIA4

Supported order sides:

- Buy
- Sell

Order constraints:

| Field | Requirement |
|---|---|
| Symbol | PETR4, VALE3 or VIIA4 |
| Side | Buy or Sell |
| Quantity | Positive integer, maximum 99,999 |
| Price | Positive decimal, strictly below 1,000 |
| Price increment | Numeric multiple of 0.01; trailing zeroes are allowed |

The maximum absolute exposure per symbol is:

`100_000_000.00m` — a domain constant, not an external configuration value.

The exposure limit is inclusive.

---

## 2. Exposure Formula

For this challenge, each accepted order immediately contributes its full notional:

`Exposure = Sum(accepted Buy Price × OrderQty) - Sum(accepted Sell Price × OrderQty)`

For each symbol:

`Exposure = Buy Notional - Sell Notional`

Where:

`Notional = Price × OrderQty`

This is an accounting simplification, not a market execution; no market execution is implemented.

Interpretation:

- Buy orders increase exposure.
- Sell orders decrease exposure.
- Exposure can be positive, negative or zero.
- Each financial instrument has independent exposure.
- Rejected orders must not affect exposure.

### Example

Assume the following operations for PETR4:

| Operation | Side | Price | OrderQty | Impact |
|---|---|---:|---:|---:|
| Order 1 | Buy | 35.00 | 1,000 | +35,000 |
| Order 2 | Buy | 36.00 | 500 | +18,000 |
| Order 3 | Sell | 37.00 | 600 | -22,200 |

Result:

`Exposure = 35,000 + 18,000 - 22,200`

`Exposure = 30,800`

The resulting exposure for PETR4 is R$30,800.

Do not combine exposure across different symbols.

---

## 3. Monetary Precision

Financial calculations must use C# `decimal`.

Never use:

- `double` for monetary values.
- `float` for monetary values.
- Binary floating-point arithmetic for exposure calculations.

Example:

    decimal price = 35.75m;
    decimal quantity = 1000m;
    decimal notional = price * quantity;

The `m` suffix explicitly identifies a decimal literal.

### Precision Rules

- Preserve the numeric price value; trailing zeroes do not invalidate it.
- Avoid unnecessary intermediate rounding.
- Do not convert financial values to floating-point types.
- Use invariant culture when parsing or formatting protocol values.
- Ensure FIX decimal serialization preserves the intended monetary value.
- Define rounding behavior explicitly whenever calculations require rounding.

### Validation

A price must satisfy:

`Price > 0`

`Price < 1000`

`Price % 0.01m == 0`

For example, `1.10`, `1.100` and `1.1000` are valid; `1.101` and `1.105` are not.

The quantity must satisfy:

`Quantity >= 1`

`Quantity <= 99999`

Validation must occur before exposure mutation; business-rejected results are retained.

Do not rely exclusively on frontend validation.

---

## 4. Exposure Limit Enforcement

The maximum absolute exposure is:

`|Exposure| <= 100,000,000`

Valid boundaries:

- `+100,000,000`: accepted.
- `-100,000,000`: accepted.

Invalid boundaries:

- `+100,000,000.01`: rejected.
- `-100,000,000.01`: rejected.

### Projected Exposure

Never validate an order only against the current exposure.

Calculate the projected exposure before accepting the order.

For an order under the challenge accounting rule:

`Projected Exposure = Current Exposure + Order Impact`

Where:

- Buy impact is positive.
- Sell impact is negative.

Acceptance condition:

`Abs(Projected Exposure) <= 100,000,000`

If the projected exposure exceeds the limit, reject the order without modifying exposure and retain the business-rejected result.

### Example

Current PETR4 exposure:

`99,900,000`

Incoming order:

- Side: Buy
- Price: 50.00
- Quantity: 3,000

Order impact:

`50 × 3,000 = 150,000`

Projected exposure:

`99,900,000 + 150,000 = 100,050,000`

Result: reject the order.

The current exposure must remain R$99,900,000.

---

## 5. Acceptance Versus Execution

Business acceptance immediately and atomically adds the full signed `Price × OrderQty` to exposure. This challenge-specific accounting rule does not represent a market trade; the challenge does not implement market execution.

The accepted order's FIX report remains `ExecType = 0` (New), `OrdStatus = 0` (New), `CumQty = 0` and `LeavesQty = OrderQty`. Never infer execution from the exposure update.

---

