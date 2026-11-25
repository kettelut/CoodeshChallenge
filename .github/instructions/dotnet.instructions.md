---
applyTo: "**/*.csproj,**/*.sln,**/*.slnx,**/Directory.Build.*,**/Directory.Packages.props,**/global.json"
---

# .NET Development Instructions

## 1. Framework and SDK

The solution must target **.NET 10**.

Requirements:

- Use the .NET 10 SDK.
- Target `net10.0` for application and library projects.
- Use compatible package versions.
- Prefer the latest stable versions of dependencies compatible with .NET 10.
- Avoid preview packages and frameworks unless explicitly required.
- Keep SDK and package versions consistent across the solution.
- Use `global.json` when SDK version pinning is necessary.

Do not mix incompatible target frameworks without an explicit architectural reason.

## 2. Solution Structure

Maintain a clear and consistent solution structure.

The solution contains two main applications:

- `OrderGenerator`: ASP.NET Core application responsible for order submission and FIX Initiator communication.
- `OrderAccumulator`: application responsible for FIX Acceptor communication, order processing and exposure management.

Additional projects may be created when they provide a clear separation of responsibilities.

Recommended project organization:

- Domain.
- Application.
- Infrastructure.
- Presentation or Web.
- Tests.

Do not create projects merely to increase the number of layers.

Shared libraries should contain only genuinely shared contracts or functionality. Avoid coupling the applications through shared implementation details.

## 3. Project Configuration

Use SDK-style project files.

Recommended common settings:

- `Nullable` enabled.
- `ImplicitUsings` enabled.
- Appropriate language version.
- Warnings treated consistently.
- Deterministic builds where applicable.

Example configuration:

    <PropertyGroup>
      <TargetFramework>net10.0</TargetFramework>
      <Nullable>enable</Nullable>
      <ImplicitUsings>enable</ImplicitUsings>
    </PropertyGroup>

Do not duplicate common settings across multiple project files when they can be centralized safely using `Directory.Build.props`.

Use `Directory.Build.targets` only when shared build targets are necessary.

## 4. Dependency Management

Use NuGet for external dependencies.

Requirements:

- Prefer stable packages.
- Verify compatibility with .NET 10.
- Avoid unnecessary dependencies.
- Avoid packages that duplicate built-in .NET functionality.
- Keep dependencies updated.
- Review security advisories.
- Remove unused package references.

Use central package management through `Directory.Packages.props` when it improves consistency across multiple projects.

Do not introduce a dependency solely to implement a small utility that can be handled reliably with the .NET standard library.

QuickFIX/n must be isolated to the FIX integration projects or components.

## 5. Build Configuration

The solution must support reproducible builds.

Verify:

- All projects restore successfully.
- Project references are valid.
- Package versions are compatible.
- No circular project references exist.
- Build configurations are consistent.
- Required configuration files are included.
- Generated artifacts are excluded from source control.

Use the standard build configurations:

- Debug.
- Release.

Do not introduce custom build configurations without a concrete requirement.

## 6. Build and Restore Commands

The solution should support the standard .NET CLI workflow.

Restore dependencies:

    dotnet restore

Build the solution:

    dotnet build

Build in Release:

    dotnet build --configuration Release

Run tests:

    dotnet test

Run tests in Release:

    dotnet test --configuration Release

When a solution file exists, prefer building and testing the solution rather than individual projects unless a focused validation is required.

Do not claim successful execution unless the command was actually executed and completed successfully.

## 7. Application Configuration

Use the standard .NET configuration system.

Preferred configuration sources include:

- `appsettings.json`.
- `appsettings.Development.json`.
- Environment variables.
- User Secrets for local development.
- Secure deployment-specific secret providers.

Requirements:

- Never commit secrets.
- Do not hardcode environment-specific endpoints.
- Use strongly typed configuration where appropriate.
- Validate critical settings at startup.
- Keep configuration names consistent.
- Avoid duplicating configuration values across applications unnecessarily.

Configuration must be explicit and documented.

The challenge exposure limit is the fixed domain constant `100000000.00m`; do not configure it through options, appsettings, environment variables or user input.

## 8. Dependency Injection

Use the built-in Microsoft dependency injection container.

Requirements:

- Register dependencies in the composition root.
- Prefer constructor injection.
- Use appropriate service lifetimes.
- Avoid service locator patterns.
- Avoid resolving dependencies manually inside business logic.
- Keep registration code organized and easy to inspect.

Do not introduce an external dependency injection container without a specific technical justification.

## 9. ASP.NET Core Standards

For OrderGenerator, use ASP.NET Core on .NET 10.

Prefer Razor Pages for the order submission interface unless the approved architecture specifies otherwise.

Requirements:

