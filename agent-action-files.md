# Implementation Agent — Project Files Overview (Multiplayer Flappy Bird)

## Target Project Structure

```
AI-demo/
├── AI-demo.sln
├── ARCHITECTURE.md
├── AGENTS.md
├── README.md
├── docker-compose.yml
├── .gitignore
│
├── src/
│   ├── AiDemo.Domain/                       # Core entities (NO dependencies)
│   │   ├── Entities/
│   │   │   └── GameScore.cs                 # NEW: persisted end-of-game score
│   │   ├── Exceptions/
│   │   │   └── DomainException.cs
│   │   └── Interfaces/
│   │       └── IUnitOfWork.cs
│   │
│   ├── AiDemo.Contracts/                    # SHARED DTOs & Hub Interfaces (NO dependencies)
│   │   ├── DTOs/
│   │   │   ├── Game/
│   │   │   │   ├── PlayerStateDto.cs        # NEW: bird Y, velocity, alive, score
│   │   │   │   ├── PipeDto.cs               # NEW: x position, gapTopY
│   │   │   │   ├── GameStateDto.cs          # NEW: tick, players, pipes, status
│   │   │   │   ├── GameRoomDto.cs           # NEW: roomId, players, status
│   │   │   │   ├── GameResultDto.cs         # NEW: end-of-game rankings
│   │   │   │   └── JoinRoomDto.cs           # NEW: passphrase, displayName
│   │   │   ├── NotificationDto.cs           # KEEP
│   │   │   ├── ErrorDto.cs                  # KEEP
│   │   │   ├── RateLimitInfo.cs             # KEEP (used by IGameHubClient)
│   │   │   └── UserPresenceDto.cs           # KEEP
│   │   ├── Enums/
│   │   │   └── GameStatus.cs                # NEW: Waiting/Countdown/Running/Ended
│   │   ├── Hubs/
│   │   │   ├── IGameHub.cs                  # NEW (replaces IAppHub)
│   │   │   └── IGameHubClient.cs            # NEW (replaces IAppHubClient, keeps system callbacks)
│   │   ├── Requests/
│   │   │   └── RefreshTokenRequest.cs       # KEEP
│   │   └── Responses/
│   │       └── TokenResponse.cs             # KEEP
│   │
│   ├── AiDemo.Application/                  # Business logic (depends on Domain)
│   │   ├── Commands/
│   │   │   ├── JoinRoom/
│   │   │   │   ├── JoinRoomCommand.cs       # NEW (mediator-path)
│   │   │   │   └── JoinRoomHandler.cs       # NEW
│   │   │   └── LeaveRoom/
│   │   │       ├── LeaveRoomCommand.cs      # NEW (mediator-path)
│   │   │       └── LeaveRoomHandler.cs      # NEW
│   │   ├── Queries/
│   │   │   └── GetLeaderboard/
│   │   │       ├── GetLeaderboardQuery.cs   # NEW (mediator-path)
│   │   │       └── GetLeaderboardHandler.cs # NEW
│   │   ├── Behaviors/                       # KEEP all three behaviors
│   │   └── Interfaces/
│   │       ├── IApplicationDbContext.cs     # MODIFY: swap Items→GameScores
│   │       ├── IIdempotencyService.cs       # KEEP
│   │       ├── IGameRoomService.cs          # NEW: room lifecycle + direct-path methods
│   │       └── IGameBroadcaster.cs          # NEW: SignalR abstraction (AD-10)
│   │
│   ├── AiDemo.Infrastructure/               # External concerns
│   │   ├── Persistence/
│   │   │   ├── ApplicationDbContext.cs      # MODIFY: remove Items, add GameScores
│   │   │   ├── Configurations/
│   │   │   │   ├── GameScoreConfiguration.cs   # NEW
│   │   │   │   ├── OutboxMessageConfiguration.cs # KEEP
│   │   │   │   └── IdempotencyRecordConfiguration.cs # KEEP
│   │   │   ├── IdempotencyRecord.cs         # KEEP
│   │   │   └── Outbox/
│   │   │       └── OutboxMessage.cs         # KEEP
│   │   ├── Migrations/                      # ADD new migration for GameScores
│   │   └── Services/
│   │       ├── GamePhysicsEngine.cs         # NEW: static physics helpers
│   │       ├── GameRoomService.cs           # NEW: singleton, PeriodicTimer game loop, injects IGameBroadcaster
│   │       ├── OutboxProcessorService.cs    # KEEP
│   │       ├── DateTimeService.cs           # KEEP
│   │       └── IdempotencyService.cs        # KEEP
│   │
│   ├── AiDemo.Server/                       # ASP.NET Core SignalR Server
│   │   ├── Program.cs                       # MODIFY: map /hubs/game, register GameRoomService
│   │   ├── appsettings.json                 # KEEP
│   │   ├── appsettings.Development.json     # KEEP
│   │   ├── Dockerfile                       # KEEP
│   │   ├── Hubs/
│   │   │   └── GameHub.cs                   # NEW (replaces AppHub.cs)
│   │   ├── Endpoints/
│   │   │   ├── HealthEndpoints.cs           # KEEP
│   │   │   └── AuthEndpoints.cs             # KEEP
│   │   ├── Middleware/
│   │   │   └── SignalRRateLimitingConfiguration.cs  # KEEP
│   │   └── Services/
│   │       ├── OidcTokenService.cs          # KEEP
│   │       ├── SignalRGameBroadcaster.cs    # NEW: implements IGameBroadcaster (AD-10)
│   │       └── OutboxNotificationDispatcher.cs # MODIFY: switch to GameHub context, handle GameEnded
│   │
│   └── AvaloniaApp/                         # Avalonia Desktop Client
│       ├── App.axaml.cs                     # MODIFY: register GameHubService
│       ├── Controls/
│       │   └── GameCanvas.cs                # NEW: Avalonia Control, overrides Render()
│       ├── Services/
│       │   ├── IGameHubService.cs           # NEW
│       │   ├── GameHubService.cs            # NEW: wraps SignalR game methods + events
│       │   ├── HubConnectionService.cs      # MODIFY: update hub URL
│       │   ├── IHubConnectionService.cs     # KEEP base
│       │   ├── AuthService.cs               # KEEP
│       │   ├── IAuthService.cs              # KEEP
│       │   ├── IdempotencyKeyService.cs     # KEEP
│       │   └── RetryPolicyService.cs        # KEEP
│       ├── ViewModels/
│       │   ├── MainWindowViewModel.cs       # MODIFY: view navigation
│       │   ├── LobbyViewModel.cs            # NEW
│       │   ├── GameViewModel.cs             # NEW
│       │   ├── LeaderboardViewModel.cs      # NEW
│       │   ├── LoginViewModel.cs            # KEEP
│       │   ├── ErrorViewModel.cs            # KEEP
│       │   └── ViewModelBase.cs             # KEEP
│       └── Views/
│           ├── MainWindow.axaml             # MODIFY: ContentControl view switcher
│           ├── LobbyView.axaml              # NEW
│           ├── GameView.axaml               # NEW (hosts GameCanvas)
│           ├── LeaderboardView.axaml        # NEW
│           └── LoginView.axaml              # KEEP
```

