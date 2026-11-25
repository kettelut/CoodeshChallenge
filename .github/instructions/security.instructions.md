---
applyTo: "**/*.cs,**/*.csproj,**/*.json,**/*.config,**/*.yml,**/*.yaml,**/*.props,**/*.targets"
---

# Security Instructions

## 1. Security Objectives

The OrderProcessing solution must protect the confidentiality, integrity and availability of its applications and financial processing operations.

Security is especially important because the system receives financial orders through HTTP and FIX Protocol 4.4.

Prioritize:

- Input validation.
- Financial data integrity.
- Secure FIX communication.
- Protection of credentials and secrets.
- Secure application configuration.
- Safe error handling.
- Protection against unauthorized access.
- Dependency security.
- Resource exhaustion prevention.
- Secure logging and observability.

Security controls must be appropriate to the challenge scope. Do not introduce unnecessary infrastructure or complexity.

## 2. Threat Model

Consider the following potential threats:

- Unauthorized HTTP requests.
- Malformed or malicious order submissions.
- Unauthorized FIX session connections.
- Forged or replayed FIX messages.
- Duplicate financial order processing.
- Exposure manipulation through concurrent requests.
- Injection attacks.
- Credential disclosure.
- Sensitive information exposure through logs.
- Denial-of-service attempts.
- Vulnerable third-party dependencies.
- Misconfigured application environments.

Treat all external input as untrusted, including data received through HTTP, FIX messages, configuration files and environment variables.

## 3. Input Validation

Validate all external input on the server.

Order validation must enforce:

- Supported symbols only: `PETR4`, `VALE3` and `VIIA4`.
- Side must be Buy or Sell.
- Quantity must be an integer greater than zero and less than 100,000.
- Price must be greater than zero and less than 1,000.
- Price must be a multiple of 0.01.

Evaluate `price % 0.01m == 0` using `decimal`, not textual decimal-place count: `1.100` and `1.1000` are valid; `1.101` and `1.105` are not.

Requirements:

- Reject missing or malformed fields.
- Reject unsupported enum values.
- Reject unexpected data types.
- Reject invalid numeric representations.
- Enforce reasonable request and message size limits.
- Do not trust client-side validation.
- Do not silently normalize invalid financial values.
- Do not use unvalidated external data in business operations.

Validation must occur before financial state is modified.

## 4. Financial Integrity

Financial integrity is a critical security requirement.

Requirements:

- Use `decimal` for all financial values.
- Never use `double` or `float` for monetary calculations.
- Validate projected exposure before committing an order.
- Enforce the fixed domain constant `100000000.00m` as the maximum absolute exposure per symbol; do not make it externally configurable.
- Immediately account for accepted orders at full `Price × OrderQty`, without claiming a real market execution.
- Ensure exposure validation and updates are atomic.
- Ensure rejected orders do not modify exposure.
- Prevent duplicate financial processing.
- Isolate exposure by symbol.
- Protect shared financial state against concurrent modification.

The exposure boundary is inclusive:

`-100000000.00m <= Exposure <= 100000000.00m`

Do not allow malformed messages, race conditions or duplicate requests to bypass financial controls.

## 5. Idempotency and Replay Protection

Use `ClOrdID (11)` as the primary client order identifier.

Requirements:

- Detect previously processed order identifiers.
- Prevent duplicate exposure updates.
- Handle concurrent duplicate submissions safely.
- Return or reproduce the original accepted or rejected result for identical content under the same `ClOrdID`; reject different content as a conflict without changing the original order or exposure.
- Atomically coordinate globally keyed identity, original-result registration and per-symbol exposure validation/update, including concurrent same-ID requests for different symbols. Per-symbol locks alone are insufficient.
- Concurrent duplicates must produce at most one financial transition, with the original result or a conflict according to content.
- Consider FIX retransmission behavior and sequence numbers.

Do not rely exclusively on FIX session-level duplicate detection.

Do not automatically resubmit an order after a timeout.

A timeout or communication failure must be treated as an unknown outcome until the order status can be reconciled.

Do not confuse transport-level replay protection with application-level idempotency.

## 6. HTTP Application Security

OrderGenerator must follow ASP.NET Core security best practices.

Requirements:

- Require HTTPS for network-accessible environments and production; isolated local development may use HTTP loopback with non-sensitive test data. Never transmit credentials or sensitive information over HTTP.
- Validate all submitted data on the server.
- Apply appropriate request size limits.
- Handle malformed requests safely.
- Avoid exposing stack traces or internal exception details.
- Use secure response headers where appropriate.
- Apply appropriate cookie security settings when authentication cookies are used.
- Use anti-forgery protection for browser-based form submissions.
- Avoid unnecessary CORS permissions.
- Restrict allowed origins when cross-origin access is required.
- Do not expose internal application services directly through HTTP.