- Keep HTTP concerns in the presentation layer.
- Use application services for business operations.
- Validate input on the server.
- Use dependency injection.
- Handle errors consistently.
- Require HTTPS for network-accessible environments and production. Isolated local development may use HTTP loopback with non-sensitive test data; never send credentials or sensitive information over HTTP.
- Use appropriate logging.
- Avoid exposing internal exceptions.
- Keep frontend code independent of FIX implementation details.

Do not reference QuickFIX/n directly from Razor Pages, controllers or HTTP endpoints.

## 10. Hosted Services and Application Lifecycle

Use `IHostedService` or `BackgroundService` when background processing is required.

For FIX session lifecycle management:

- Initialize sessions through the appropriate application lifecycle.
- Handle startup and shutdown explicitly.
- Handle session logon and logout.
- Dispose resources correctly.
- Avoid blocking application startup unnecessarily.
- Ensure background tasks terminate safely.
- Prevent duplicate service initialization.

Do not use unmanaged background threads when .NET hosted services provide a suitable abstraction.

## 11. Logging and Observability

Use `Microsoft.Extensions.Logging`.

Requirements:

- Use structured logging.
- Use appropriate log levels.
- Include correlation identifiers when relevant.
- Avoid logging secrets.
- Avoid excessive logging of complete FIX messages.
- Distinguish business rejections from infrastructure failures.
- Include sufficient diagnostic information.

Prefer dependency-injected `ILogger<T>`.

Use OpenTelemetry only if justified by the observability requirements. Do not introduce additional infrastructure without a clear benefit.

## 12. HTTP and Network Communication

Use supported .NET networking APIs.

Requirements:

- Prefer asynchronous network operations.
- Apply appropriate timeout policies.
- Handle cancellation.
- Dispose network resources correctly.
- Avoid unnecessary connection creation.
- Avoid blocking calls.
- Handle transient failures explicitly.

For FIX communication, use QuickFIX/n rather than implementing FIX transport or message parsing manually.

Do not treat successful network transmission as confirmation of business acceptance.

## 13. FIX Integration

QuickFIX/n must be used for FIX Protocol 4.4 integration.

Application responsibilities:

- `OrderGenerator`: FIX Initiator.
- `OrderAccumulator`: FIX Acceptor.

Requirements:

- Keep FIX configuration externalized.
- Use explicit session identifiers.
- Handle session lifecycle events.
- Use appropriate message validation.
- Handle asynchronous ExecutionReports.
- Correlate responses using `ClOrdID (11)`.
- Handle disconnects and timeouts.
- Prevent duplicate business processing.
- Keep FIX message handling separate from domain logic.

Do not implement business exposure calculations inside QuickFIX/n callbacks.

Do not assume that an accepted order has been executed.

## 14. Financial Processing

Financial calculations must use `decimal`.

Requirements:

- Never use `double` or `float` for financial values.
- Avoid culture-dependent parsing.
- Preserve decimal precision across application boundaries.
- Validate financial values before processing.
- Use explicit comparisons for exposure limits.
- Ensure exposure updates are atomic.
- Prevent duplicate financial effects.
- Immediately account for the full `Price × OrderQty` of each accepted order without claiming an actual market execution.
- Validate the numeric decimal price with `price % 0.01m == 0`, not textual scale: `1.100` and `1.1000` are valid; `1.101` and `1.105` are not.

The maximum absolute exposure is:

`100000000.00m`

The accepted interval is inclusive.

Rejected orders must not modify exposure.

## 15. Concurrency

Use .NET synchronization primitives appropriately.

Possible mechanisms include:

- `lock`.
- `SemaphoreSlim`.
- `ConcurrentDictionary<TKey, TValue>`.
- Other mechanisms justified by the application design.

Requirements:

- Protect shared mutable state.
- Ensure per-symbol exposure operations are atomic.
- Coordinate idempotency checks with exposure updates.
- Atomically coordinate global `ClOrdID` identity and original-result registration with per-symbol exposure updates, even for concurrent requests with different symbols; per-symbol locks alone do not ensure identity.
- Identical content under the same `ClOrdID` returns or reproduces the original accepted or rejected result; different content is rejected as a conflict without modifying original state.
- Use one global lock and one publication of a fully prepared copy-on-write snapshot containing exposure and original results; never mutate the published snapshot. This serializes different symbols but prevents partial state on pre-commit failure.
- Do not hold locks during network or unrelated I/O operations.

A thread-safe collection does not automatically make a multi-step business operation atomic.

## 16. Performance and Resource Management

Prioritize correctness and maintainability.

Requirements:

- Use asynchronous APIs for I/O.
- Avoid unnecessary allocations in frequently executed paths.
- Avoid unbounded collections.
- Dispose resources correctly.
- Avoid unnecessary object creation.
- Avoid blocking thread pool threads.
- Use efficient collection operations where justified.

