# Implementation Agent - Internal Context

## Architecture Summary

This implementation follows the Clean Architecture pattern defined in ARCHITECTURE.md with:
- **SignalR for all RPC** (not REST endpoints for business logic)
- **martinothamar/Mediator** (NOT MediatR - different API!)
- **Authentik** for OIDC authentication (local dev)
- **Outbox pattern** for reliable event delivery
- **Idempotency keys** for safe command retries

## Critical Implementation Rules

### 1. SignalR Only for Business Logic
```csharp
// ✅ CORRECT - Use SignalR hub methods
await _hubConnection.InvokeAsync<ItemDto>("CreateItemAsync", dto);

// ❌ WRONG - No REST for business logic
app.MapPost("/api/items", ...);
```

### 2. martinothamar/Mediator Syntax
```csharp
// ✅ CORRECT - Source generator attribute
[GenerateMediator]
public sealed partial record CreateItemCommand(
    string Name,
    Guid UserId,
    Guid? IdempotencyKey = null
) : ICommand<ItemDto>;

// ❌ WRONG - MediatR syntax
public record CreateItemCommand : IRequest<ItemDto>  // NO!
```

### 3. DTOs Location
- **All DTOs go in `AiDemo.Contracts/DTOs/`**
- Never in Domain or Application layer
- Use `sealed record` for all DTOs

### 4. Idempotency Keys
All mutating commands should support idempotency:
```csharp
public interface IIdempotentCommand<TResponse> : ICommand<TResponse>
{
    Guid? IdempotencyKey { get; }
}
```

### 5. Outbox Pattern
Events stored in same transaction, processed by background service:
```csharp
// In handler - add to outbox
_context.OutboxMessages.Add(new OutboxMessage
{
    Type = "ItemCreated",
    Payload = JsonSerializer.Serialize(dto)
});
await _context.SaveChangesAsync(ct);  // Atomic with domain change
```

## NuGet Packages Reference

### AiDemo.Domain
```xml
<PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.0" />
```

### AiDemo.Contracts
```xml
<!-- No dependencies - pure DTOs and interfaces -->
```

### AiDemo.Application
```xml
<PackageReference Include="Mediator.Abstractions" Version="2.2.0" />
<PackageReference Include="Mediator.SourceGenerator" Version="2.2.0" />
<PackageReference Include="FluentValidation" Version="11.11.0" />
<PackageReference Include="FluentValidation.DependencyInjectionExtensions" Version="11.11.0" />
```

### AiDemo.Infrastructure
```xml
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.0" />
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.0" />
```

### AiDemo.Server
```xml
<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.0" />
<PackageReference Include="Microsoft.AspNetCore.SignalR" Version="10.0.0" />
<PackageReference Include="MessagePack.AspNetCoreMvcFormatter" Version="3.0.0" />
<PackageReference Include="AspNetCore.HealthChecks.NpgSql" Version="8.0.2" />
<PackageReference Include="Serilog.AspNetCore" Version="9.0.0" />
```

### AvaloniaApp (additions)
```xml
<PackageReference Include="Microsoft.AspNetCore.SignalR.Client" Version="10.0.0" />
<PackageReference Include="MessagePack" Version="3.0.0" />
<PackageReference Include="IdentityModel.OidcClient" Version="6.0.0" />
<PackageReference Include="Microsoft.Extensions.Http.Polly" Version="10.0.0" />
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="10.0.0" />
```

## Authentik Configuration (Local Development)

### Application: `ai-demo`
- **OAuth2 Provider**: `ai-demo` (public, PKCE enabled)
- **Client ID**: `ai-demo-desktop`
- **Redirect URIs**: `aidemo://callback`, `http://localhost:*/callback`

### Authentik Admin
- Default URL: `http://localhost:9000`
- Bootstrap password set via `AUTHENTIK_BOOTSTRAP_PASSWORD` env var

## Connection Strings

