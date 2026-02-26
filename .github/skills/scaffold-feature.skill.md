---
description: 'Given a feature/entity name, discovers the project layout and produces a ready-to-execute task checklist and file scaffolding plan. Generic and reusable across Clean Architecture projects.'
tools: ['read', 'search']
---

# Skill: scaffold-feature

Produce a complete, ready-to-execute implementation plan for a new feature by first discovering the actual project structure, then generating a fully resolved plan with no hardcoded assumptions.

## Usage

Call this skill with a feature (entity) name. Examples:
- `scaffold-feature: Category`
- `scaffold-feature: Comment`
- `scaffold-feature: Tag`

Optionally specify which operations are needed:
- `scaffold-feature: Category (CRUD)` — full create / read / update / delete (default)
- `scaffold-feature: Category (read-only)` — queries only, no mutations

## Procedure

### Step 1 — Discover architecture

Read `ARCHITECTURE.md` (fall back to `README.md`) to identify:
- Layer names and their `src/` paths (Domain, Application, Contracts/Shared, Infrastructure, Server, Client)
- Root namespace prefix (infer from `.csproj` filenames under `src/`)
- RPC mechanism (SignalR, gRPC, REST, etc.)
- ORM in use and whether an outbox pattern is present
- Mediator library (MediatR, martinothamar/Mediator, etc.)
- Whether idempotency keys are used on mutating operations

### Step 2 — Locate key files

Search `src/` to resolve each placeholder in the table below. Record the actual relative path found, or `N/A` if the concept does not exist in this project.

| Placeholder | What to search for |
|-------------|-------------------|
| `{DomainLayer}` | Directory containing domain entities (e.g. classes with `CreatedAt`, no external deps) |
| `{ContractsLayer}` | Directory containing shared DTOs / hub interfaces |
| `{ApplicationLayer}` | Directory containing commands, queries, handlers |
| `{InfrastructureLayer}` | Directory containing ORM context / repositories |
| `{ServerLayer}` | Directory containing the host entry point and hub implementation |
| `{ClientLayer}` | Directory containing the client-side connection service |
| `{HubServerInterface}` | Interface defining client→server RPC methods |
| `{HubClientInterface}` | Interface defining server→client callback methods |
| `{HubImpl}` | Concrete hub class implementing `{HubServerInterface}` |
| `{DbContext}` | ORM context class file |
| `{ClientService}` | Client-side hub connection / service wrapper |
| `{CommandExample}` | An existing command folder to use as style reference |

### Step 3 — Read style reference

Read the files in `{CommandExample}` to infer:
- Mediator attribute(s) on the command record (e.g. `[GenerateMediator]`)
- Command base interface and its generic form (e.g. `ICommand<T>`, `IIdempotentCommand<T>`)
- Handler base interface (e.g. `ICommandHandler<TCommand, TResult>`)
- Whether idempotency key is a nullable `Guid?` property on the command
- Namespace convention (e.g. `{Namespace}.Application.Commands.{CommandName}`)

### Step 4 — Resolve and generate

Substitute all discovered values into the output format below.

Apply the degradation rules in the table below **before** writing the output. Each conditional item must be **silently dropped** (not mentioned, not listed as N/A) when its guard condition is false. Only items marked "always" are unconditional.

| Item | Include when |
|------|-------------|
| Create input DTO | `(CRUD)` mode — always for CRUD |
| Update input DTO | `(CRUD)` mode |
| Update command + handler + validator | `(CRUD)` mode |
| Delete command + handler | `(CRUD)` mode |
| Validator file | A validation library was detected in `{CommandExample}` or `.csproj` deps |
| "Files to modify" row: `{HubServerInterface}` | `{HubServerInterface}` ≠ N/A |
| "Files to modify" row: `{HubClientInterface}` | `{HubClientInterface}` ≠ N/A |
| "Files to modify" row: `{HubImpl}` | `{HubImpl}` ≠ N/A |
| "Files to modify" row: `{DbContext}` | `{DbContext}` ≠ N/A |
| "Files to modify" row: `{ClientService}` | `{ClientService}` ≠ N/A |
| Checklist: hub method signatures | `{HubServerInterface}` ≠ N/A |
| Checklist: hub callback signatures | `{HubClientInterface}` ≠ N/A |
| Checklist: implement hub methods | `{HubImpl}` ≠ N/A |
| Checklist: outbox mention | `{HasOutbox}` = yes |
| Checklist: idempotency keys mention | `{IdempotentCommandInterface}` ≠ N/A |
| Checklist: DbSet / ORM config | `{DbContext}` ≠ N/A |
| Checklist: ORM migration | `{InfrastructureLayer}` ≠ N/A AND `{DbContext}` ≠ N/A |
| Checklist: client wrappers | `{ClientService}` ≠ N/A |

