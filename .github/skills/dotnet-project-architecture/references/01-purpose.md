# .NET Project Architecture

## Purpose

Provide architectural guidance for designing, implementing, analyzing and reviewing .NET 10 applications.

Use this skill when:

- Creating a new .NET solution.
- Defining project and folder structures.
- Establishing application boundaries.
- Evaluating dependency direction.
- Separating domain, application and infrastructure concerns.
- Reviewing architectural decisions.
- Refactoring tightly coupled applications.
- Evaluating maintainability and testability.
- Integrating external infrastructure and communication protocols.
- Defining architecture appropriate to a project's actual complexity.

The goal is to produce solutions that are maintainable, testable, reliable and understandable without introducing unnecessary abstractions or infrastructure.

---

## 1. Architectural Principles

Apply the following principles consistently.

### Separation of Concerns

Each component must have a clearly defined responsibility.

Separate:

- Presentation.
- Application orchestration.
- Domain business rules.
- Infrastructure and external integrations.

Do not allow a component to accumulate responsibilities from unrelated architectural layers.

### Dependency Inversion

High-level business policies must not depend directly on infrastructure implementations.

Use abstractions at meaningful boundaries when they provide a concrete architectural benefit.

The domain must not depend on:

- ASP.NET Core.
- Entity Framework Core.
- QuickFIX/n.
- Database-specific implementations.
- External transport libraries.
- Infrastructure configuration types.

### High Cohesion and Low Coupling

Group related behavior together and minimize unnecessary dependencies between components.

Avoid:

- God classes.
- Excessive cross-project references.
- Shared mutable state without clear ownership.
- Duplicate business rules.
- Circular dependencies.
- Infrastructure-specific types leaking across boundaries.

### Explicit Boundaries

Define clear boundaries between:

- External requests and internal commands.
- Domain entities and transport models.
- Application use cases and infrastructure implementations.
- Business failures and technical failures.
- Internal state and external representations.

### Pragmatic Architecture

Choose the simplest architecture that correctly satisfies the requirements.

Do not introduce patterns, projects, interfaces or infrastructure merely because they are commonly used.

Architecture should reduce complexity, not create it.

---

## 2. Recommended Solution Structure

A layered structure is a useful starting point for many .NET applications.

Example:

    src/
    ├── Application/
    │   ├── Interfaces/
    │   ├── Services/
    │   ├── Commands/
    │   ├── Queries/
    │   └── DTOs/
    ├── Domain/
    │   ├── Entities/
    │   ├── ValueObjects/
    │   ├── Enums/
    │   ├── Exceptions/
    │   └── Services/
    ├── Infrastructure/
    │   ├── Persistence/
    │   ├── ExternalServices/
    │   ├── Messaging/
    │   └── DependencyInjection/
    └── Presentation/
        ├── Controllers/
        ├── Pages/
        └── Models/

This is a conceptual example, not a mandatory directory structure.

Use separate projects when assembly boundaries provide meaningful isolation, independent testing, dependency control or deployment benefits.

Avoid creating a project for every folder or technical concern.

### Project responsibilities

| Layer | Responsibility |
|---|---|
| Domain | Business rules, entities, value objects and invariants |
| Application | Use cases, orchestration and application contracts |
| Infrastructure | External systems, persistence and technical implementations |
| Presentation | HTTP endpoints, user interfaces and request handling |

### Dependency direction

The recommended dependency direction is:

    Presentation → Application → Domain
    Infrastructure → Application / Domain

The domain must remain independent of infrastructure and presentation frameworks.

Infrastructure dependencies should be introduced through composition and dependency injection.

---

## 3. Domain Layer

The domain layer represents the core business model and rules.

It should contain:

- Entities.
- Value objects.
- Domain services.
- Enumerations.
- Domain exceptions.
- Business invariants.

### Entities

Use entities when an object has identity and a lifecycle.

Entities should:

- Protect their invariants.
- Expose meaningful operations.
- Avoid unnecessary public setters.
- Avoid depending on infrastructure.
- Represent business concepts rather than database structures.

### Value Objects

Use value objects for concepts defined by their values rather than identity.

Examples:

- Money.
- EmailAddress.
- OrderIdentifier.
- FinancialInstrument.
- Quantity.

Value objects should be immutable whenever practical.

### Domain Services

Use domain services when business logic does not naturally belong to a single entity or value object.

Avoid creating service classes that merely forward calls without adding meaningful behavior.

### Domain Events

Use domain events when a business occurrence needs to be communicated to other parts of the application.

Do not introduce event-driven architecture unless there is a concrete need.

---

## 4. Application Layer

The application layer coordinates use cases.

Typical responsibilities:

- Accept application commands.
- Execute use cases.
- Coordinate domain operations.
- Define transaction boundaries.
- Invoke infrastructure abstractions.
- Return application results.
- Coordinate validation and error handling.

Application services should not become repositories for all business logic.

Keep business invariants in the domain where practical.

### Use Case Design

Represent meaningful business operations explicitly.

Examples:

- SubmitOrder.
- AcceptOrder.
- RejectOrder.
- CalculateExposure.
- RetrieveOrderStatus.

A use case should have:

- A clear input contract.
- A defined responsibility.
- Explicit dependencies.
- Predictable outcomes.
- Appropriate error semantics.

Avoid excessive fragmentation into trivial handlers.

### DTOs and Contracts

Use application DTOs to define boundaries between layers.

Do not expose domain entities directly as HTTP responses or FIX messages.

Avoid sharing presentation-specific DTOs with the domain.

---