### Development
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=aidemo;Username=postgres;Password=postgres"
  },
  "Oidc": {
    "Authority": "http://localhost:9000/application/o/ai-demo",
    "ClientId": "ai-demo-desktop"
  }
}
```

## Important Implementation Notes

1. **JWT Token in SignalR**: Access token passed via query string for WebSocket connections
2. **MessagePack Protocol**: Enable for efficient binary serialization
3. **Automatic Reconnection**: Configure retry policy with exponential backoff
4. **Health Checks**: Only endpoints outside SignalR - `/health`, `/health/ready`, `/health/live`
5. **Token Refresh**: `/api/auth/refresh` endpoint for refreshing without reconnection

## Entity Definitions

### Item Entity
```csharp
public sealed class Item
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid CreatedByUserId { get; set; }
}
```

### OutboxMessage Entity
```csharp
public sealed class OutboxMessage
{
    public Guid Id { get; init; }
    public string Type { get; init; } = default!;
    public string Payload { get; init; } = default!;
    public DateTime CreatedAt { get; init; }
    public DateTime? ProcessedAt { get; set; }
    public int RetryCount { get; set; }
    public string? LastError { get; set; }
}
```

### IdempotencyRecord Entity
```csharp
public sealed class IdempotencyRecord
{
    public Guid Key { get; init; }
    public string OperationType { get; init; } = default!;
    public string? ResponseJson { get; set; }
    public DateTime CreatedAt { get; init; }
    public DateTime ExpiresAt { get; init; }
}
```

## Testing Strategy

- **Unit Tests**: Focus on handlers (Application layer)
- **Integration Tests**: SignalR hub methods with in-memory database
- **No mocking EF Core**: Use in-memory database or Testcontainers

## Plan Review: 2026-02-26

### Summary
A planning review was conducted comparing all non-completed tasks in agent-action-todo.md against ARCHITECTURE.md, AGENTS.md, and current implementation.

### Key Findings

1. **GAP-4 is Critical**: AppHub.cs sends notifications directly via `Clients.Others` AND handlers write to the outbox table. This causes **double notifications**. The direct hub calls must be removed per ADR-005 (Outbox Pattern). Only the OutboxProcessorService should trigger SignalR notifications.

2. **Phase 2.3 auto-refresh is already implemented** — the event subscriptions and handlers exist in MainWindowViewModel.cs. The only real gap is thread marshalling (Dispatcher.UIThread).

3. **Phase 3.2 Authentik tasks reference Keycloak concepts** (realm-export.json). Authentik uses Blueprints, not realm exports. Tasks revised accordingly.

4. **Phase 4.1 is already complete** — all projects are in the solution, build order is automatic via project references.

5. **IAppHub interface diverges** from ARCHITECTURE.md — missing `SubscribeToItemAsync`, `UnsubscribeFromItemAsync`, `GetOnlineUsersAsync`, and `UpdateItemAsync` signature differs.

6. **agent-action-internal.md itself** uses Keycloak terminology that should be Authentik per AGENTS.md.

### Priority Order for Remaining Work

**Must Fix (before MVP usable):**
1. GAP-4: Remove direct `Clients.Others` calls from AppHub.cs (outbox should be sole notification source)
2. Phase 1.6: HealthEndpoints.cs + AuthEndpoints.cs + OidcTokenService.cs (completes server API surface)
3. Phase 2.3: Add Dispatcher.UIThread wrapping in MainWindowViewModel event handlers

**Should Do (improves quality):**
4. Phase 1.6: Dockerfile
5. Phase 4.1: launchSettings.json
6. Phase 3.2: .env.example file
7. Phase 5.3: Custom retry policy for HubConnectionService

**Nice to Have (post-MVP):**
8. Phase 3.2: Authentik Blueprint for automated provisioning
9. GAP-1: Add Subscribe/Unsubscribe/GetOnlineUsers to IAppHub
10. GAP-2/GAP-7: OnForceDisconnect + Graceful Shutdown
11. Phase 5.1: Rate Limiting
12. Phase 5.2: Test Infrastructure
13. Phase 5.3: Polly resilience + ErrorViewModel
14. Phase 3.2: Production OIDC documentation