## Output format

```
## Scaffold Plan: <FeatureName>

### Resolved project layout

| Placeholder | Resolved value |
|-------------|----------------|
| {DomainLayer} | <path> |
| {ContractsLayer} | <path> |
| {ApplicationLayer} | <path> |
| {InfrastructureLayer} | <path or N/A> |
| {ServerLayer} | <path> |
| {ClientLayer} | <path or N/A> |
| {HubServerInterface} | <file or N/A> |
| {HubClientInterface} | <file or N/A> |
| {HubImpl} | <file or N/A> |
| {DbContext} | <file or N/A> |
| {ClientService} | <file or N/A> |
| {Namespace} | <root namespace> |
| {MediatorAttr} | <attribute or N/A> |
| {CommandInterface} | <interface or N/A> |
| {IdempotentCommandInterface} | <interface or N/A> |
| {HandlerInterface} | <interface or N/A> |
| {HasOutbox} | yes / no |

### New files to create

| # | File path | Purpose |
|---|-----------|---------|
| 1 | {DomainLayer}/<Feature>.cs | Domain entity |
| 2 | {ContractsLayer}/DTOs/<Feature>Dto.cs | Response DTO |
| 3 | {ContractsLayer}/DTOs/Create<Feature>Dto.cs | Create input DTO |
| 4 | {ContractsLayer}/DTOs/Update<Feature>Dto.cs | Update input DTO (omit if read-only) |
| 5 | {ApplicationLayer}/Commands/Create<Feature>/Create<Feature>Command.cs | Command record |
| 6 | {ApplicationLayer}/Commands/Create<Feature>/Create<Feature>Handler.cs | Handler |
| 7 | {ApplicationLayer}/Commands/Create<Feature>/Create<Feature>Validator.cs | Validator (if validation library present) |
| 8 | {ApplicationLayer}/Commands/Update<Feature>/... | (omit if read-only) |
| 9 | {ApplicationLayer}/Commands/Delete<Feature>/... | (omit if read-only) |
| 10 | {ApplicationLayer}/Queries/Get<Feature>s/Get<Feature>sQuery.cs | List query |
| 11 | {ApplicationLayer}/Queries/Get<Feature>ById/Get<Feature>ByIdQuery.cs | Single-item query |

### Files to modify

| File | Change |
|------|--------|
| {HubServerInterface} | Add Create/Update/Delete/Get method signatures; idempotency keys on mutating ops if {IdempotentCommandInterface} ≠ N/A |
| {HubClientInterface} | Add On<Feature>Created / On<Feature>Updated / On<Feature>Deleted callbacks |
| {HubImpl} | Implement new methods: mediator dispatch + outbox message (if {HasOutbox} = yes) |
| {DbContext} | Add DbSet<<Feature>> and any EF configuration |
| {ClientService} | Add InvokeAsync wrappers and On<> callback registrations |

### Checklist for action-agent

- [ ] Create Domain entity in {DomainLayer}
- [ ] Create Contracts DTOs (response + create + update) in {ContractsLayer}
- [ ] Add method signatures to {HubServerInterface} (idempotency keys on mutating ops)
- [ ] Add callback signatures to {HubClientInterface}
- [ ] Create Application Commands (create / update / delete) with handlers and validators in {ApplicationLayer}
- [ ] Create Application Queries (list + by-id) with handlers in {ApplicationLayer}
- [ ] Add DbSet and ORM configuration in {DbContext}
- [ ] Implement hub methods in {HubImpl} (mediator dispatch + outbox if {HasOutbox} = yes)
- [ ] Add client wrappers in {ClientService}
- [ ] Add ORM migration (command inferred from {InfrastructureLayer} tooling)
- [ ] Run build-validate (follow `.github/skills/build-validate.skill.md`) to confirm no compile errors
```