Do not introduce performance optimizations without evidence that they are needed.

Never compromise financial correctness, idempotency or thread safety for performance.

## 17. Testing Infrastructure

Use a testing framework compatible with .NET 10.

The test solution should support:

- Unit tests.
- Integration tests.
- FIX integration tests.
- End-to-end tests where appropriate.

Requirements:

- Keep test projects separate from production projects.
- Use deterministic test data.
- Avoid dependence on external infrastructure for unit tests.
- Clearly distinguish mocked dependencies from real integrations.
- Ensure tests can run through `dotnet test`.

Tests must cover critical financial rules, concurrency and duplicate processing.

Do not claim that a test suite validates real FIX interoperability if it uses only mocked messages.

## 18. Code Analysis and Quality

Use compiler warnings and analyzers to maintain code quality.

Requirements:

- Resolve relevant compiler warnings.
- Avoid suppressing warnings without justification.
- Keep nullable reference analysis enabled.
- Use analyzers when they provide meaningful value.
- Avoid unnecessary code-style dependencies.
- Maintain consistent formatting.

Use `.editorconfig` to define repository-wide coding conventions when appropriate.

Do not disable analyzers or warnings merely to obtain a successful build.

## 19. Security and Secrets

Follow .NET security best practices.

Requirements:

- Require HTTPS for network-accessible environments and production. Isolated local development may use HTTP loopback with non-sensitive test data; never transmit credentials or sensitive information over HTTP.
- Keep secrets outside source control.
- Use User Secrets for local development when appropriate.
- Use environment variables or secure secret providers for deployment.
- Validate external input.
- Keep dependencies updated.
- Avoid exposing stack traces.
- Protect sensitive configuration.
- Avoid logging credentials or authentication material.

Never commit real FIX credentials or production connection settings.

## 20. Deployment and Runtime

Keep deployment requirements aligned with the challenge scope.

Document:

- Required .NET runtime.
- Application startup commands.
- Required configuration.
- FIX connectivity requirements.
- Startup order.
- Shutdown behavior.
- Persistence limitations.

Do not introduce containers, orchestration or cloud infrastructure unless required or explicitly approved.

If Docker is introduced, ensure the Docker configuration matches the actual application architecture and runtime requirements.

## 21. Source Control and Generated Files

Do not commit generated or environment-specific artifacts.

The `.gitignore` must exclude, where applicable:

- `bin/`.
- `obj/`.
- `.vs/`.
- IDE-generated files.
- Test results.
- Coverage output.
- Local environment configuration.
- User-specific files.
- Secrets and credentials.

Do not exclude source files or required project configuration.

Keep dependency and SDK configuration files under source control when they are part of the reproducible build strategy.

## 22. Scope Control

Keep the .NET solution focused on the Coodesh challenge.

Do not introduce unnecessary infrastructure or frameworks, including:

- Kafka.
- RabbitMQ.
- Redis.
- Kubernetes.
- Distributed event sourcing.
- External dependency injection containers.
- Unnecessary ORMs.
- Complex generic repository frameworks.
- Multiple logging providers without justification.

Prefer built-in .NET capabilities and a small number of well-justified dependencies.

## 23. .NET Compliance Checklist

Before considering a .NET change complete, verify:

- [ ] Projects target .NET 10.
- [ ] SDK and package versions are compatible.
- [ ] Project references follow architectural boundaries.
- [ ] Nullable reference types are enabled.
- [ ] Dependency injection is configured correctly.
- [ ] Configuration is externalized where appropriate.
- [ ] Secrets are not committed.
- [ ] Asynchronous operations are handled correctly.
- [ ] Resources are disposed correctly.
- [ ] Logging is structured and safe.
- [ ] Financial values use `decimal`.
- [ ] Shared state is thread-safe.
- [ ] FIX integration remains isolated.
- [ ] Tests are compatible with the target framework.
- [ ] Build and test commands are reproducible.
- [ ] Generated artifacts are excluded from source control.
- [ ] No unnecessary dependencies or infrastructure were introduced.

## 24. Instructions for GitHub Copilot

When creating or modifying .NET projects:

1. Inspect the existing solution and project files.
2. Read the relevant repository, architecture and C# instructions.
3. Preserve established project conventions.
4. Use .NET 10-compatible APIs and packages.
5. Avoid unnecessary project restructuring.
6. Keep project references aligned with architecture boundaries.
7. Do not introduce dependencies without justification.
8. Keep configuration and secrets separate.
9. Update tests when project behavior changes.
10. Restore and build when authorized and when the environment permits.
11. Report warnings and unresolved compatibility issues.
12. Do not claim successful validation without actual execution.

If a framework, package or SDK compatibility issue is identified, report it and recommend a compatible alternative instead of silently changing the target framework.