Do not disable ASP.NET Core security middleware without an explicit technical justification.

### 6.1 Cross-Site Scripting

Prevent XSS by:

- Using Razor's default HTML encoding.
- Avoiding unsafe raw HTML rendering.
- Sanitizing user-generated HTML only when HTML input is genuinely required.
- Avoiding direct insertion of untrusted values into JavaScript.
- Using safe DOM APIs when client-side JavaScript is necessary.

Never trust order descriptions, error messages or other external text as safe HTML.

### 6.2 Cross-Site Request Forgery

For browser-based state-changing operations:

- Use ASP.NET Core anti-forgery protection.
- Validate anti-forgery tokens.
- Use appropriate cookie settings.
- Do not rely exclusively on CORS as CSRF protection.

If an API uses token-based authentication without ambient browser credentials, document the relevant security model.

### 6.3 CORS

If CORS is required:

- Allow only explicitly approved origins.
- Restrict HTTP methods.
- Restrict headers.
- Avoid wildcard origins when credentials are enabled.
- Do not enable CORS globally without a clear requirement.

## 7. FIX Session Security

FIX communication must be restricted to authorized counterparties.

Requirements:

- Validate `SenderCompID`.
- Validate `TargetCompID`.
- Configure expected session identifiers.
- Restrict network access to authorized peers.
- Protect session configuration.
- Use secure transport when required by the deployment environment.
- Avoid exposing FIX listeners publicly without justification.
- Monitor unexpected connection attempts.
- Handle invalid session messages safely.

Do not assume that SenderCompID and TargetCompID provide authentication by themselves.

FIX session identifiers are protocol-level identifiers, not a substitute for transport security or peer authentication.

## 8. Network Security

Network communication must be restricted to required endpoints.

Requirements:

- Expose only necessary ports.
- Restrict FIX Acceptor access to authorized network sources where possible.
- Avoid unnecessary public listeners.
- Use TLS or another approved secure transport mechanism when required.
- Configure connection and operation timeouts.
- Handle unexpected disconnects.
- Avoid accepting untrusted network traffic without validation.

Do not hardcode network addresses or environment-specific endpoints.

Document any local-development configuration that disables transport encryption and ensure it is not presented as production-ready.

## 9. Secrets and Credentials

Never store secrets in source code or version control.

Sensitive values include:

- FIX credentials.
- Passwords.
- API keys.
- Access tokens.
- Private keys.
- Certificates containing private key material.
- Database credentials.
- Connection strings containing passwords.

Use appropriate configuration mechanisms:

- .NET User Secrets for local development.
- Environment variables for suitable deployment scenarios.
- Secure secret providers for production environments.
- Protected certificate storage when certificates are required.

Requirements:

- Never commit real credentials.
- Never include secrets in sample configuration.
- Use placeholders in documentation.
- Restrict access to secret storage.
- Avoid exposing secrets through logs or exceptions.
- Rotate credentials when exposure is suspected.

If a secret is accidentally committed, removing it from the latest commit is not sufficient. Treat it as compromised and rotate or revoke it.

## 10. Configuration Security

Validate security-sensitive configuration during application startup.

Requirements:

- Reject missing mandatory configuration.
- Reject invalid ports and endpoints.
- Validate session identifiers.
- Avoid insecure default credentials.
- Avoid permissive production defaults.
- Separate development and production configuration.
- Do not expose sensitive configuration through diagnostic endpoints.

Configuration files committed to the repository must contain only safe example values.

Do not assume that a configuration value is secure merely because it is stored outside source code.

## 11. Error Handling

Error handling must not disclose sensitive implementation details.

Requirements:

- Do not return stack traces to external clients.
- Do not expose internal paths or infrastructure details.
- Do not include credentials in exception messages.
- Use safe and meaningful error responses.
- Log technical details internally with appropriate redaction.
- Distinguish business rejections from infrastructure failures.
- Avoid swallowing security-related exceptions.

For FIX communication, return protocol-compliant responses without exposing internal implementation details.

Do not report an order as accepted when processing failed or its outcome is unknown.

## 12. Logging and Sensitive Data

Use structured logging through `ILogger<T>`.

Requirements:

