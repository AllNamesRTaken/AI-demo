# Architecture Documentation

> **Version:** 1.1.0  
> **Last Updated:** January 27, 2026  
> **Status:** Draft

## Table of Contents

- [Overview](#overview)
- [Architecture Decision Records](#architecture-decision-records)
- [System Architecture](#system-architecture)
- [Project Structure](#project-structure)
- [Technology Stack](#technology-stack)
- [Communication Layer](#communication-layer)
- [Authentication & Authorization](#authentication--authorization)
- [Shared Contracts](#shared-contracts)
- [Server Implementation](#server-implementation)
- [Client Implementation](#client-implementation)
- [Health Checks & Monitoring](#health-checks--monitoring)
- [Reliability Patterns](#reliability-patterns)
- [Rate Limiting](#rate-limiting)
- [Client Resilience](#client-resilience)
- [API Versioning](#api-versioning)
- [Graceful Shutdown](#graceful-shutdown)
- [Client Connection Management](#client-connection-management)
- [Deployment](#deployment)
- [Security Considerations](#security-considerations)
- [Future Improvements](#future-improvements)

---

## Overview

This solution implements a **client-server architecture** using **SignalR** for all RPC communication (both server-to-client and client-to-server), enabling seamless interaction whether running locally or remotely.

### Key Principles

- **Single Communication Protocol:** SignalR WebSocket for all business logic RPC
- **Bidirectional Real-time:** Server can push to clients; clients can invoke server methods
- **Strongly-Typed Contracts:** Shared interfaces ensure type safety across client/server boundary
- **OIDC Authentication:** Authentik (local development) or cloud provider (production) for identity management
- **Clean Architecture:** Separation of concerns with Domain, Application, Infrastructure layers

---

## Architecture Decision Records

### ADR-001: SignalR for All RPC Communication

**Status:** Accepted

**Context:** Need unified communication between desktop clients and server that works locally and remotely.

**Decision:** Use SignalR for all RPC calls instead of traditional REST APIs.

**Consequences:**
- ✅ Bidirectional real-time communication
- ✅ Single persistent connection (WebSocket)
- ✅ Built-in reconnection handling
- ✅ Same code works locally and remotely
- ⚠️ Requires health check REST endpoint for monitoring
- ⚠️ More complex debugging (no Swagger)

---

### ADR-002: Mediator Pattern with martinothamar/Mediator

**Status:** Accepted

**Context:** Need CQRS pattern implementation with MIT license for commercial use.

**Decision:** Use [martinothamar/Mediator](https://github.com/martinothamar/Mediator) instead of MediatR.

**Rationale:**
- MIT License (vs MediatR's Apache 2.0 with additional terms)
- Source generator based (better performance, AOT-friendly)
- Compatible API surface
- No runtime reflection

**Consequences:**
- ✅ Clear MIT licensing for all commercial use
- ✅ Better performance via source generators
- ✅ AOT compilation support
- ⚠️ Slightly different registration syntax

---

### ADR-003: Authentik for Local OIDC

**Status:** Accepted

**Context:** Need OIDC-compliant identity provider for both local development and production.

**Decision:** Use Authentik for local development (lightweight, easy setup). Use a cloud OIDC provider (e.g., Azure AD, Auth0, Okta) for production.

**Rationale:**
- Authentik is lightweight, easy to run locally (Docker), and OIDC-compliant
- Simpler setup and better developer experience than Keycloak for local development
- For production, a managed/cloud OIDC provider is preferred for reliability and security

**Consequences:**
- ✅ Fast, simple local onboarding (Authentik)
- ✅ No local infrastructure maintenance
- ✅ Production uses enterprise-grade, managed identity
- ⚠️ Developers must configure Authentik locally (see README)
- ⚠️ Production and local OIDC configs may differ

---

### ADR-004: Minimal REST Endpoints

**Status:** Accepted

**Context:** SignalR alone doesn't support standard health checks and token operations.

**Decision:** Add minimal REST endpoints for:
- Health checks (`/health`, `/health/ready`, `/health/live`)
- Token refresh (`/api/auth/refresh`)
- OpenAPI documentation for these endpoints only

**Consequences:**
- ✅ Standard Kubernetes/container health probes
- ✅ Load balancer compatibility
- ✅ Token refresh without reconnection

---

### ADR-005: Outbox Pattern for Reliable Event Delivery

**Status:** Accepted

**Context:** If the server crashes after saving to the database but before sending SignalR notifications, clients miss updates. Need guaranteed event delivery.

**Decision:** Implement the Outbox Pattern using a dedicated outbox table.

**Rationale:**
- Events are stored atomically with domain changes in the same transaction
- Background processor sends events and marks them as processed
- Retry mechanism for failed deliveries
- No external message broker dependency initially

**Consequences:**
- ✅ Guaranteed event delivery (at-least-once semantics)
- ✅ Resilient to server crashes
- ✅ Audit trail of all events
- ⚠️ Events may be delivered more than once (clients must be idempotent)
- ⚠️ Slight delay in event delivery

---

### ADR-006: Idempotency Keys for Commands

**Status:** Accepted

**Context:** Network retries and reconnections could cause duplicate command execution.

**Decision:** All mutating commands include an optional `IdempotencyKey` (Guid). Server tracks processed keys and returns cached responses for duplicates.

**Rationale:**
- Prevents duplicate operations on retry
- Client can safely retry failed requests
- Works across reconnections

**Consequences:**
- ✅ Safe retries for all commands
- ✅ Better user experience during network issues
- ⚠️ Requires idempotency key storage (with TTL cleanup)
- ⚠️ Clients must generate and track keys

---

### ADR-007: Rate Limiting

**Status:** Accepted

**Context:** SignalR connections could be abused with excessive requests. Need to protect server resources.

**Decision:** Implement sliding window rate limiting per user and per connection.

**Rationale:**
- Prevents resource exhaustion
- Fair usage across users
- Protects against accidental and malicious abuse

**Consequences:**
- ✅ Server stability under load
- ✅ Fair resource allocation
- ⚠️ Legitimate high-frequency use cases may need whitelisting

---

### ADR-008: Hub API Versioning

**Status:** Accepted

**Context:** Breaking changes to hub interfaces would break older clients. Need backward compatibility strategy.

**Decision:** Support multiple hub versions via path-based versioning (`/hub/v1`, `/hub/v2`).

**Rationale:**
- Clients can upgrade at their own pace
- Clear deprecation path
- No breaking changes for existing clients

**Consequences:**
- ✅ Backward compatibility
- ✅ Smooth client migration
- ⚠️ Multiple hub implementations to maintain
- ⚠️ Need version sunset policy

---

### ADR-009: Graceful Shutdown

**Status:** Accepted

**Context:** Server shutdown disconnects clients abruptly, causing poor user experience.

**Decision:** Implement graceful shutdown with client notification and drain period.

**Rationale:**
- Clients can reconnect to other instances
- No lost operations during shutdown
- Better user experience during deployments

**Consequences:**
- ✅ Zero-downtime deployments possible
- ✅ Better user experience
- ⚠️ Slightly longer shutdown time

---

## System Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              CLIENTS                                        │
├─────────────────┬─────────────────┬─────────────────┬──────────────────────┤
│  Avalonia App   │   Blazor Web    │   Mobile App    │   Future Client      │
│  (Desktop)      │   (Browser)     │   (MAUI)        │                      │
├─────────────────┴─────────────────┴─────────────────┴──────────────────────┤
│                    Shared.Contracts (DTOs, Hub Interfaces)                  │
├─────────────────────────────────────────────────────────────────────────────┤
│                         SignalR Connection                                  │
│              (WebSocket with JWT Bearer Token from OIDC)                    │
│                                                                             │
│              REST Endpoints: /health, /api/auth/refresh                     │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                           SERVER                                            │
├─────────────────────────────────────────────────────────────────────────────┤
│                      ASP.NET Core 10 Host                                   │
├─────────────────────────────────────────────────────────────────────────────┤
│   SignalR Hub (AppHub)                 │  Minimal API Endpoints             │
│   ┌────────────────────────────────┐   │  ┌────────────────────────────┐   │
│   │  Client → Server RPC           │   │  │  GET  /health              │   │
│   │  Server → Client Callbacks     │   │  │  GET  /health/ready        │   │
│   └────────────────────────────────┘   │  │  GET  /health/live         │   │
│                                        │  │  POST /api/auth/refresh    │   │
│                                        │  └────────────────────────────┘   │
├─────────────────────────────────────────────────────────────────────────────┤
│                      Application Layer (Mediator)                           │
│                    Commands / Queries / Handlers                            │
├─────────────────────────────────────────────────────────────────────────────┤
│                         Domain Layer                                        │
│                    Entities / Business Rules / Interfaces                   │
├─────────────────────────────────────────────────────────────────────────────┤
│                      Infrastructure Layer                                   │
│                    EF Core / External Services / OIDC Client                │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                  OIDC PROVIDER (Authentik: local, Cloud: prod)              │
├─────────────────────────────────────────────────────────────────────────────┤
│  • OpenID Connect / OAuth 2.0                                               │
│  • User Management & Authentication                                         │
│  • Token Issuance (JWT Access + Refresh Tokens)                             │
│  • Role-Based Access Control (RBAC)                                         │
│  • Multi-Factor Authentication (MFA)                                        │
│  • Identity Federation (LDAP, SAML, Social)                                 │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Project Structure

```
YourApp/
│
├── src/
│   │
│   ├── YourApp.Domain/                      # Core business entities
│   │   ├── Entities/
│   │   │   └── Item.cs
│   │   ├── Enums/
│   │   ├── Events/                          # Domain events
│   │   ├── Exceptions/
│   │   └── Interfaces/
│   │       ├── IRepository.cs
│   │       └── IUnitOfWork.cs
│   │
│   ├── YourApp.Application/                 # Business logic layer
│   │   ├── Commands/
│   │   │   └── CreateItem/
│   │   │       ├── CreateItemCommand.cs
│   │   │       ├── CreateItemHandler.cs
│   │   │       └── CreateItemValidator.cs
│   │   ├── Queries/
│   │   │   └── GetItems/
│   │   │       ├── GetItemsQuery.cs
│   │   │       └── GetItemsHandler.cs
│   │   ├── Behaviors/                       # Pipeline behaviors
│   │   │   ├── ValidationBehavior.cs
│   │   │   ├── LoggingBehavior.cs
│   │   │   └── IdempotencyBehavior.cs       # Idempotency key handling
│   │   ├── Interfaces/
│   │   │   ├── IApplicationDbContext.cs
│   │   │   └── IIdempotencyService.cs
│   │   └── DependencyInjection.cs
│   │
│   ├── YourApp.Infrastructure/              # External concerns
│   │   ├── Persistence/
│   │   │   ├── ApplicationDbContext.cs
│   │   │   ├── Configurations/
│   │   │   ├── Migrations/
│   │   │   └── Outbox/                      # Outbox pattern
│   │   │       ├── OutboxMessage.cs
│   │   │       └── OutboxProcessor.cs
│   │   ├── Services/
│   │   │   ├── DateTimeService.cs
│   │   │   └── IdempotencyService.cs
│   │   └── DependencyInjection.cs
│   │
│   ├── YourApp.Contracts/                   # SHARED: Client & Server
│   │   ├── Hubs/
│   │   │   ├── IAppHub.cs                   # Client → Server methods
│   │   │   └── IAppHubClient.cs             # Server → Client callbacks
│   │   ├── DTOs/
│   │   │   ├── ItemDto.cs
│   │   │   ├── CreateItemDto.cs
│   │   │   └── UpdateItemDto.cs
│   │   ├── Requests/
│   │   │   └── RefreshTokenRequest.cs
│   │   └── Responses/
│   │       ├── TokenResponse.cs
│   │       └── ErrorResponse.cs
│   │
│   ├── YourApp.Server/                      # ASP.NET Core SignalR Server
│   │   ├── Hubs/
│   │   │   ├── AppHub.cs                    # Current version (v1)
│   │   │   └── AppHubV2.cs                  # Future version (v2)
│   │   ├── Endpoints/
│   │   │   ├── HealthEndpoints.cs           # TODO: Extract from Program.cs
│   │   │   └── AuthEndpoints.cs             # TODO: Token refresh endpoint
│   │   ├── Services/
│   │   │   ├── OidcTokenService.cs          # TODO: Token operations
│   │   │   └── OutboxProcessorService.cs    # ✅ Background outbox processor
│   │   ├── Middleware/
│   │   │   └── RateLimitingMiddleware.cs
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   └── appsettings.Development.json
│   │
│   └── YourApp.Client.Avalonia/             # Avalonia Desktop Client
│       ├── Services/
│       │   ├── IHubConnectionService.cs
│       │   ├── HubConnectionService.cs
│       │   ├── HubConnectionPool.cs         # Connection pooling
│       │   ├── IdempotencyKeyService.cs     # Client-side key generation
│       │   ├── IAuthService.cs
│       │   └── AuthService.cs
│       ├── ViewModels/
│       │   ├── MainWindowViewModel.cs
│       │   └── LoginViewModel.cs
│       ├── Views/
│       │   ├── MainWindow.axaml
│       │   └── LoginView.axaml
│       ├── App.axaml
│       └── Program.cs
│
├── tests/
│   ├── YourApp.Domain.Tests/
│   ├── YourApp.Application.Tests/
│   ├── YourApp.Infrastructure.Tests/
│   └── YourApp.Server.Tests/
│
├── docker/
│   ├── docker-compose.yml                   # Full stack (Server + Authentik + DB)
│   ├── docker-compose.dev.yml               # TODO: Development overrides
│   └── authentik/
│       └── bootstrap.env                    # TODO: Authentik configuration
│
├── ARCHITECTURE.md                          # This document
├── AGENTS.md                                # AI agent instructions
├── README.md
└── YourApp.sln
```

---

## Technology Stack

### Server

| Component | Technology | Version | License |
|-----------|------------|---------|---------|
| Runtime | .NET | 10.0 | MIT |
| Web Framework | ASP.NET Core | 10.0 | MIT |
| Real-time | SignalR | 10.0 | MIT |
| Mediator | martinothamar/Mediator | 2.x | MIT |
| Validation | FluentValidation | 11.x | Apache 2.0 |
| ORM | Entity Framework Core | 10.0 | MIT |
| Database | PostgreSQL | 16.x | PostgreSQL License |
| Health Checks | AspNetCore.HealthChecks | 8.x | Apache 2.0 |
| Logging | Serilog | 4.x | Apache 2.0 |
| OpenAPI | Swashbuckle | 7.x | MIT |

### Client (Avalonia)

| Component | Technology | Version | License |
|-----------|------------|---------|---------|
| UI Framework | Avalonia | 11.x | MIT |
| MVVM | CommunityToolkit.Mvvm | 8.x | MIT |
| SignalR Client | Microsoft.AspNetCore.SignalR.Client | 10.0 | MIT |
| OIDC Client | IdentityModel.OidcClient | 6.x | Apache 2.0 |
| HTTP Client | Refit | 7.x | MIT |
| Resilience | Polly | 8.x | BSD-3-Clause |

### Identity

| Component | Technology | Version | License |
|-----------|------------|---------|---------|
| Identity Provider | Authentik (dev), Cloud OIDC (prod) | 2024.2+ | MIT |
| Protocol | OpenID Connect | 1.0 | - |

---

## Communication Layer

### SignalR Hub Contracts

#### Client → Server Interface (IAppHub)

```csharp
// Location: YourApp.Contracts/Hubs/IAppHub.cs

/// <summary>
/// Defines methods that clients can invoke on the server.
/// Mutating operations include optional idempotency keys for safe retries.
/// </summary>
public interface IAppHub
{
    // Queries (no idempotency needed - read-only)
    Task<List<ItemDto>> GetItemsAsync(CancellationToken cancellationToken = default);
    Task<ItemDto?> GetItemByIdAsync(Guid id, CancellationToken cancellationToken = default);
    
    // Commands (with idempotency key support)
    Task<ItemDto> CreateItemAsync(
        CreateItemDto dto, 
        Guid? idempotencyKey = null,  // Optional: client-generated key for safe retries
        CancellationToken cancellationToken = default);
    
    Task<ItemDto> UpdateItemAsync(
        Guid id, 
        UpdateItemDto dto, 
        Guid? idempotencyKey = null,
        CancellationToken cancellationToken = default);
    
    Task DeleteItemAsync(
        Guid id, 
        Guid? idempotencyKey = null,
        CancellationToken cancellationToken = default);
    
    // Subscriptions (for targeted real-time updates)
    Task SubscribeToItemAsync(Guid itemId);
    Task UnsubscribeFromItemAsync(Guid itemId);
    
    // User presence
    Task<List<UserPresenceDto>> GetOnlineUsersAsync();
}
```

#### Server → Client Interface (IAppHubClient)

```csharp
// Location: YourApp.Contracts/Hubs/IAppHubClient.cs

/// <summary>
/// Defines callbacks that the server can invoke on connected clients.
/// </summary>
public interface IAppHubClient
{
    // Item events
    Task OnItemCreated(ItemDto item);
    Task OnItemUpdated(ItemDto item);
    Task OnItemDeleted(Guid id);
    
    // Notifications
    Task OnNotification(NotificationDto notification);
    Task OnError(ErrorDto error);
    
    // Presence
    Task OnUserConnected(UserPresenceDto user);
    Task OnUserDisconnected(Guid userId);
    
    // System
    Task OnForceDisconnect(string reason);
}
```

### MessagePack Protocol

SignalR uses MessagePack binary protocol for efficient serialization:

```csharp
// Server configuration
builder.Services.AddSignalR()
    .AddMessagePackProtocol(options =>
    {
        options.SerializerOptions = MessagePackSerializerOptions.Standard
            .WithSecurity(MessagePackSecurity.UntrustedData)
            .WithCompression(MessagePackCompression.Lz4BlockArray);
    });

// Client configuration
var connection = new HubConnectionBuilder()
    .WithUrl(hubUrl, options => { /* ... */ })
    .AddMessagePackProtocol()
    .Build();
```

---

## Authentication & Authorization

### OIDC Provider Configuration

#### Local Development (Authentik)

Use Authentik as the OIDC provider for local development. See README for setup instructions and example configuration export.

#### Production (Cloud OIDC)

Use your organization's cloud OIDC provider (e.g., Azure AD, Auth0, Okta). Configuration will differ; see production deployment docs.

#### Server JWT Validation

```csharp
// Program.cs
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Oidc:Authority"];
        options.Audience = builder.Configuration["Oidc:Audience"];
        options.RequireHttpsMetadata = builder.Environment.IsProduction();
        
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
        
        // SignalR WebSocket token handling
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                
                if (!string.IsNullOrEmpty(accessToken) && 
                    path.StartsWithSegments("/hub"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });
```

### Client OIDC Flow (Authorization Code + PKCE)

```csharp
// AuthService.cs
public class AuthService : IAuthService
{
    private readonly OidcClient _oidcClient;
    private TokenResponse? _tokens;
    
    public AuthService(IConfiguration configuration)
    {
        var options = new OidcClientOptions
        {
            Authority = configuration["Oidc:Authority"],
            ClientId = configuration["Oidc:ClientId"],
            Scope = "openid profile email offline_access",
            RedirectUri = "yourapp://callback",
            PostLogoutRedirectUri = "yourapp://logout-callback",
            Browser = new SystemBrowser(),
            Policy = new Policy
            {
                RequireIdentityTokenSignature = true
            }
        };
        
        _oidcClient = new OidcClient(options);
    }
    
    public async Task<LoginResult> LoginAsync()
    {
        var result = await _oidcClient.LoginAsync(new LoginRequest());
        
        if (!result.IsError)
        {
            _tokens = new TokenResponse
            {
                AccessToken = result.AccessToken,
                RefreshToken = result.RefreshToken,
                ExpiresAt = result.AccessTokenExpiration
            };
        }
        
        return new LoginResult(
            Success: !result.IsError,
            Error: result.Error,
            User: result.User
        );
    }
    
    public async Task<string?> GetAccessTokenAsync()
    {
        if (_tokens == null) return null;
        
        // Refresh if expiring within 60 seconds
        if (_tokens.ExpiresAt <= DateTime.UtcNow.AddSeconds(60))
        {
            await RefreshTokenAsync();
        }
        
        return _tokens?.AccessToken;
    }
    
    public async Task RefreshTokenAsync()
    {
        if (_tokens?.RefreshToken == null) return;
        
        var result = await _oidcClient.RefreshTokenAsync(_tokens.RefreshToken);
        
        if (!result.IsError)
        {
            _tokens = new TokenResponse
            {
                AccessToken = result.AccessToken,
                RefreshToken = result.RefreshToken,
                ExpiresAt = DateTime.UtcNow.AddSeconds(result.ExpiresIn)
            };
        }
    }
}
```

---

## Shared Contracts

### DTOs

```csharp
// Location: YourApp.Contracts/DTOs/

public sealed record ItemDto(
    Guid Id,
    string Name,
    string Description,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    Guid CreatedByUserId
);

public sealed record CreateItemDto(
    string Name,
    string Description
);

public sealed record UpdateItemDto(
    string Name,
    string Description
);

public sealed record NotificationDto(
    Guid Id,
    string Title,
    string Message,
    NotificationType Type,
    DateTime Timestamp
);

public sealed record ErrorDto(
    string Code,
    string Message,
    string? TraceId = null,                              // For support correlation
    Dictionary<string, string[]>? ValidationErrors = null
);

public sealed record UserPresenceDto(
    Guid UserId,
    string DisplayName,
    DateTime ConnectedAt
);

// Idempotency tracking
public sealed record IdempotencyRecord(
    Guid Key,
    string OperationType,
    string? ResponseJson,
    DateTime CreatedAt,
    DateTime ExpiresAt
);
```

---

## Server Implementation

### Hub Implementation

```csharp
// Location: YourApp.Server/Hubs/AppHub.cs

[Authorize]
public sealed class AppHub : Hub<IAppHubClient>, IAppHub
{
    private readonly IMediator _mediator;
    private readonly ILogger<AppHub> _logger;
    
    public AppHub(IMediator mediator, ILogger<AppHub> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }
    
    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        _logger.LogInformation("User {UserId} connected", userId);
        
        // Add to user-specific group
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");
        
        // Notify others
        await Clients.Others.OnUserConnected(new UserPresenceDto(
            userId,
            GetDisplayName(),
            DateTime.UtcNow
        ));
        
        await base.OnConnectedAsync();
    }
    
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        _logger.LogInformation("User {UserId} disconnected", userId);
        
        await Clients.Others.OnUserDisconnected(userId);
        
        await base.OnDisconnectedAsync(exception);
    }
    
    public async Task<List<ItemDto>> GetItemsAsync(CancellationToken cancellationToken = default)
    {
        return await _mediator.Send(new GetItemsQuery(GetUserId()), cancellationToken);
    }
    
    public async Task<ItemDto> CreateItemAsync(CreateItemDto dto, CancellationToken cancellationToken = default)
    {
        var command = new CreateItemCommand(dto.Name, dto.Description, GetUserId());
        var item = await _mediator.Send(command, cancellationToken);
        
        // Notify all other connected clients
        await Clients.Others.OnItemCreated(item);
        
        return item;
    }
    
    public async Task<ItemDto> UpdateItemAsync(Guid id, UpdateItemDto dto, CancellationToken cancellationToken = default)
    {
        var command = new UpdateItemCommand(id, dto.Name, dto.Description, GetUserId());
        var item = await _mediator.Send(command, cancellationToken);
        
        // Notify subscribers
        await Clients.Group($"item:{id}").OnItemUpdated(item);
        
        return item;
    }
    
    public async Task DeleteItemAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var command = new DeleteItemCommand(id, GetUserId());
        await _mediator.Send(command, cancellationToken);
        
        // Notify subscribers
        await Clients.Group($"item:{id}").OnItemDeleted(id);
    }
    
    public async Task SubscribeToItemAsync(Guid itemId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"item:{itemId}");
    }
    
    public async Task UnsubscribeFromItemAsync(Guid itemId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"item:{itemId}");
    }
    
    private Guid GetUserId() => 
        Guid.Parse(Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value 
            ?? throw new UnauthorizedAccessException());
    
    private string GetDisplayName() =>
        Context.User?.FindFirst("preferred_username")?.Value 
        ?? Context.User?.FindFirst(ClaimTypes.Name)?.Value 
        ?? "Unknown";
}
```

### Application Layer with Mediator

```csharp
// Location: YourApp.Application/Commands/CreateItem/CreateItemCommand.cs

[GenerateMediator]
public sealed partial record CreateItemCommand(
    string Name,
    string Description,
    Guid UserId
) : ICommand<ItemDto>;

// Location: YourApp.Application/Commands/CreateItem/CreateItemHandler.cs

public sealed class CreateItemHandler : ICommandHandler<CreateItemCommand, ItemDto>
{
    private readonly IApplicationDbContext _context;
    
    public CreateItemHandler(IApplicationDbContext context)
    {
        _context = context;
    }
    
    public async ValueTask<ItemDto> Handle(
        CreateItemCommand command, 
        CancellationToken cancellationToken)
    {
        var item = new Item
        {
            Id = Guid.NewGuid(),
            Name = command.Name,
            Description = command.Description,
            CreatedByUserId = command.UserId,
            CreatedAt = DateTime.UtcNow
        };
        
        _context.Items.Add(item);
        await _context.SaveChangesAsync(cancellationToken);
        
        return new ItemDto(
            item.Id,
            item.Name,
            item.Description,
            item.CreatedAt,
            item.UpdatedAt,
            item.CreatedByUserId
        );
    }
}

// Location: YourApp.Application/Commands/CreateItem/CreateItemValidator.cs

public sealed class CreateItemValidator : AbstractValidator<CreateItemCommand>
{
    public CreateItemValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);
        
        RuleFor(x => x.Description)
            .MaximumLength(2000);
    }
}
```

---

## Health Checks & Monitoring

### Health Check Endpoints

```csharp
// Location: YourApp.Server/Endpoints/HealthEndpoints.cs

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        var health = app.MapGroup("/health")
            .WithTags("Health")
            .AllowAnonymous();
        
        health.MapHealthChecks("/", new HealthCheckOptions
        {
            ResponseWriter = WriteResponse
        });
        
        health.MapHealthChecks("/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = WriteResponse
        });
        
        health.MapHealthChecks("/live", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("live"),
            ResponseWriter = WriteResponse
        });
        
        return app;
    }
    
    private static Task WriteResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        
        var response = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds
            }),
            totalDuration = report.TotalDuration.TotalMilliseconds
        };
        
        return context.Response.WriteAsJsonAsync(response);
    }
}
```

### Auth Token Refresh Endpoint

```csharp
// Location: YourApp.Server/Endpoints/AuthEndpoints.cs

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/auth")
            .WithTags("Authentication");
        
        auth.MapPost("/refresh", RefreshTokenAsync)
            .WithName("RefreshToken")
            .WithDescription("Exchange a refresh token for a new access token")
            .Produces<TokenResponse>()
            .Produces<ErrorDto>(StatusCodes.Status400BadRequest)
            .AllowAnonymous();
        
        return app;
    }
    
    private static async Task<IResult> RefreshTokenAsync(
        RefreshTokenRequest request,
        IOidcTokenService tokenService,
        CancellationToken cancellationToken)
    {
        var result = await tokenService.RefreshTokenAsync(
            request.RefreshToken, 
            cancellationToken);
        
        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }
        
        return Results.BadRequest(new ErrorDto(
            "TOKEN_REFRESH_FAILED",
            result.Error ?? "Failed to refresh token"
        ));
    }
}
```

### Health Check Registration

```csharp
// Program.cs
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "live" })
    .AddNpgSql(
        connectionString, 
        name: "database",
        tags: new[] { "ready", "db" })
    .AddUrlGroup(
        new Uri("${oidcUrl}/health/ready"), 
        name: "oidc-provider",
        tags: new[] { "ready", "identity" })
    .AddSignalRHub(
        hubUrl,
        name: "signalr-hub",
        tags: new[] { "ready" });
```

---

## Client Implementation

### SignalR Hub Connection Service

```csharp
// Location: YourApp.Client.Avalonia/Services/HubConnectionService.cs

public sealed class HubConnectionService : IHubConnectionService, IAsyncDisposable
{
    private HubConnection? _connection;
    private readonly IAuthService _authService;
    private readonly ILogger<HubConnectionService> _logger;
    
    public event Func<ItemDto, Task>? ItemCreated;
    public event Func<ItemDto, Task>? ItemUpdated;
    public event Func<Guid, Task>? ItemDeleted;
    public event Func<NotificationDto, Task>? NotificationReceived;
    public event Func<string, Task>? ConnectionStateChanged;
    
    public HubConnectionState State => _connection?.State ?? HubConnectionState.Disconnected;
    
    public HubConnectionService(
        IAuthService authService,
        ILogger<HubConnectionService> logger)
    {
        _authService = authService;
        _logger = logger;
    }
    
    public async Task ConnectAsync(string hubUrl, CancellationToken cancellationToken = default)
    {
        if (_connection != null)
        {
            await _connection.DisposeAsync();
        }
        
        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.AccessTokenProvider = () => _authService.GetAccessTokenAsync();
            })
            .WithAutomaticReconnect(new RetryPolicy())
            .AddMessagePackProtocol()
            .ConfigureLogging(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Information);
            })
            .Build();
        
        RegisterCallbacks();
        RegisterConnectionEvents();
        
        await _connection.StartAsync(cancellationToken);
        _logger.LogInformation("Connected to SignalR hub");
    }
    
    private void RegisterCallbacks()
    {
        _connection!.On<ItemDto>("OnItemCreated", async item =>
        {
            if (ItemCreated != null)
                await ItemCreated.Invoke(item);
        });
        
        _connection.On<ItemDto>("OnItemUpdated", async item =>
        {
            if (ItemUpdated != null)
                await ItemUpdated.Invoke(item);
        });
        
        _connection.On<Guid>("OnItemDeleted", async id =>
        {
            if (ItemDeleted != null)
                await ItemDeleted.Invoke(id);
        });
        
        _connection.On<NotificationDto>("OnNotification", async notification =>
        {
            if (NotificationReceived != null)
                await NotificationReceived.Invoke(notification);
        });
    }
    
    private void RegisterConnectionEvents()
    {
        _connection!.Closed += async (error) =>
        {
            _logger.LogWarning(error, "Connection closed");
            if (ConnectionStateChanged != null)
                await ConnectionStateChanged.Invoke("Disconnected");
        };
        
        _connection.Reconnecting += async (error) =>
        {
            _logger.LogInformation("Reconnecting...");
            if (ConnectionStateChanged != null)
                await ConnectionStateChanged.Invoke("Reconnecting");
        };
        
        _connection.Reconnected += async (connectionId) =>
        {
            _logger.LogInformation("Reconnected with ID: {ConnectionId}", connectionId);
            if (ConnectionStateChanged != null)
                await ConnectionStateChanged.Invoke("Connected");
        };
    }
    
    // IAppHub implementation
    public Task<List<ItemDto>> GetItemsAsync(CancellationToken cancellationToken = default) =>
        _connection!.InvokeAsync<List<ItemDto>>("GetItemsAsync", cancellationToken);
    
    public Task<ItemDto> CreateItemAsync(CreateItemDto dto, CancellationToken cancellationToken = default) =>
        _connection!.InvokeAsync<ItemDto>("CreateItemAsync", dto, cancellationToken);
    
    public Task<ItemDto> UpdateItemAsync(Guid id, UpdateItemDto dto, CancellationToken cancellationToken = default) =>
        _connection!.InvokeAsync<ItemDto>("UpdateItemAsync", id, dto, cancellationToken);
    
    public Task DeleteItemAsync(Guid id, CancellationToken cancellationToken = default) =>
        _connection!.InvokeAsync("DeleteItemAsync", id, cancellationToken);
    
    public async ValueTask DisposeAsync()
    {
        if (_connection != null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }
    
    private sealed class RetryPolicy : IRetryPolicy
    {
        private static readonly TimeSpan[] _delays = new[]
        {
            TimeSpan.Zero,
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(30)
        };
        
        public TimeSpan? NextRetryDelay(RetryContext retryContext)
        {
            return retryContext.PreviousRetryCount < _delays.Length
                ? _delays[retryContext.PreviousRetryCount]
                : TimeSpan.FromMinutes(1);
        }
    }
}
```

---

## Reliability Patterns

### Outbox Pattern Implementation

The Outbox Pattern ensures reliable event delivery by storing events in the same transaction as domain changes.

#### Outbox Message Entity

```csharp
// Location: YourApp.Infrastructure/Persistence/Outbox/OutboxMessage.cs

public sealed class OutboxMessage
{
    public Guid Id { get; init; }
    public string Type { get; init; } = default!;           // e.g., "ItemCreated"
    public string Payload { get; init; } = default!;        // JSON serialized event
    public DateTime CreatedAt { get; init; }
    public DateTime? ProcessedAt { get; set; }
    public int RetryCount { get; set; }
    public string? LastError { get; set; }
}
```

#### Outbox Processor (Background Service)

```csharp
// Location: YourApp.Infrastructure/Services/OutboxProcessorService.cs

public sealed class OutboxProcessorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<AppHub, IAppHubClient> _hubContext;
    private readonly ILogger<OutboxProcessorService> _logger;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outbox messages");
            }
            
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }
    
    private async Task ProcessOutboxMessagesAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        
        var messages = await context.OutboxMessages
            .Where(m => m.ProcessedAt == null && m.RetryCount < 5)
            .OrderBy(m => m.CreatedAt)
            .Take(100)
            .ToListAsync(ct);
        
        foreach (var message in messages)
        {
            try
            {
                await DispatchMessageAsync(message);
                message.ProcessedAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                message.RetryCount++;
                message.LastError = ex.Message;
                _logger.LogWarning(ex, "Failed to process outbox message {Id}", message.Id);
            }
        }
        
        await context.SaveChangesAsync(ct);
    }
    
    private async Task DispatchMessageAsync(OutboxMessage message)
    {
        switch (message.Type)
        {
            case "ItemCreated":
                var item = JsonSerializer.Deserialize<ItemDto>(message.Payload)!;
                await _hubContext.Clients.All.OnItemCreated(item);
                break;
            // Add other event types...
        }
    }
}
```

#### Using Outbox in Handlers

```csharp
// Location: YourApp.Application/Commands/CreateItem/CreateItemHandler.cs

public async ValueTask<ItemDto> Handle(CreateItemCommand command, CancellationToken ct)
{
    var item = new Item { /* ... */ };
    _context.Items.Add(item);
    
    var dto = new ItemDto(item.Id, item.Name, /* ... */);
    
    // Add to outbox in same transaction
    _context.OutboxMessages.Add(new OutboxMessage
    {
        Id = Guid.NewGuid(),
        Type = "ItemCreated",
        Payload = JsonSerializer.Serialize(dto),
        CreatedAt = DateTime.UtcNow
    });
    
    await _context.SaveChangesAsync(ct);  // Single atomic transaction
    
    return dto;
}
```

### Idempotency Implementation

#### Idempotency Service

```csharp
// Location: YourApp.Infrastructure/Services/IdempotencyService.cs

public interface IIdempotencyService
{
    Task<(bool Exists, T? CachedResponse)> TryGetAsync<T>(Guid key, CancellationToken ct);
    Task SetAsync<T>(Guid key, T response, TimeSpan ttl, CancellationToken ct);
}

public sealed class IdempotencyService : IIdempotencyService
{
    private readonly IApplicationDbContext _context;
    
    public async Task<(bool Exists, T? CachedResponse)> TryGetAsync<T>(Guid key, CancellationToken ct)
    {
        var record = await _context.IdempotencyRecords
            .FirstOrDefaultAsync(r => r.Key == key && r.ExpiresAt > DateTime.UtcNow, ct);
        
        if (record == null)
            return (false, default);
        
        var response = record.ResponseJson != null 
            ? JsonSerializer.Deserialize<T>(record.ResponseJson) 
            : default;
        
        return (true, response);
    }
    
    public async Task SetAsync<T>(Guid key, T response, TimeSpan ttl, CancellationToken ct)
    {
        _context.IdempotencyRecords.Add(new IdempotencyRecord
        {
            Key = key,
            OperationType = typeof(T).Name,
            ResponseJson = JsonSerializer.Serialize(response),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(ttl)
        });
        
        await _context.SaveChangesAsync(ct);
    }
}
```

#### Idempotency Behavior (Pipeline)

```csharp
// Location: YourApp.Application/Behaviors/IdempotencyBehavior.cs

public sealed class IdempotencyBehavior<TRequest, TResponse> 
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IIdempotentCommand<TResponse>
{
    private readonly IIdempotencyService _idempotencyService;
    
    public async ValueTask<TResponse> Handle(
        TRequest request, 
        MessageHandlerDelegate<TRequest, TResponse> next, 
        CancellationToken ct)
    {
        if (request.IdempotencyKey is null)
            return await next(request, ct);
        
        var (exists, cached) = await _idempotencyService
            .TryGetAsync<TResponse>(request.IdempotencyKey.Value, ct);
        
        if (exists && cached is not null)
            return cached;
        
        var response = await next(request, ct);
        
        await _idempotencyService.SetAsync(
            request.IdempotencyKey.Value, 
            response, 
            TimeSpan.FromHours(24), 
            ct);
        
        return response;
    }
}

// Marker interface for idempotent commands
public interface IIdempotentCommand<TResponse> : ICommand<TResponse>
{
    Guid? IdempotencyKey { get; }
}
```

---

## Rate Limiting

### Configuration

```csharp
// Program.cs

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    
    // Global rate limit per user
    options.AddPolicy("signalr-user", context =>
    {
        var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
        
        return RateLimitPartition.GetSlidingWindowLimiter(userId, _ => new SlidingWindowRateLimiterOptions
        {
            Window = TimeSpan.FromMinutes(1),
            SegmentsPerWindow = 6,
            PermitLimit = 100  // 100 requests per minute per user
        });
    });
    
    // Per-connection rate limit (for SignalR)
    options.AddPolicy("signalr-connection", context =>
    {
        var connectionId = context.Connection.Id;
        
        return RateLimitPartition.GetTokenBucketLimiter(connectionId, _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = 20,
            TokensPerPeriod = 10,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1)
        });
    });
});

// Apply to SignalR
app.UseRateLimiter();
```

### Hub Rate Limiting Attribute

```csharp
// Location: YourApp.Server/Hubs/AppHub.cs

[Authorize]
[EnableRateLimiting("signalr-user")]
public sealed class AppHub : Hub<IAppHubClient>, IAppHub
{
    // High-frequency methods can have stricter limits
    [EnableRateLimiting("signalr-connection")]
    public async Task<List<ItemDto>> GetItemsAsync(CancellationToken ct = default)
    {
        // ...
    }
}
```

### Rate Limit Exceeded Response

```csharp
// Clients receive this when rate limited
public interface IAppHubClient
{
    // ... existing methods ...
    
    Task OnRateLimitExceeded(RateLimitInfo info);
}

public sealed record RateLimitInfo(
    int RetryAfterSeconds,
    string Message
);
```

---

## Client Resilience

All hub operations in the Avalonia client are wrapped via `IHubOperationPolicy` (implemented by `HubOperationPolicy` in `AvaloniaApp/Services/RetryPolicyService.cs`). The pipeline uses Polly v8's `ResiliencePipelineBuilder` with three layers applied inside-out:

1. **Timeout** — 30 s hard limit per attempt.
2. **Retry** — up to 4 attempts, exponential backoff starting at 1 s with jitter (1 s → 2 s → 4 s → 8 s).
3. **Circuit Breaker** — opens when ≥ 50 % of calls fail within a 30 s sampling window (minimum 5 calls). Stays open for 30 s before allowing a probe attempt.

`IHubOperationPolicy` is registered as a singleton and can be injected wherever hub calls need protection. `ErrorViewModel` (injected into `MainWindowViewModel`) surfaces circuit-open and rate-limit events to the user as dismissible notifications without blocking the UI thread.

---

## API Versioning

### Hub Version Strategy

```csharp
// Program.cs

// Map versioned hubs
app.MapHub<AppHub>("/hub/v1");
app.MapHub<AppHubV2>("/hub/v2");  // Future version

// Deprecated version with warning header
app.MapHub<AppHubLegacy>("/hub")
   .WithMetadata(new ObsoleteAttribute("Use /hub/v1 or /hub/v2"));
```

### Version-Specific Contracts

```csharp
// Location: YourApp.Contracts/Hubs/V1/IAppHubV1.cs
namespace YourApp.Contracts.Hubs.V1;

public interface IAppHubV1
{
    Task<List<ItemDto>> GetItemsAsync(CancellationToken ct = default);
    Task<ItemDto> CreateItemAsync(CreateItemDto dto, Guid? idempotencyKey = null, CancellationToken ct = default);
    // ... original methods
}

// Location: YourApp.Contracts/Hubs/V2/IAppHubV2.cs
namespace YourApp.Contracts.Hubs.V2;

public interface IAppHubV2
{
    // New: Pagination support
    Task<PagedResult<ItemDto>> GetItemsAsync(int page, int pageSize, CancellationToken ct = default);
    
    // New: Batch operations
    Task<List<ItemDto>> CreateItemsAsync(List<CreateItemDto> dtos, Guid? idempotencyKey = null, CancellationToken ct = default);
    
    // Existing methods maintained for compatibility
    Task<ItemDto> CreateItemAsync(CreateItemDto dto, Guid? idempotencyKey = null, CancellationToken ct = default);
}
```

### Client Version Selection

```csharp
// Location: YourApp.Client.Avalonia/Services/HubConnectionService.cs

public async Task ConnectAsync(string baseUrl, ApiVersion version = ApiVersion.V1, CancellationToken ct = default)
{
    var hubUrl = version switch
    {
        ApiVersion.V1 => $"{baseUrl}/hub/v1",
        ApiVersion.V2 => $"{baseUrl}/hub/v2",
        _ => throw new ArgumentException($"Unsupported API version: {version}")
    };
    
    _connection = new HubConnectionBuilder()
        .WithUrl(hubUrl, options => { /* ... */ })
        .Build();
    
    await _connection.StartAsync(ct);
}

public enum ApiVersion { V1, V2 }
```

### Version Sunset Policy

| Version | Status | Sunset Date | Notes |
|---------|--------|-------------|-------|
| v1 | Current | - | Stable, production use |
| v2 | Preview | - | New features, breaking changes |
| Legacy (/hub) | Deprecated | 2026-06-01 | Migrate to v1 |

---

## Graceful Shutdown

### Server-Side Implementation

```csharp
// Program.cs

var app = builder.Build();

// Register graceful shutdown
app.Lifetime.ApplicationStopping.Register(async () =>
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    var hubContext = app.Services.GetRequiredService<IHubContext<AppHub, IAppHubClient>>();
    
    logger.LogInformation("Server shutting down, notifying clients...");
    
    // Notify all connected clients
    await hubContext.Clients.All.OnForceDisconnect(
        "Server is shutting down for maintenance. Please reconnect in a moment.");
    
    // Give clients time to receive notification and initiate reconnect
    await Task.Delay(TimeSpan.FromSeconds(5));
    
    logger.LogInformation("Graceful shutdown complete");
});

// Configure host shutdown timeout
builder.WebHost.UseShutdownTimeout(TimeSpan.FromSeconds(30));
```

### Client-Side Reconnection Handling

```csharp
// Location: YourApp.Client.Avalonia/Services/HubConnectionService.cs

private void RegisterCallbacks()
{
    // ... existing callbacks ...
    
    _connection!.On<string>("OnForceDisconnect", async reason =>
    {
        _logger.LogWarning("Server requested disconnect: {Reason}", reason);
        
        // Notify UI
        if (ForceDisconnected != null)
            await ForceDisconnected.Invoke(reason);
        
        // The automatic reconnect will handle reconnection
        // But we can show a user-friendly message
    });
}

public event Func<string, Task>? ForceDisconnected;
```

### Health Check During Shutdown

```csharp
// Location: YourApp.Server/Endpoints/HealthEndpoints.cs

public static class HealthEndpoints
{
    private static bool _isShuttingDown = false;
    
    public static void SignalShutdown() => _isShuttingDown = true;
    
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        // ... existing setup ...
        
        // Readiness probe returns unhealthy during shutdown
        health.MapHealthChecks("/ready", new HealthCheckOptions
        {
            Predicate = check => !_isShuttingDown && check.Tags.Contains("ready"),
            ResponseWriter = WriteResponse
        });
        
        return app;
    }
}

// Program.cs - trigger during shutdown
app.Lifetime.ApplicationStopping.Register(() => HealthEndpoints.SignalShutdown());
```

---

## Client Connection Management

### Connection Pool (for multiple simultaneous operations)

```csharp
// Location: YourApp.Client.Avalonia/Services/HubConnectionPool.cs

public sealed class HubConnectionPool : IAsyncDisposable
{
    private readonly ConcurrentBag<HubConnection> _pool = new();
    private readonly SemaphoreSlim _semaphore;
    private readonly string _hubUrl;
    private readonly Func<Task<string?>> _tokenProvider;
    private readonly int _maxConnections;
    
    public HubConnectionPool(
        string hubUrl, 
        Func<Task<string?>> tokenProvider,
        int maxConnections = 3)
    {
        _hubUrl = hubUrl;
        _tokenProvider = tokenProvider;
        _maxConnections = maxConnections;
        _semaphore = new SemaphoreSlim(maxConnections, maxConnections);
    }
    
    public async Task<PooledConnection> AcquireAsync(CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct);
        
        if (!_pool.TryTake(out var connection) || connection.State != HubConnectionState.Connected)
        {
            connection = await CreateConnectionAsync(ct);
        }
        
        return new PooledConnection(connection, this);
    }
    
    internal void Return(HubConnection connection)
    {
        if (connection.State == HubConnectionState.Connected)
        {
            _pool.Add(connection);
        }
        _semaphore.Release();
    }
    
    private async Task<HubConnection> CreateConnectionAsync(CancellationToken ct)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(_hubUrl, options =>
            {
                options.AccessTokenProvider = _tokenProvider;
            })
            .WithAutomaticReconnect()
            .AddMessagePackProtocol()
            .Build();
        
        await connection.StartAsync(ct);
        return connection;
    }
    
    public async ValueTask DisposeAsync()
    {
        foreach (var connection in _pool)
        {
            await connection.DisposeAsync();
        }
    }
}

public sealed class PooledConnection : IAsyncDisposable
{
    public HubConnection Connection { get; }
    private readonly HubConnectionPool _pool;
    
    internal PooledConnection(HubConnection connection, HubConnectionPool pool)
    {
        Connection = connection;
        _pool = pool;
    }
    
    public ValueTask DisposeAsync()
    {
        _pool.Return(Connection);
        return ValueTask.CompletedTask;
    }
}
```

### Connection Strategy by Client Type

| Client Type | Strategy | Max Connections | Notes |
|-------------|----------|-----------------|-------|
| Desktop (Avalonia) | Pool (3) | 3 | Multiple concurrent operations |
| Web (Blazor WASM) | Single | 1 | Browser connection limits |
| Mobile | Single | 1 | Battery/bandwidth optimization |
| Server-to-Server | Pool (10) | 10 | High throughput |

### Client-Side Idempotency Key Generation

```csharp
// Location: YourApp.Client.Avalonia/Services/IdempotencyKeyService.cs

public interface IIdempotencyKeyService
{
    Guid GenerateKey();
    Guid GenerateKey(string operation, params object[] parameters);
}

public sealed class IdempotencyKeyService : IIdempotencyKeyService
{
    public Guid GenerateKey() => Guid.NewGuid();
    
    /// <summary>
    /// Generates a deterministic key based on operation and parameters.
    /// Useful for retry scenarios where the same operation should use the same key.
    /// </summary>
    public Guid GenerateKey(string operation, params object[] parameters)
    {
        var input = $"{operation}:{string.Join(":", parameters.Select(p => p?.ToString() ?? "null"))}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return new Guid(hash.Take(16).ToArray());
    }
}

// Usage in ViewModel
public async Task CreateItemAsync()
{
    var idempotencyKey = _idempotencyService.GenerateKey("CreateItem", ItemName, UserId);
    
    // Safe to retry - same key = same result
    var result = await _hubConnection.CreateItemAsync(
        new CreateItemDto(ItemName, Description), 
        idempotencyKey);
}
```

---

## Deployment

### Docker Compose

```yaml
# docker/docker-compose.yml
version: '3.8'

services:
  server:
    build:
      context: ..
      dockerfile: src/YourApp.Server/Dockerfile
    ports:
      - "5000:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__DefaultConnection=Host=db;Database=yourapp;Username=postgres;Password=${DB_PASSWORD}
      - Oidc__Authority=http://authentik-server:9000/application/o/yourapp/
      - Oidc__Audience=yourapp-server
    depends_on:
      db:
        condition: service_healthy
      authentik:
        condition: service_healthy
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health/live"]
      interval: 30s
      timeout: 10s
      retries: 3
  
  db:
    image: postgres:16-alpine
    environment:
      POSTGRES_DB: yourapp
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: ${DB_PASSWORD}
    volumes:
      - postgres_data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres"]
      interval: 10s
      timeout: 5s
      retries: 5
  
  authentik:
    image: ghcr.io/goauthentik/server:2024.2
    command: server
    environment:
      AUTHENTIK_SECRET_KEY: ${AUTHENTIK_SECRET_KEY}
      AUTHENTIK_POSTGRESQL__HOST: db
      AUTHENTIK_POSTGRESQL__NAME: authentik
      AUTHENTIK_POSTGRESQL__USER: postgres
      AUTHENTIK_POSTGRESQL__PASSWORD: ${DB_PASSWORD}
    ports:
      - "9000:9000"
    depends_on:
      db:
        condition: service_healthy
    healthcheck:
      test: ["CMD", "wget", "--no-verbose", "--tries=1", "--spider", "http://localhost:9000/-/health/live/"]
      interval: 30s
      timeout: 10s
      retries: 3

volumes:
  postgres_data:
```

---

## Security Considerations

### Transport Security

- ✅ **TLS Everywhere:** All connections use HTTPS/WSS in production
- ✅ **HSTS Headers:** Enforce HTTPS via HTTP Strict Transport Security
- ✅ **Certificate Pinning:** Consider for mobile clients

### Authentication Security

- ✅ **PKCE:** Authorization Code flow with PKCE for public clients
- ✅ **Short-lived Tokens:** Access tokens expire in 5 minutes
- ✅ **Token Refresh:** Refresh tokens for session continuity
- ✅ **Token Validation:** Full JWT validation on every request

### SignalR Security

- ✅ **Authorization on Hub:** `[Authorize]` attribute on hub class
- ✅ **Per-method Authorization:** Fine-grained method-level authorization
- ✅ **Connection Validation:** Validate tokens on connection
- ✅ **Group-based Access:** Users only receive relevant updates

### Data Security

- ✅ **Input Validation:** FluentValidation on all commands
- ✅ **SQL Injection Prevention:** Parameterized queries via EF Core
- ✅ **Audit Logging:** Track who did what and when

---

## Appendix: Configuration

### appsettings.json

```json
{
  "Oidc": {
    "Authority": "https://auth.yourdomain.com/application/o/yourapp/",
    "Audience": "yourapp-server",
    "MetadataAddress": "https://auth.yourdomain.com/application/o/yourapp/.well-known/openid-configuration"
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=yourapp;Username=postgres;Password=your-password"
  },
  "SignalR": {
    "EnableDetailedErrors": false,
    "KeepAliveInterval": "00:00:15",
    "ClientTimeoutInterval": "00:00:30"
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft.AspNetCore": "Warning",
        "Microsoft.AspNetCore.SignalR": "Debug"
      }
    }
  }
}
```

---

## Future Improvements

> **Note:** The following improvements are planned for future iterations. They are documented here for reference.

### TODO: Distributed Caching with Redis

**Priority:** High  
**Rationale:** Required for multi-instance deployment and SignalR scale-out.

```csharp
// Future implementation - Redis for SignalR backplane
builder.Services.AddSignalR()
    .AddStackExchangeRedis(builder.Configuration.GetConnectionString("Redis")!);

// Future implementation - Distributed caching
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "YourApp:";
});

// Use cases:
// - SignalR scale-out across multiple server instances
// - Distributed session state
// - Idempotency key storage (instead of database)
// - Rate limiting state sharing
```

**Packages to add:**
```xml
<PackageReference Include="Microsoft.AspNetCore.SignalR.StackExchangeRedis" Version="10.0.0" />
<PackageReference Include="Microsoft.Extensions.Caching.StackExchangeRedis" Version="10.0.0" />
```

---

### TODO: OpenTelemetry for Observability

**Priority:** Medium  
**Rationale:** Distributed tracing is essential for debugging and monitoring in production.

```csharp
// Future implementation - OpenTelemetry
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("YourApp.Server"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddSource("Microsoft.AspNetCore.SignalR")
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri(builder.Configuration["Otlp:Endpoint"]!);
        }))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddOtlpExporter());

// Custom SignalR instrumentation
builder.Services.AddSingleton<SignalRDiagnosticObserver>();
```

**Packages to add:**
```xml
<PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.9.0" />
<PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.9.0" />
<PackageReference Include="OpenTelemetry.Instrumentation.Http" Version="1.9.0" />
<PackageReference Include="OpenTelemetry.Instrumentation.EntityFrameworkCore" Version="1.9.0" />
<PackageReference Include="OpenTelemetry.Instrumentation.Runtime" Version="1.9.0" />
<PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.9.0" />
```

**Observability stack options:**
- **Jaeger** - Open-source, self-hosted tracing
- **Grafana Tempo** - Scalable tracing backend
- **Azure Monitor** - Cloud-managed (if using Azure)
- **Datadog/New Relic** - Commercial APM solutions

---

### Other Considerations for Future

| Improvement | Priority | Notes |
|-------------|----------|-------|
| Event Sourcing | Low | For entities requiring full audit trail (use Marten or EventStoreDB) |
| GraphQL Subscriptions | Low | Alternative to SignalR for web-only scenarios |
| gRPC-Web | Low | Binary protocol alternative for browser clients |
| Multi-tenancy | Medium | If SaaS deployment is needed |
| Blue-Green Deployments | Medium | Zero-downtime deployment strategy |
