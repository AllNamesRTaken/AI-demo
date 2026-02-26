---
description: 'Given a feature/entity name, produces the canonical 8-step task checklist and file scaffolding plan following the project architecture conventions.'
tools: ['read', 'search']
---

# Skill: scaffold-feature

Produce a complete, ready-to-execute implementation plan for a new feature, following the conventions in AGENTS.md and ARCHITECTURE.md.

## Usage

Call this skill with a feature (entity) name. Examples:
- `scaffold-feature: Category`
- `scaffold-feature: Comment`
- `scaffold-feature: Tag`

Optionally specify which operations are needed:
- `scaffold-feature: Category (CRUD)` — full create / read / update / delete (default)
- `scaffold-feature: Category (read-only)` — queries only, no mutations

## Procedure

1. Read `AGENTS.md` to confirm current naming conventions and the quick-reference checklist.
2. Read `src/AiDemo.Contracts/Hubs/IAppHub.cs` and `IAppHubClient.cs` to understand existing patterns.
3. Read one existing Command + Handler pair (e.g., `src/AiDemo.Application/Commands/CreateItem/`) for code style reference.
4. Generate the output below.

## Output format

```
## Scaffold Plan: <FeatureName>

### New files to create

| # | File path | Purpose |
|---|-----------|---------|
| 1 | src/AiDemo.Domain/Entities/<Feature>.cs | Domain entity |
| 2 | src/AiDemo.Contracts/DTOs/<Feature>Dto.cs | Response DTO (sealed record) |
| 3 | src/AiDemo.Contracts/DTOs/Create<Feature>Dto.cs | Create input DTO |
| 4 | src/AiDemo.Contracts/DTOs/Update<Feature>Dto.cs | Update input DTO (omit if read-only) |
| 5 | src/AiDemo.Application/Commands/Create<Feature>/Create<Feature>Command.cs | Command record |
| 6 | src/AiDemo.Application/Commands/Create<Feature>/Create<Feature>Handler.cs | Handler |
| 7 | src/AiDemo.Application/Commands/Create<Feature>/Create<Feature>Validator.cs | FluentValidation validator |
| 8 | src/AiDemo.Application/Commands/Update<Feature>/... | (omit if read-only) |
| 9 | src/AiDemo.Application/Commands/Delete<Feature>/... | (omit if read-only) |
| 10 | src/AiDemo.Application/Queries/Get<Feature>s/Get<Feature>sQuery.cs | List query |
| 11 | src/AiDemo.Application/Queries/Get<Feature>ById/Get<Feature>ByIdQuery.cs | Single query |

### Files to modify

| File | Change |
|------|--------|
| src/AiDemo.Contracts/Hubs/IAppHub.cs | Add Create/Update/Delete/Get methods with idempotency keys on mutating ops |
| src/AiDemo.Contracts/Hubs/IAppHubClient.cs | Add On<Feature>Created / On<Feature>Updated / On<Feature>Deleted callbacks |
| src/AiDemo.Server/Hubs/AppHub.cs | Implement new IAppHub methods, dispatch via mediator, outbox for notifications |
| src/AiDemo.Infrastructure/Persistence/AppDbContext.cs | Add DbSet<<Feature>> and EF configuration |
| src/AvaloniaApp/Services/HubConnectionService.cs | Add client-side InvokeAsync wrappers and On<> callback registrations |

### Code templates

#### Domain entity — src/AiDemo.Domain/Entities/<Feature>.cs
\`\`\`csharp
namespace AiDemo.Domain.Entities;

public sealed class <Feature>
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; } = string.Empty;
    // TODO: add domain-specific properties
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; private set; }
    public Guid CreatedByUserId { get; private set; }

    private <Feature>() { }

    public static <Feature> Create(string name, Guid createdByUserId)
    {
        return new <Feature> { Name = name, CreatedByUserId = createdByUserId };
    }

    public void Update(string name)
    {
        Name = name;
        UpdatedAt = DateTime.UtcNow;
    }
}
\`\`\`

#### Response DTO — src/AiDemo.Contracts/DTOs/<Feature>Dto.cs
\`\`\`csharp
namespace AiDemo.Contracts.DTOs;

public sealed record <Feature>Dto(
    Guid Id,
    string Name,
    // TODO: add domain-specific properties
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    Guid CreatedByUserId
);
\`\`\`

#### Command — src/AiDemo.Application/Commands/Create<Feature>/Create<Feature>Command.cs
\`\`\`csharp
using AiDemo.Application.Interfaces;
using AiDemo.Contracts.DTOs;
using Mediator;

namespace AiDemo.Application.Commands.Create<Feature>;

[GenerateMediator]
public sealed partial record Create<Feature>Command(
    string Name,
    // TODO: add domain-specific properties
    Guid UserId,
    Guid? IdempotencyKey = null
) : IIdempotentCommand<<Feature>Dto>;
\`\`\`

#### IAppHub additions
\`\`\`csharp
// Mutating — include idempotency key
Task<<Feature>Dto> Create<Feature>Async(Create<Feature>Dto dto, Guid? idempotencyKey = null);
Task<<Feature>Dto> Update<Feature>Async(Update<Feature>Dto dto, Guid? idempotencyKey = null);
Task Delete<Feature>Async(Guid id, Guid? idempotencyKey = null);
// Read
Task<<Feature>Dto?> Get<Feature>ByIdAsync(Guid id);
Task<IEnumerable<<Feature>Dto>> Get<Feature>sAsync();
\`\`\`

#### IAppHubClient additions
\`\`\`csharp
Task On<Feature>Created(<Feature>Dto item);
Task On<Feature>Updated(<Feature>Dto item);
Task On<Feature>Deleted(Guid id);
\`\`\`

### Outbox message types to register

| Event | Outbox message type string |
|-------|---------------------------|
| Created | `<Feature>Created` |
| Updated | `<Feature>Updated` |
| Deleted | `<Feature>Deleted` |

### Checklist for action-agent

- [ ] Create Domain entity
- [ ] Create Contracts DTOs (response + create + update)
- [ ] Add IAppHub methods (with idempotency keys on mutating operations)
- [ ] Add IAppHubClient callbacks
- [ ] Create Application Commands (create / update / delete) with handlers and validators
- [ ] Create Application Queries (list + by-id) with handlers
- [ ] Add DbSet and EF configuration in AppDbContext
- [ ] Implement hub methods in AppHub.cs (mediator dispatch + outbox)
- [ ] Add client wrappers in HubConnectionService.cs
- [ ] Add EF migration: `dotnet ef migrations add Add<Feature>`
- [ ] Run build-validate to confirm no compile errors
- [ ] Run verify-contracts to confirm hub interface and implementations are in sync
```
