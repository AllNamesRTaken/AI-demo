# AI Agents Instructions

> This document provides guidance for AI coding assistants (GitHub Copilot, Cursor, etc.) working on this codebase.

## Project Overview

This is a **client-server application** using:
- **SignalR** for all RPC communication (bidirectional real-time)
- **Authentik** for OIDC authentication in local development (cloud OIDC provider in production)
- **Avalonia** for the desktop client
- **.NET 10** across all projects

## Critical Documents

| Document | Purpose |
|----------|---------|
| [ARCHITECTURE.md](./ARCHITECTURE.md) | **Primary reference** - Full system architecture, patterns, and implementation details |
| [README.md](./README.md) | Project overview and getting started |

## Architecture Rules

### 1. Communication Pattern

**ALWAYS use SignalR for business logic RPC:**
```csharp
// ✅ CORRECT: Use SignalR hub methods
await _hubConnection.InvokeAsync<ItemDto>("CreateItemAsync", dto);

// ❌ WRONG: Do not create REST endpoints for business logic
app.MapPost("/api/items", ...); // Only for health checks and auth
```

**REST endpoints are ONLY for:**
- Health checks (`/health/*`)
- Token refresh (`/api/auth/refresh`)

## OIDC Provider Usage

- **Local development:** Use [Authentik](https://goauthentik.io/) as the OIDC provider. It is lightweight, easy to run in Docker, and suitable for developer onboarding.
- **Production:** Use your organization's cloud OIDC provider (e.g., Azure AD, Auth0, or enterprise Keycloak). Configuration and endpoints may differ.

**Note:** All code/configuration samples referencing Keycloak should be adapted to use Authentik for local development. Use `Oidc:Authority` and `Oidc:ClientId` configuration keys.

## Infrastructure Execution (Windows Development)

**ALWAYS use WSL for Docker and infrastructure commands** to align with Linux-based production environments:

```bash
# Enter interactive WSL session (recommended)
wsl

# Then run Docker commands with sudo
sudo docker compose up -d
sudo docker compose ps
sudo docker compose logs -f
sudo docker compose down
```

**Why WSL?**
- Ensures development environment matches Linux-based production deployment
- Avoids Windows Docker Desktop pipe issues
- Provides consistent behavior across dev/staging/prod
- Required for proper Docker integration on Windows

**Note:** Use `docker compose` (v2 command format) instead of `docker-compose` (v1). Docker requires `sudo` in WSL.

### 2. Mediator Pattern

**Use martinothamar/Mediator (NOT MediatR):**
```csharp
// ✅ CORRECT: martinothamar/Mediator with source generators
[GenerateMediator]
public sealed partial record CreateItemCommand(...) : ICommand<ItemDto>;

// ❌ WRONG: MediatR syntax
public record CreateItemCommand(...) : IRequest<ItemDto>;
```

### 3. Project Structure

Follow the Clean Architecture layers:
```
AiDemo.Domain/        → Entities, interfaces (no dependencies)
AiDemo.Application/   → Commands, queries, handlers (depends on Domain)
AiDemo.Infrastructure/→ EF Core, external services (depends on Application)
AiDemo.Contracts/     → SHARED DTOs and hub interfaces (no dependencies)
AiDemo.Server/        → ASP.NET Core host (depends on all)
AiDemo.Client.*/      → UI clients (depends on Contracts)
```

### 4. Shared Contracts

**Hub interfaces MUST be in AiDemo.Contracts:**
```csharp
// AiDemo.Contracts/Hubs/IAppHub.cs - Client → Server
public interface IAppHub
{
    // Mutating operations include idempotency key
    Task<ItemDto> CreateItemAsync(
        CreateItemDto dto, 
        Guid? idempotencyKey = null,
        CancellationToken ct = default);
}

// AiDemo.Contracts/Hubs/IAppHubClient.cs - Server → Client
public interface IAppHubClient
{
    Task OnItemCreated(ItemDto item);
}
```

### 5. Idempotency Keys

**All mutating commands SHOULD support idempotency:**
```csharp
// Command with idempotency support
public interface IIdempotentCommand<TResponse> : ICommand<TResponse>
{
    Guid? IdempotencyKey { get; }
}

[GenerateMediator]
public sealed partial record CreateItemCommand(
    string Name,
    Guid UserId,
    Guid? IdempotencyKey = null  // ← Include in all commands
) : IIdempotentCommand<ItemDto>;
```

### 6. Outbox Pattern

**Use outbox for reliable event delivery:**
```csharp
// In handler - add to outbox in same transaction
_context.OutboxMessages.Add(new OutboxMessage
{
    Type = "ItemCreated",
    Payload = JsonSerializer.Serialize(dto)
});
await _context.SaveChangesAsync(ct);  // Single atomic transaction

// ❌ WRONG: Direct SignalR notification from handler
await Clients.Others.OnItemCreated(item);  // May fail after DB commit
```

**Background Service Processes Outbox:**
- `OutboxProcessorService` runs as a background service in AiDemo.Server
- Polls outbox table every 1 second for unprocessed messages
- Sends SignalR notifications and marks messages as processed
- Implements retry logic with exponential backoff

### 7. Authentication

**All hub methods require authentication:**
```csharp
[Authorize]  // ← Required on hub class
public sealed class AppHub : Hub<IAppHubClient>, IAppHub
{
        // All methods automatically require auth
}
```

**Get user ID from claims:**
```csharp
private Guid GetUserId() => 
        Guid.Parse(Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                ?? throw new UnauthorizedAccessException());
```

**OIDC Configuration:**
```json
// appsettings.Development.json
{
    "Oidc": {
        "Authority": "http://localhost:9000/application/o/ai-demo",
        "ClientId": "ai-demo-desktop",
        "Audience": "ai-demo-server"
    }
}
```

**Note:** Authentik typically runs on port 9000. Adjust based on your docker-compose configuration.

## Code Generation Preferences

### When creating commands/queries:

```csharp
// Command with handler in same folder
// Location: AiDemo.Application/Commands/CreateItem/

// CreateItemCommand.cs
[GenerateMediator]
public sealed partial record CreateItemCommand(
    string Name,
    Guid UserId
) : ICommand<ItemDto>;

// CreateItemHandler.cs
public sealed class CreateItemHandler : ICommandHandler<CreateItemCommand, ItemDto>
{
    public async ValueTask<ItemDto> Handle(CreateItemCommand command, CancellationToken ct)
    {
        // Implementation
    }
}

// CreateItemValidator.cs (optional)
public sealed class CreateItemValidator : AbstractValidator<CreateItemCommand>
{
    public CreateItemValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}
```

### When creating DTOs:

```csharp
// Use records, sealed, in Contracts project
public sealed record ItemDto(
    Guid Id,
    string Name,
    string Description,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    Guid CreatedByUserId
);
```

### When adding hub methods:

1. Add to `IAppHub` interface (Contracts)
2. Add callback to `IAppHubClient` if needed (Contracts)  
3. Implement in `AppHub` (Server)
4. Add outbox message in handler for notifications
5. Add corresponding method in client `HubConnectionService`

## Naming Conventions

| Item | Convention | Example |
|------|------------|---------|
| Commands | `{Action}{Entity}Command` | `CreateItemCommand` |
| Queries | `Get{Entity/Entities}Query` | `GetItemsQuery` |
| Handlers | `{Command/Query}Handler` | `CreateItemHandler` |
| DTOs | `{Entity}Dto` | `ItemDto` |
| Hub callbacks | `On{Event}` | `OnItemCreated` |
| Hub methods | `{Action}Async` | `CreateItemAsync` |

## Testing Approach

- **Unit tests** for handlers (Application layer)
- **Integration tests** for hub methods (Server)
- **No mocking** of EF Core - use in-memory database or Testcontainers

## Common Mistakes to Avoid

1. ❌ Creating REST controllers for business logic
2. ❌ Using MediatR instead of martinothamar/Mediator
3. ❌ Putting DTOs in Domain or Application layer
4. ❌ Forgetting `[Authorize]` on hub class
5. ❌ Not notifying other clients after mutations
6. ❌ Blocking async calls in SignalR handlers
7. ❌ Hardcoding OIDC URLs (use configuration)
8. ❌ Sending SignalR notifications directly (use outbox pattern)
9. ❌ Forgetting idempotency keys on mutating operations

## Quick Reference

### Add a new feature (e.g., "Category"):

1. `AiDemo.Domain/Entities/Category.cs` - Entity
2. `AiDemo.Contracts/DTOs/CategoryDto.cs` - DTO
3. `AiDemo.Contracts/Hubs/IAppHub.cs` - Add methods (with idempotency key)
4. `AiDemo.Contracts/Hubs/IAppHubClient.cs` - Add callbacks
5. `AiDemo.Application/Commands/CreateCategory/` - Command + Handler + Validator
6. `AiDemo.Application/Queries/GetCategories/` - Query + Handler
7. `AiDemo.Server/Hubs/AppHub.cs` - Implement hub methods
8. `AiDemo.Client.Avalonia/Services/HubConnectionService.cs` - Client methods

**OIDC Setup for Local Development:**
- Run Authentik in Docker (see README for compose file)
- Configure OIDC application in Authentik for your client and server
- Use `Oidc:Authority` and `Oidc:ClientId` in your appsettings

### Key files to understand:

- [ARCHITECTURE.md](./ARCHITECTURE.md) - **Read this first**
- `src/AiDemo.Contracts/Hubs/IAppHub.cs` - All available operations
- `src/AiDemo.Server/Hubs/AppHub.cs` - Server implementation
- `src/AiDemo.Server/Program.cs` - Service configuration

### Program.cs Setup (Incremental Approach):

When implementing the server, build Program.cs incrementally:
1. Basic ASP.NET Core setup
2. Add JWT authentication with OIDC
3. Configure SignalR with MessagePack
4. Register Mediator and pipeline behaviors
5. Configure EF Core and DbContext
6. Add health checks
7. Register background services (OutboxProcessorService)

This allows validation at each step before proceeding.