- Include correlation identifiers when appropriate.
- Log security-relevant events.
- Log authentication or session failures where applicable.
- Avoid logging passwords and credentials.
- Avoid logging private keys or tokens.
- Avoid exposing sensitive personal or financial information.
- Avoid indiscriminate logging of complete FIX messages.
- Apply appropriate retention and access restrictions.

Useful identifiers may include:

- `ClOrdID`.
- FIX session identifier.
- Symbol.
- Processing status.
- Rejection category.

Do not log more financial or personal information than necessary for troubleshooting.

Sanitize untrusted values before logging when they could introduce log injection or misleading log entries.

## 13. Dependency Security

Third-party dependencies must be evaluated for security and compatibility.

Requirements:

- Use trusted NuGet packages.
- Prefer stable package versions.
- Verify package compatibility with .NET 10.
- Review known vulnerabilities.
- Keep dependencies updated.
- Remove unused packages.
- Avoid abandoned or unmaintained libraries.
- Avoid unnecessary transitive dependencies.

QuickFIX/n must be obtained from a trusted package source and kept updated according to compatibility and security requirements.

Do not disable vulnerability checks to obtain a successful build.

Use available .NET package auditing capabilities when appropriate.

## 14. Injection Protection

Prevent injection attacks across all application boundaries.

Requirements:

- Use parameterized SQL queries if database access is introduced.
- Never concatenate untrusted input into SQL commands.
- Avoid shell execution using untrusted values.
- Avoid unsafe dynamic code execution.
- Avoid unsafe deserialization.
- Encode output appropriately.
- Validate file paths if file access is introduced.
- Do not use untrusted input to select arbitrary types or application components.

Do not introduce database or shell execution functionality unless required by the application.

## 15. Denial-of-Service Protection

Protect applications against excessive resource consumption.

Consider:

- HTTP request size limits.
- Request rate limiting where appropriate.
- Connection limits.
- FIX session limits.
- Message size limits.
- Operation timeouts.
- Bounded in-memory collections.
- Controlled background processing.
- Safe handling of malformed messages.
- Protection against unbounded pending order correlation.

Requirements:

- Avoid unbounded queues and dictionaries.
- Remove completed correlation entries safely.
- Avoid unlimited concurrent financial operations.
- Avoid blocking thread pool threads.
- Configure reasonable timeouts.
- Handle resource exhaustion explicitly.

Do not introduce complex distributed rate-limiting infrastructure without a concrete requirement.

## 16. Concurrency and Security

Concurrency failures can become financial integrity vulnerabilities.

Requirements:

- Protect shared mutable state.
- Make exposure validation and modification atomic.
- Coordinate idempotency with financial updates.
- Prevent duplicate order processing.
- Avoid race conditions in pending FIX request correlation.
- Avoid deadlocks.
- Avoid unsafe shared collections.

A concurrent collection alone does not guarantee atomicity across multiple business operations.

The security review must consider concurrent requests, retransmissions and simultaneous FIX message delivery.

## 17. File System Security

If applications write FIX logs, message stores or other files:

- Use controlled application directories.
- Apply appropriate file permissions.
- Avoid writing sensitive files to publicly accessible directories.
- Validate file paths.
- Avoid path traversal vulnerabilities.
- Protect log and message-store contents.
- Define retention and cleanup policies.
- Avoid exposing internal files through HTTP.

Do not store credentials or private keys in ordinary application log directories.

## 18. Authentication and Authorization

Do not introduce authentication complexity without a clear requirement.

However:

- Do not expose order submission endpoints publicly without evaluating access control.
- Restrict administrative operations.
- Protect configuration and diagnostics endpoints.
- Ensure only authorized FIX counterparties can establish sessions.
- Document the authentication and authorization assumptions.

If authentication is not implemented because of the challenge scope, explicitly document that limitation.

Do not claim that the application is production-secure without appropriate access controls.

## 19. Cryptography

Use established .NET cryptographic APIs and approved protocols.

Requirements:

- Do not implement custom cryptographic algorithms.
- Do not hardcode encryption keys.
- Protect private key material.
- Validate certificates appropriately.
- Use secure TLS configurations.
- Avoid obsolete cryptographic algorithms.
- Avoid disabling certificate validation.

Never use callbacks that unconditionally accept invalid TLS certificates.

## 20. Development and Repository Security

The repository must not contain:

- Real credentials.
- Access tokens.
- Private keys.
- Production connection strings.
- Sensitive FIX session configuration.
- Customer information.
- Unnecessary internal infrastructure details.

The `.gitignore` must exclude:

- Local secrets.
- Environment-specific files.
- IDE-generated files.
- Build artifacts.
- Test output.
- Local FIX logs and message stores when they contain sensitive data.

