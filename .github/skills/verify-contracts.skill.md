---
description: 'Verifies that IAppHub and IAppHubClient interfaces are fully and correctly implemented in AppHub.cs (server) and HubConnectionService.cs (client).'
tools: ['read', 'search']
---

# Skill: verify-contracts

Cross-check the hub contracts in `AiDemo.Contracts` against their implementations on both the server (`AppHub.cs`) and the client (`HubConnectionService.cs`), reporting any gaps or mismatches.

## Usage

- `verify-contracts` — check all methods in both interfaces
- `verify-contracts: IAppHub` — check server-side implementation only
- `verify-contracts: IAppHubClient` — check client-side callbacks only

## Procedure

Read all four files in parallel:

| File | Role |
|------|------|
| `src/AiDemo.Contracts/Hubs/IAppHub.cs` | Source of truth — server-callable methods |
| `src/AiDemo.Contracts/Hubs/IAppHubClient.cs` | Source of truth — client callback methods |
| `src/AiDemo.Server/Hubs/AppHub.cs` | Must implement every `IAppHub` method |
| `src/AvaloniaApp/Services/HubConnectionService.cs` | Must have an `InvokeAsync` wrapper for every `IAppHub` method and a `connection.On<>` registration for every `IAppHubClient` callback |

## Checks to perform

### 1. IAppHub → AppHub.cs (server implementation)

For each method declared in `IAppHub`:
- ✅ `AppHub` has a `public` method with the **exact same name and signature**
- ✅ Mutating methods (`Create*`, `Update*`, `Delete*`) accept `Guid? idempotencyKey = null`
- ✅ The method dispatches through `_mediator.Send(...)` — no direct DB/SignalR calls
- ✅ Mutations write to the outbox rather than calling `Clients.Others.*` directly

### 2. IAppHubClient → HubConnectionService.cs (client callbacks)

For each method declared in `IAppHubClient`:
- ✅ `HubConnectionService` has a `_connection.On<>(nameof(IAppHubClient.<Method>), ...)` registration inside `RegisterCallbacks()`
- ✅ An event (`EventHandler<T>`) is declared and raised from the callback

### 3. IAppHub → HubConnectionService.cs (client call wrappers)

For each method declared in `IAppHub`:
- ✅ `HubConnectionService` has a `public async Task` (or `Task<T>`) wrapper that calls `_connection!.InvokeAsync(nameof(IAppHub.<Method>), ...)`
- ✅ The wrapper passes `idempotencyKey` for mutating methods
- ✅ The wrapper calls `EnsureConnected()` before invoking

## Output format

```
## Contract Verification Report

### IAppHub → AppHub.cs

| Method | Implemented | Idempotency key | Mediator dispatch | Outbox used |
|--------|:-----------:|:---------------:|:-----------------:|:-----------:|
| CreateItemAsync | ✅ | ✅ | ✅ | ✅ |
| ...   | ...         | ...             | ...               | ...         |

Missing implementations: <list or "None">
Violations: <list or "None">

---

### IAppHubClient → HubConnectionService.cs (callbacks)

| Callback | On<> registered | Event declared & raised |
|----------|:---------------:|:-----------------------:|
| OnItemCreated | ✅ | ✅ |
| ...           | ...             | ...                     |

Missing registrations: <list or "None">

---

### IAppHub → HubConnectionService.cs (invoke wrappers)

| Method | Wrapper exists | InvokeAsync used | idempotencyKey forwarded | EnsureConnected called |
|--------|:--------------:|:----------------:|:------------------------:|:----------------------:|
| CreateItemAsync | ✅ | ✅ | ✅ | ✅ |
| ...             | ...            | ...              | ...                      | ...                    |

Missing wrappers: <list or "None">

---

### Summary

Overall: ✅ FULLY IN SYNC | ❌ GAPS FOUND

<If gaps found: list each gap as an actionable fix, e.g.:>
- [ ] Add `On<Feature>Created` registration in `HubConnectionService.RegisterCallbacks()`
- [ ] Add `Delete<Feature>Async` wrapper in `HubConnectionService`
```
