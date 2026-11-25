---
name: fix44-integration
description: Specialized guidance for implementing, configuring, testing and reviewing FIX Protocol 4.4 integrations using QuickFIX/n in .NET applications, including Initiator and Acceptor roles, session management, message construction, ExecutionReports, asynchronous correlation, reliability, idempotency and protocol compliance.
---

# Fix44 Integration

## Purpose

Guide QuickFIX/n FIX 4.4 integration and protocol review.

## When to use

Use for Initiator/Acceptor sessions, NewOrderSingle, ExecutionReport, correlation or recovery.

## Essential project rules

- Use FIX 4.4 and typed QuickFIX/n messages; do not leak FIX types into the domain.
- Acceptance remains New with `CumQty=0` and `LeavesQty=OrderQty`; exposure accounting is not market execution.
- Business-invalid, protocol-valid orders receive rejected ExecutionReports; malformed messages use applicable FIX protocol rejects. Timeout and disconnection are not business rejections.

Read the [repository instructions](../../copilot-instructions.md), applicable path-specific instructions and the [approved execution plan](../../docs/plan.md).

## Procedure

Verify typed FIX 4.4 messages, session configuration and actual library APIs; keep handlers thin, register pending requests before sending, classify failures correctly and test real isolated sessions.

## Detailed references

Read the relevant reference files below when the task needs their detailed rules, examples, checklists or test matrices. They are not loaded automatically.

- [Purpose to 4. FIX Session Configuration](references/01-purpose.md)
- [5. QuickFIX/n Application Lifecycle to 9. Message Correlation](references/02-5-quickfix-n-application-lifecycle.md)
- [10. Duplicate Messages and Idempotency to 16. Testing Strategy](references/03-10-duplicate-messages-and-idempotency.md)
- [17. Common FIX Integration Errors to 20. Skill Execution Guidelines](references/04-17-common-fix-integration-errors.md)