Review changes before committing.

Do not assume that deleting a secret from the working tree removes it from Git history.

## 21. Security Testing

Security-sensitive behavior must be covered by appropriate tests.

Test at least:

### Input Validation

- Missing fields.
- Invalid symbols.
- Invalid side values.
- Invalid quantity.
- Invalid price.
- Malformed HTTP requests.
- Malformed FIX messages.

### Financial Integrity

- Exposure limit enforcement.
- Concurrent requests.
- Duplicate `ClOrdID`.
- Same-ID identical and conflicting content concurrently across symbols, including original-result and state integrity.
- Rejected order state preservation.
- No duplicate financial impact.

### HTTP Security

- Anti-forgery protection for browser-based state-changing requests.
- Unsafe output encoding.
- Request size limits.
- Error response information disclosure.
- CORS configuration where applicable.

### FIX Security

- Invalid session identifiers.
- Unauthorized connection attempts.
- Invalid message fields.
- Duplicate messages.
- Unexpected disconnects.
- Invalid configuration.

### Operational Security

- Missing mandatory configuration.
- Secret leakage through logs.
- Vulnerable dependencies.
- Resource exhaustion scenarios.

Do not claim security coverage for scenarios that have not been tested.

## 22. Security Review Severity

Classify findings using the following severity levels:

| Severity | Description |
|---|---|
| Critical | Direct compromise of financial integrity, credentials or system control with severe impact |
| High | Significant unauthorized access, financial manipulation or sensitive data exposure |
| Medium | Exploitable weakness with limited scope or impact |
| Low | Minor security weakness or hardening opportunity |
| Informational | Recommendation or observation without a confirmed vulnerability |

Every finding should include:

- Description.
- Affected component.
- Evidence.
- Exploitation scenario.
- Potential impact.
- Recommended remediation.
- Relevant file or configuration reference.

Do not classify hypothetical issues as confirmed vulnerabilities without supporting evidence.

## 23. Security Anti-Patterns

Never:

- Commit credentials or private keys.
- Disable TLS certificate validation.
- Trust client-side validation.
- Trust FIX message content without validation.
- Expose stack traces externally.
- Log secrets.
- Use floating-point arithmetic for financial values.
- Allow duplicate orders to modify exposure.
- Modify exposure before successful validation.
- Accept unauthorized FIX sessions.
- Use permissive CORS without justification.
- Disable anti-forgery protection without justification.
- Deserialize untrusted content unsafely.
- Execute untrusted input through shell commands.
- Ignore dependency vulnerability warnings.
- Treat network delivery as business acceptance.
- Automatically retry an order with an unknown outcome.

## 24. Security Compliance Checklist

Before considering a security-sensitive change complete, verify:

- [ ] All external input is validated.
- [ ] Financial rules are enforced server-side.
- [ ] Monetary values use `decimal`.
- [ ] Exposure updates are atomic.
- [ ] Duplicate orders cannot create additional financial effects.
- [ ] HTTP endpoints use appropriate security controls.
- [ ] HTTPS is configured where required.
- [ ] Anti-forgery protection is enabled for browser-based state-changing requests.
- [ ] CORS is restricted when applicable.
- [ ] FIX sessions validate expected identifiers.
- [ ] Network access is appropriately restricted.
- [ ] Secrets are externalized.
- [ ] Configuration is validated at startup.
- [ ] Error responses do not expose internal details.
- [ ] Logs do not contain credentials or sensitive information.
- [ ] Dependencies have been reviewed for known vulnerabilities.
- [ ] Resource consumption is appropriately bounded.
- [ ] Shared state is protected against race conditions.
- [ ] TLS certificate validation is not disabled.
- [ ] Security limitations are documented.
- [ ] Relevant security tests have been executed.

## 25. Instructions for GitHub Copilot

When creating or modifying security-sensitive code:

1. Inspect the existing implementation and configuration.
2. Read the architecture, C#, .NET, financial domain and FIX instructions.
3. Treat all external input as untrusted.
4. Preserve financial integrity and idempotency guarantees.
5. Avoid introducing unnecessary security dependencies.
6. Never hardcode secrets.
7. Do not disable security controls to simplify implementation.
8. Keep error responses safe.
9. Add tests for relevant security behavior.
10. Report potential vulnerabilities with evidence.
11. Document security assumptions and limitations.
12. Do not claim a security review or vulnerability check has passed unless it was actually performed.

If a security requirement conflicts with an implementation decision, report the conflict and recommend a secure alternative instead of silently weakening the control.