## Key Files Purpose

| File | Status | Purpose |
|------|--------|---------|
| `AiDemo.Contracts/Hubs/IGameHub.cs` | NEW | Client→Server: join, leave, ready, flap, leaderboard |
| `AiDemo.Contracts/Hubs/IGameHubClient.cs` | NEW | Server→Client: room updates, game ticks, results |
| `AiDemo.Contracts/DTOs/Game/GameStateDto.cs` | NEW | Full game world state broadcast each tick |
| `AiDemo.Domain/Entities/GameScore.cs` | NEW | Persisted end-of-game score for leaderboard |
| `AiDemo.Application/Interfaces/IGameRoomService.cs` | NEW | Contract for room lifecycle + direct-path methods (EnqueueFlap, SetPlayerReady) |
| `AiDemo.Application/Interfaces/IGameBroadcaster.cs` | NEW | Abstraction over SignalR hub context (AD-10: breaks circular dep) |
| `AiDemo.Infrastructure/Services/GameRoomService.cs` | NEW | Singleton: rooms, PeriodicTimer game loop, injects IGameBroadcaster |
| `AiDemo.Infrastructure/Services/GamePhysicsEngine.cs` | NEW | Pure physics: gravity, collision, pipe gen |
| `AiDemo.Server/Hubs/GameHub.cs` | NEW | SignalR hub; direct-path (Flap, Ready) + mediator-path (Join, Leave, Leaderboard) |
| `AiDemo.Server/Services/SignalRGameBroadcaster.cs` | NEW | Implements IGameBroadcaster using IHubContext |
| `AiDemo.Server/Services/OutboxNotificationDispatcher.cs` | MODIFY | Switch to GameHub context, handle GameEnded type |
| `AiDemo.Server/Program.cs` | MODIFY | Maps `/hubs/game`, registers GameRoomService + SignalRGameBroadcaster |
| `AvaloniaApp/Controls/GameCanvas.cs` | NEW | Renders game world from GameStateDto |
| `AvaloniaApp/Services/GameHubService.cs` | NEW | Client SignalR connection for game hub |
| `AvaloniaApp/ViewModels/LobbyViewModel.cs` | NEW | Passphrase entry, player list, ready toggle |
| `AvaloniaApp/ViewModels/GameViewModel.cs` | NEW | Holds latest state, flap command |
| `AvaloniaApp/Views/GameView.axaml` | NEW | Game canvas + HUD layout |

## Project Dependencies Graph (unchanged)

```
                    ┌──────────────────┐
                    │  AiDemo.Domain   │ (no dependencies)
                    └────────┬─────────┘
                             │
        ┌────────────────────┼────────────────────┐
        │                    │                    │
        ▼                    ▼                    │
┌───────────────┐   ┌─────────────────┐           │
│AiDemo.Contracts│  │AiDemo.Application│◄──────────┘
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
