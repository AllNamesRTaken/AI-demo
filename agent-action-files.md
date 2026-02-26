# Implementation Agent - Project Files Overview

## Target Project Structure

```
AI-demo/
├── AI-demo.sln                              # Enhanced with new projects
├── ARCHITECTURE.md
├── AGENTS.md
├── README.md
│
├── src/
│   ├── AiDemo.Domain/                       # Core business entities (NO dependencies)
│   │   ├── AiDemo.Domain.csproj
│   │   ├── Entities/
│   │   │   └── Item.cs
│   │   ├── Enums/
│   │   │   └── NotificationType.cs
│   │   ├── Events/
│   │   │   └── ItemCreatedEvent.cs
│   │   ├── Exceptions/
│   │   │   └── DomainException.cs
│   │   └── Interfaces/
│   │       ├── IRepository.cs
│   │       └── IUnitOfWork.cs
│   │
│   ├── AiDemo.Contracts/                    # SHARED DTOs & Hub Interfaces (NO dependencies)
│   │   ├── AiDemo.Contracts.csproj
│   │   ├── DTOs/
│   │   │   ├── ItemDto.cs
│   │   │   ├── CreateItemDto.cs
│   │   │   ├── UpdateItemDto.cs
│   │   │   ├── NotificationDto.cs
│   │   │   ├── ErrorDto.cs
│   │   │   └── UserPresenceDto.cs
│   │   ├── Hubs/
│   │   │   ├── IAppHub.cs                   # Client → Server methods
│   │   │   └── IAppHubClient.cs             # Server → Client callbacks
│   │   ├── Requests/
│   │   │   └── RefreshTokenRequest.cs
│   │   └── Responses/
│   │       └── TokenResponse.cs
│   │
│   ├── AiDemo.Application/                  # Business logic (depends on Domain)
│   │   ├── AiDemo.Application.csproj
│   │   ├── DependencyInjection.cs
│   │   ├── Commands/
│   │   │   ├── CreateItem/
│   │   │   │   ├── CreateItemCommand.cs
│   │   │   │   ├── CreateItemHandler.cs
│   │   │   │   └── CreateItemValidator.cs
│   │   │   ├── UpdateItem/
│   │   │   │   ├── UpdateItemCommand.cs
│   │   │   │   ├── UpdateItemHandler.cs
│   │   │   │   └── UpdateItemValidator.cs
│   │   │   └── DeleteItem/
│   │   │       ├── DeleteItemCommand.cs
│   │   │       └── DeleteItemHandler.cs
│   │   ├── Queries/
│   │   │   ├── GetItems/
│   │   │   │   ├── GetItemsQuery.cs
│   │   │   │   └── GetItemsHandler.cs
│   │   │   └── GetItemById/
│   │   │       ├── GetItemByIdQuery.cs
│   │   │       └── GetItemByIdHandler.cs
│   │   ├── Behaviors/
│   │   │   ├── ValidationBehavior.cs
│   │   │   ├── LoggingBehavior.cs
│   │   │   └── IdempotencyBehavior.cs
│   │   └── Interfaces/
│   │       ├── IApplicationDbContext.cs
│   │       └── IIdempotencyService.cs
│   │
│   ├── AiDemo.Infrastructure/               # External concerns (depends on Application)
│   │   ├── AiDemo.Infrastructure.csproj
│   │   ├── DependencyInjection.cs
│   │   ├── Persistence/
│   │   │   ├── ApplicationDbContext.cs
│   │   │   ├── Configurations/
│   │   │   │   ├── ItemConfiguration.cs
│   │   │   │   ├── OutboxMessageConfiguration.cs
│   │   │   │   └── IdempotencyRecordConfiguration.cs
│   │   │   └── Outbox/
│   │   │       └── OutboxMessage.cs
│   │   └── Services/
│   │       ├── DateTimeService.cs
│   │       └── IdempotencyService.cs
│   │
│   ├── AiDemo.Server/                       # ASP.NET Core SignalR Server
│   │   ├── AiDemo.Server.csproj
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   ├── appsettings.Development.json
│   │   ├── Dockerfile
│   │   ├── Hubs/
│   │   │   └── AppHub.cs
│   │   ├── Endpoints/
│   │   │   ├── HealthEndpoints.cs
│   │   │   └── AuthEndpoints.cs
│   │   └── Services/
│   │       ├── KeycloakTokenService.cs
│   │       └── OutboxNotificationService.cs
│   │
│   └── AvaloniaApp/                         # Existing - ENHANCED
│       ├── AvaloniaApp.csproj               # Add SignalR, OIDC packages
│       ├── App.axaml.cs                     # Add DI setup
│       ├── Program.cs                       # Enhanced with service registration
│       ├── Services/
│       │   ├── IHubConnectionService.cs
│       │   ├── HubConnectionService.cs
│       │   ├── IAuthService.cs
│       │   ├── AuthService.cs
│       │   └── IdempotencyKeyService.cs
│       ├── ViewModels/
│       │   ├── MainWindowViewModel.cs       # Enhanced with SignalR
│       │   ├── ViewModelBase.cs
│       │   └── LoginViewModel.cs            # NEW
│       └── Views/
│           ├── MainWindow.axaml             # Enhanced with item list
│           ├── MainWindow.axaml.cs
│           └── LoginView.axaml              # NEW
│
├── docker/
│   ├── docker-compose.yml
│   ├── docker-compose.dev.yml
│   └── keycloak/
│       └── realm-export.json
│
└── tests/                                   # Optional - Phase 2
    ├── AiDemo.Domain.Tests/
    ├── AiDemo.Application.Tests/
    └── AiDemo.Server.Tests/
```

## Key Files Purpose

| File | Purpose |
|------|---------|
| `AiDemo.Domain/Entities/Item.cs` | Core Item entity with validation |
| `AiDemo.Contracts/Hubs/IAppHub.cs` | Client→Server SignalR interface |
| `AiDemo.Contracts/Hubs/IAppHubClient.cs` | Server→Client callbacks |
| `AiDemo.Application/Commands/CreateItem/*` | CQRS command with handler & validator |
| `AiDemo.Infrastructure/Persistence/ApplicationDbContext.cs` | EF Core context with outbox |
| `AiDemo.Server/Hubs/AppHub.cs` | SignalR hub implementing IAppHub |
| `AiDemo.Server/Program.cs` | Server bootstrap with auth, SignalR |
| `AvaloniaApp/Services/HubConnectionService.cs` | Client SignalR connection |
| `AvaloniaApp/Services/AuthService.cs` | Keycloak OIDC authentication |
| `docker/docker-compose.yml` | Full stack deployment |

## Project Dependencies Graph

```
                    ┌──────────────────┐
                    │  AiDemo.Domain   │ (no dependencies)
                    └────────┬─────────┘
                             │
        ┌────────────────────┼────────────────────┐
        │                    │                    │
        ▼                    ▼                    │
┌───────────────┐   ┌─────────────────┐           │
│AiDemo.Contracts│   │AiDemo.Application│◄─────────┘
│ (no deps)     │   │ (→Domain)       │
└───────┬───────┘   └────────┬────────┘
        │                    │
        │           ┌────────▼────────┐
        │           │AiDemo.Infrastructure│
        │           │(→Application,Domain)│
        │           └────────┬────────┘
        │                    │
        ▼                    ▼
┌───────────────┐   ┌─────────────────┐
│  AvaloniaApp  │   │  AiDemo.Server  │
│ (→Contracts)  │   │(→All projects)  │
└───────────────┘   └─────────────────┘
```
