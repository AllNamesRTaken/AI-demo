# Implementation Agent - Task List

## Phase 5: Production Hardening (Optional - Post-MVP)

### 5.1 Rate Limiting (Recommended for Production)
- [x] Create `Middleware/SignalRRateLimitingConfiguration.cs` - Rate limit policies
- [x] Update `Program.cs` - Register rate limiting services
- [x] Add `[EnableRateLimiting]` attributes to AppHub methods
- [x] Add `OnRateLimitExceeded` callback to IAppHubClient (was already present)

### 5.2 Testing Infrastructure
- [ ] Create `tests/AiDemo.Application.Tests/` project
  - xUnit test project targeting net10.0
  - Reference AiDemo.Application, AiDemo.Infrastructure, AiDemo.Contracts
  - Add packages: `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, `FluentAssertions`, `NSubstitute` (or use in-memory DB per AGENTS.md "no mocking EF Core")
  - Add `Microsoft.EntityFrameworkCore.InMemoryDatabase` or `Testcontainers.PostgreSql` for DB tests
  - Add project to solution file
- [ ] Create `tests/AiDemo.Server.Tests/` project
  - xUnit test project targeting net10.0
  - Reference AiDemo.Server, AiDemo.Contracts
  - Add packages: `Microsoft.AspNetCore.Mvc.Testing`, `Microsoft.AspNetCore.SignalR.Client`
  - Needed for SignalR integration tests
  - Add project to solution file
- [ ] Add in-memory database helper for tests
  - Create `TestDbContextFactory` that configures `ApplicationDbContext` with `UseInMemoryDatabase("TestDb_" + Guid.NewGuid())`
  - Alternatively, if Testcontainers is used, create a `PostgresFixture` class using `Testcontainers.PostgreSql`
  - Ensure each test gets an isolated database instance
  - Per AGENTS.md: prefer in-memory database or Testcontainers over mocking EF Core
- [ ] Create sample integration test for `CreateItemCommand`
  - Test happy path: create item → verify returned DTO has correct properties
  - Test validation: empty name → expect validation exception
  - Test idempotency: send same command with same key twice → get same result
  - Test outbox: after create → verify OutboxMessage was added to DbContext
  - Use in-memory DB with real `CreateItemHandler`
- [ ] Create SignalR hub integration tests
  - Use `WebApplicationFactory<Program>` to spin up test server
  - Connect with `HubConnectionBuilder` using test JWT token
  - Test `CreateItemAsync` end-to-end (hub → mediator → DB → response)
  - Test `GetItemsAsync` returns items
  - Test that `OnItemCreated` callback fires for other clients
  - **Complex:** Requires generating valid JWT tokens for test auth — create a `TestJwtTokenHelper` class
  - Consider adding a test-only auth bypass scheme or using Bogus JWT with known signing key

### 5.3 Client Resilience
- [x] Create `Services/RetryPolicyService.cs` - Polly v8 resilience pipeline (IHubOperationPolicy)
- [x] Update `HubConnectionService.cs` - Custom retry policy, ForceDisconnect handler, connection state events
- [x] Create `ViewModels/ErrorViewModel.cs` - User-friendly error display, wired to ErrorReceived + RateLimitExceeded

---

## Implementation Order (Critical Path)

```
1. AiDemo.Domain        ──┐
2. AiDemo.Contracts     ──┼── Can be done in parallel (no deps)
3. AiDemo.Application   ──┤── Depends on Domain
4. AiDemo.Infrastructure ─┤── Depends on Domain, Application
5. AiDemo.Server        ──┤── Depends on all above [CHECKPOINT: Must validate]
6. AvaloniaApp updates  ──┤── Depends on Contracts [CHECKPOINT: Test connection]
7. Docker/Authentik     ──┼── Independent [CHECKPOINT: Full stack runs]
8. Phase 5 (Optional)   ──┘── Post-MVP hardening
```

---

## Quality Checklist

Before marking complete, verify:
- [x] All projects build successfully
- [x] Solution references are correct
- [x] No MediatR syntax used (only martinothamar/Mediator)
- [x] No REST endpoints for business logic
- [x] All DTOs in Contracts project only
- [x] Idempotency keys on all mutating commands
- [x] Outbox pattern in handlers for notifications
- [x] `[Authorize]` on AppHub class
- [x] Health endpoints allow anonymous access
- [x] Docker compose starts all services (PostgreSQL running)
- [x] Authentik OIDC provider properly configured for local development
- [ ] Rate limiting implementation (optional for MVP, recommended for production)
  - [ ] 5.2 Testing Infrastructure (not implemented)
  - [x] Rate limiting policies (5.1)
  - [x] Client resilience / Polly (5.3)

---

## Gaps Found: ARCHITECTURE.md vs Current Implementation

> These are deviations between what ARCHITECTURE.md specifies and what currently exists.
> Some are addressed by non-completed tasks above; others are **new items** not in the original plan.

### GAP-1: IAppHub Interface Mismatch (MEDIUM Priority)
**Architecture specifies** (ARCHITECTURE.md line ~470):
- `SubscribeToItemAsync(Guid itemId)` — for targeted real-time updates via SignalR groups
- `UnsubscribeFromItemAsync(Guid itemId)` — leave item group
- `GetOnlineUsersAsync()` — returns `List<UserPresenceDto>`
- `UpdateItemAsync(Guid id, UpdateItemDto dto, ...)` — takes `id` as separate param

**Current `IAppHub.cs`** is missing `SubscribeToItemAsync`, `UnsubscribeFromItemAsync`, `GetOnlineUsersAsync`. Also, `UpdateItemAsync` takes only `UpdateItemDto` (which contains `Id` inside the DTO) instead of `Guid id, UpdateItemDto dto` as separate parameters.

**Action:** Decide whether to add subscription/presence methods now (adds complexity) or defer. The `UpdateItemDto` shape is a design choice — current approach (Id inside DTO) is acceptable but diverges from ARCHITECTURE.md.

### GAP-2: IAppHubClient Missing `OnForceDisconnect` (LOW Priority)
**Architecture specifies**: `Task OnForceDisconnect(string reason)` callback for graceful shutdown.
**Current implementation**: Not present in `IAppHubClient.cs`.
**Action:** Add to IAppHubClient. Also add server-side graceful shutdown logic in Program.cs (ARCHITECTURE.md "Graceful Shutdown" section).

### GAP-3: AppHub Missing Group-Based Notifications (MEDIUM Priority)
**Architecture specifies**: `Clients.Group($"item:{id}").OnItemUpdated(item)` — use SignalR groups for targeted notifications.
**Current implementation**: Uses `Clients.Others` which broadcasts to ALL connected clients.
**Action:** Only relevant once Subscribe/Unsubscribe methods exist. For MVP, `Clients.Others` is acceptable.

### GAP-4: Outbox Pattern Not Fully Wired (HIGH Priority - Architectural)
**Architecture specifies**: Handlers should use the outbox table for notifications (ADR-005), NOT direct `Clients.Others` calls.
**Current AppHub.cs**: Calls `await Clients.Others.OnItemCreated(result)` directly in hub methods (e.g., line 43).
**Current handlers (e.g., CreateItemHandler)**: Already add to `OutboxMessages` table correctly.
**Problem**: Notifications happen TWICE — once via direct hub call AND once when outbox processor picks up the message.
**Action:** Remove the direct `Clients.Others.OnItemCreated/Updated/Deleted` calls from AppHub.cs. The OutboxProcessorService should be the SOLE source of SignalR notifications. This is the most architecturally critical gap.

### GAP-5: `appsettings.Development.json` Uses `Jwt:Key` Instead of `Oidc` (LOW Priority)
**Current**: Has a `Jwt` section with symmetric key for dev testing.
**Architecture expects**: Only `Oidc:Authority`/`Oidc:ClientId`.
**Analysis**: The symmetric key approach is a dev shortcut. The `appsettings.json` already has `Oidc` section. The `Program.cs` already reads `Oidc:Authority`. The `Jwt` section appears unused by current code. Consider removing the `Jwt` section from dev settings to avoid confusion.

### GAP-6: No `launchSettings.json` for Server (LOW Priority)
**Missing**: `src/AiDemo.Server/Properties/launchSettings.json` for consistent dev experience.
**Action:** Create with HTTP profile on port 5000.

### GAP-7: Graceful Shutdown Not Implemented (LOW Priority - Post-MVP)
**Architecture specifies** (ADR-009): Server notifies clients before shutdown, drain period, health check returns unhealthy during shutdown.
**Current**: No shutdown logic.
**Action:** Add `ApplicationStopping` handler in Program.cs per ARCHITECTURE.md "Graceful Shutdown" section. Low priority for MVP.

### GAP-8: agent-action-internal.md References Keycloak (CLEANUP)
**Issue**: The `agent-action-internal.md` file still references "Keycloak" in multiple places (including section headers, configuration examples, etc.) but AGENTS.md mandates Authentik for local dev.
**Action:** Update all Keycloak references to Authentik in agent-action-internal.md. The file also lists `KeycloakTokenService.cs` in the project structure which should be `OidcTokenService.cs`.
