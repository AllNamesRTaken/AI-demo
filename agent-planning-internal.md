# Planning Agent - Internal Notes

## Context Analysis

### Current State
- **Solution**: AI-demo.sln with single project `AvaloniaApp`
- **Target Framework**: .NET 10.0
- **Current Architecture**: Minimal Avalonia app with:
  - Basic MVVM (CommunityToolkit.Mvvm)
  - Single MainWindow + MainWindowViewModel
  - No server, no authentication, no SignalR

### Target Architecture (from ARCHITECTURE.md)
Full client-server Clean Architecture:
- SignalR for all RPC (not REST)
- Keycloak for OIDC authentication
- martinothamar/Mediator (NOT MediatR) for CQRS
- Outbox pattern for reliable event delivery
- Idempotency keys for safe retries
- PostgreSQL with EF Core

## Key Design Decisions

### 1. Naming Convention
Per ARCHITECTURE.md examples, use `YourApp` prefix. For this implementation:
- **Project Prefix**: `AiDemo` (matching solution name)
- Alternatively, could use generic `App` prefix

### 2. Minimal Implementation Scope
Create a working minimal app with:
- One entity: `Item` (CRUD operations)
- Full authentication flow with Keycloak
- Real-time updates via SignalR
- Outbox pattern for notifications
- Idempotency for commands

### 3. Project Structure Mapping
| Architecture Doc | Actual Project |
|-----------------|----------------|
| YourApp.Domain | AiDemo.Domain |
| YourApp.Application | AiDemo.Application |
| YourApp.Infrastructure | AiDemo.Infrastructure |
| YourApp.Contracts | AiDemo.Contracts |
| YourApp.Server | AiDemo.Server |
| YourApp.Client.Avalonia | AvaloniaApp (existing, to enhance) |

### 4. Package Dependencies
**Server-side:**
- Mediator.SourceGenerator (martinothamar/Mediator)
- FluentValidation
- Microsoft.EntityFrameworkCore.Design
- Npgsql.EntityFrameworkCore.PostgreSQL
- Microsoft.AspNetCore.Authentication.JwtBearer
- Microsoft.AspNetCore.SignalR
- MessagePack (for SignalR protocol)

**Client-side (AvaloniaApp):**
- Microsoft.AspNetCore.SignalR.Client
- MessagePack.AspNetCoreMvcFormatter
- IdentityModel.OidcClient
- Polly (for resilience)

### 5. Critical Implementation Notes
1. **No REST for business logic** - Only /health and /api/auth/refresh
2. **Use source generators** - martinothamar/Mediator uses `[GenerateMediator]`
3. **DTOs in Contracts only** - Never in Domain/Application
4. **Idempotency keys** - Optional Guid on all mutating commands
5. **Outbox pattern** - Events saved in same transaction as domain changes

## Web Research Notes
- martinothamar/Mediator: MIT license, source generator based, AOT-friendly
- Keycloak: Apache 2.0, full OIDC support, PKCE for public clients
- SignalR: Built-in WebSocket support, automatic reconnection

## Risk Assessment
- **Keycloak setup complexity**: Mitigate with pre-configured realm-export.json
- **EF Core migrations**: Start with simple entity, expand later
- **SignalR debugging**: No Swagger - use logging extensively

## Open Questions for Implementation Agent
1. Should we rename AvaloniaApp project to AiDemo.Client.Avalonia?
2. Which PostgreSQL version constraint? (Architecture says 16.x)
3. Include test projects in initial implementation?
