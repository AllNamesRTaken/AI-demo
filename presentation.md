---
marp: true
theme: uncover
class: invert
paginate: true
header: 'AI-Powered Development Demo'
footer: 'SignalR • Clean Architecture • OIDC Authentication'
style: |
  section {
    font-size: 24px;
    line-height: 1.4;
  }
  h2 {
    font-size: 34px;
    margin-bottom: 0.5em;
  }
  h3 {
    font-size: 28px;
    margin-bottom: 0.4em;
  }
  pre {
    font-size: 16px;
    line-height: 1.3;
  }
  ul, ol {
    margin: 0.3em 0;
  }
  li {
    margin: 0.2em 0;
  }
---

# Building a Real-Time Application with AI Assistance

**A Journey Through Clean Architecture, SignalR, and Authentik OIDC**

*Created: February 2026*

---

## Project Overview

### What We Built
- **Real-time desktop application** using Avalonia UI
- **SignalR hub** for bidirectional communication
- **PostgreSQL** database with EF Core
- **Authentik OIDC** authentication
- **Clean Architecture** with CQRS pattern

### Technology Stack
- .NET 10.0
- Avalonia 11.3
- SignalR with MessagePack
- PostgreSQL 16
- Authentik (OIDC Provider)
- Docker Compose

---

## Architecture: Clean Architecture + CQRS

```
┌─────────────────────────────────────────────────┐
│              Avalonia Desktop Client            │
│  (ViewModels, Views, Services)                  │
└───────────────┬─────────────────────────────────┘
                │ SignalR Hub Connection
                │
┌───────────────▼─────────────────────────────────┐
│           ASP.NET Core Server                   │
│  ┌─────────────────────────────────────────┐   │
│  │  AppHub (SignalR Hub)                   │   │
│  └──────┬──────────────────────────────────┘   │
│         │                                        │
│  ┌──────▼──────────────────────────────────┐   │
│  │  Application Layer (CQRS)               │   │
│  │  • Commands (CreateItem, UpdateItem)    │   │
│  │  • Queries (GetItems, GetItemById)      │   │
│  │  • Handlers + Mediator                  │   │
│  └──────┬──────────────────────────────────┘   │
│         │                                        │
│  ┌──────▼──────────────────────────────────┐   │
│  │  Domain Layer                           │   │
│  │  • Entities (Item)                      │   │
│  │  • Domain Events                        │   │
│  └─────────────────────────────────────────┘   │
└─────────────────┬───────────────────────────────┘
                  │
          ┌───────▼────────┐
          │  PostgreSQL    │
          └────────────────┘
```

---

## Key Architectural Patterns (1/2)

### 1. SignalR for All RPC
❌ No REST endpoints  
✅ SignalR hub methods = API

```csharp
public interface IAppHub {
    Task<ItemDto> CreateItemAsync(
        CreateItemDto dto, Guid? idempotencyKey);
}
```

### 2. Mediator Pattern
- **Commands** for mutations, **Queries** for reads
- Source generators (zero-reflection)
- Pipeline behaviors (validation, logging)

---

## Key Architectural Patterns (2/2)

### 3. Idempotency Keys
All mutating operations accept `Guid? idempotencyKey`

**Why?** Prevents duplicate operations on:
- Network failures
- Timeouts
- User double-clicks

**How?** Stored in `IdempotencyRecords` table
- Check before execution
- Return cached result if exists
- Store result after execution

---

## Authentication: OIDC Flow

**Desktop App → System Browser → Authentik**

1. Click "Login with Authentik"
2. Browser opens to Authentik
3. User authenticates
4. Redirect to `localhost:7890/callback`
5. App captures auth code
6. Exchange code for JWT token
7. Token sent via SignalR query string

---

## Authentication: Server Validation

```csharp
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => {
        options.Authority = 
            "http://webinfo.local:9000/application/o/ai-demo/";
        options.RequireHttpsMetadata = false; // Dev only
        
        options.Events = new JwtBearerEvents {
            OnMessageReceived = context => {
                // SignalR passes token via query string
                context.Token = 
                    context.Request.Query["access_token"];
            }
        };
    });
```

---

## Data Flow: Creating an Item

```
┌─────────┐   CreateItemAsync()    ┌──────────┐
│ Client  ├──────SignalR──────────►│  AppHub  │
└─────────┘                        └────┬─────┘
                                        │
                                        │ Send(Command)
                                        │
                                   ┌────▼──────────────┐
                                   │ CreateItemHandler │
                                   └────┬──────────────┘
                                        │
                                        │ Save + OutboxMessage
                                        │
                                   ┌────▼────────┐
                                   │ PostgreSQL  │
                                   └────┬────────┘
                                        │
                          ┌─────────────┴─────────────┐
                          │                           │
                    ┌─────▼────────┐         ┌───────▼──────────┐
                    │ Items Table  │         │ OutboxMessages   │
                    └──────────────┘         └───────┬──────────┘
                                                     │
                                    ┌────────────────▼────────────────┐
                                    │ OutboxProcessorService          │
                                    │ (Background Service)            │
                                    └────────┬────────────────────────┘
                                             │
                                             │ Clients.Others.OnItemCreated()
                                             │
                                      ┌──────▼──────┐
                                      │ Other Clients│
                                      └─────────────┘
```

---

## Development Workflow: AI-Assisted Development

### The Challenge
Starting with empty projects and:
- Complex authentication (OIDC)
- Real-time communication (SignalR)
- Clean Architecture setup
- Docker orchestration
- Database migrations

### The AI Agent Approach
Using GitHub Copilot in **action-agent** mode with structured tracking

---

## Agent-Driven Development (1/3)

### Phase 1: Planning
**Agent**: `planning-agent`
- Analyzed requirements
- Created `agent-planning-todo.md` with task breakdown
- Documented decisions in `agent-planning-internal.md`
- Generated file structure plan

---

## Agent-Driven Development (2/3)

### Phase 2: Execution  
**Agent**: `action-agent`
- Loaded tasks from `agent-action-todo.md`
- Executed tasks sequentially
- Updated todo checkboxes: `- [ ]` → `- [x]`
- Moved completed tasks to `agent-action-done.md`
- Discovered and added subtasks dynamically

---

## Agent-Driven Development (3/3)

### Benefits
- **Context Preservation** - Track state across sessions
- **Incremental Progress** - Visible task completion
- **Dynamic Discovery** - Add tasks as needed
- **Accountability** - Clear record of what happened

---

## Task Tracking System

### Files Structure
```
agent-planning-todo.md       # High-level planning tasks
agent-planning-internal.md   # Architectural decisions
agent-planning-done.md       # Completed planning

agent-action-todo.md         # Implementation tasks (checkboxes)
agent-action-internal.md     # Implementation context/decisions
agent-action-done.md         # Completed with timestamps
agent-action-files.md        # Files created/modified
```

### Example Task Flow
```markdown
## agent-action-todo.md
- [x] Generate and apply EF Core migrations
- [x] Fix connection string in appsettings.json
- [x] Configure Authentik OIDC provider
- [ ] Create remaining server endpoints
```

---

## Key Challenges & Solutions (1/2)

### 1. Database Connection
**Problem**: Windows → WSL → Docker networking  
**Solution**: Run server from WSL, expose port 5432

### 2. OIDC Token Validation
**Problem**: Audience mismatch  
**Solution**: Disable audience check, validate issuer only

---

## Key Challenges & Solutions (2/2)

### 3. SignalR Parameter Passing
**Problem**: CancellationToken sent as argument  
**Solution**: Named parameter `cancellationToken: ct`

### 4. URL Encoding Bug
**Problem**: Browser inserting `^&` escapes  
**Solution**: `UseShellExecute = true` (no manual escaping)

---

## Development Statistics

### Time: ~9 hours total
- Initial Setup: 2h • Auth Integration: 4h
- SignalR + CRUD: 2h • Database: 1h

### Code Generated
- 5 Projects, ~3,500 LOC
- 15+ Hub Methods, 4 DB Tables

### Agent Interactions
- ~100+ tool invocations
- Zero manual file creation
- Incremental debugging via logging

---

## Lessons Learned

### What Worked Well ✅
- **Clean Architecture** - Easy testing & changes
- **SignalR** - No REST boilerplate
- **Tracking files** - Context preserved
- **Incremental building** - Early issue detection

### What Was Challenging ⚠️
- **MessagePack serialization** - Parameter nuances
- **OIDC configuration** - URLs, audiences
- **Logging visibility** - GUI + buffering issues
- **Build artifacts** - Windows/WSL mixing

---

## Key Takeaways: AI-Assisted Dev

### 1. Structured Tracking
Prevents forgotten steps, maintains context

### 2. Incremental Validation
Build → Test → Log → Debug

### 3. Tool Visibility
Debug Console > Terminal, Explicit > Implicit

---

## Key Takeaways (continued)

### 4. Architecture Matters
Clean separation → Agent understanding

### 5. Documentation = Code
AGENTS.md, ARCHITECTURE.md as instructions

### 6. Smart Debugging
Verbose logging → Layer isolation → System browser

---

## Future Enhancements (1/2)

### Short Term
- [ ] Token refresh flow
- [ ] Rate limiting implementation
- [ ] Optimistic UI updates
- [ ] Offline queue with sync

### Medium Term
- [ ] Multi-tenant support
- [ ] Horizontal scaling (Redis backplane)

---

## Future Enhancements (2/2)

### Medium Term (continued)
- [ ] Event sourcing for audit trail
- [ ] GraphQL API alternative

### Long Term
- [ ] Mobile clients (MAUI)
- [ ] Real-time collaboration features
- [ ] AI-powered code reviews
- [ ] Automated testing pipeline

---

## Demo: Live Application

### What You'll See
1. **Authentik Login** - Browser-based OIDC flow
2. **Real-time Updates** - Multiple clients synchronized
3. **CRUD Operations** - Create, read, update, delete items
4. **Database Persistence** - PostgreSQL with EF Core
5. **Clean Separation** - Hub → Mediator → Handler → Database

### Try It Yourself
```bash
# Start infrastructure
wsl -e bash -c "cd /mnt/c/Projekt/AI-demo && sudo docker compose up -d"

# Start server (in WSL)
wsl -e bash -c "cd /mnt/c/Projekt/AI-demo && dotnet run --project src/AiDemo.Server"

# Start client (Windows)
dotnet run --project src/AvaloniaApp
```

---

## Conclusion

### What We Achieved
- ✅ **Production-ready architecture** (Clean Architecture + CQRS)
- ✅ **Modern authentication** (OIDC with Authentik)
- ✅ **Real-time communication** (SignalR)
- ✅ **Database persistence** (PostgreSQL + EF Core)
- ✅ **Fully AI-assisted** (minimal manual coding)

### The AI Agent Advantage
- **Faster initial development** (structure generated quickly)
- **Consistent patterns** (agent follows AGENTS.md guidelines)
- **Documented decisions** (tracking files capture rationale)
- **Iterative refinement** (debugging through tool calls)

---

## Thank You!

### Resources
- **Code**: [github.com/yourrepo/AI-demo](https://github.com)
- **Documentation**: `ARCHITECTURE.md`, `AGENTS.md`
- **Tracking Files**: `agent-*.md` series

### Questions?

*Built with: .NET 10 • Avalonia • SignalR • PostgreSQL • Authentik • GitHub Copilot*

---

## Appendix: Technology Deep Dive

### Mediator (martinothamar/Mediator)
```csharp
[GenerateMediator]
public sealed partial record CreateItemCommand(
    string Name,
    string Description,
    Guid UserId,
    Guid? IdempotencyKey
) : ICommand<ItemDto>;

public sealed class CreateItemHandler : ICommandHandler<CreateItemCommand, ItemDto>
{
    public async ValueTask<ItemDto> Handle(CreateItemCommand command, CancellationToken ct)
    {
        // Implementation
    }
}
```

---

## Appendix: Idempotency Pattern

**Why?** Prevents duplicate operations

```csharp
public sealed class IdempotencyBehavior<TReq, TRes> 
    : IPipelineBehavior<TReq, TRes>
{
    public async ValueTask<TRes> Handle(
        TReq request, CancellationToken ct,
        MessageHandlerDelegate<TReq, TRes> next)
    {
        if (request is IIdempotentCommand<TRes> cmd 
            && cmd.IdempotencyKey.HasValue)
        {
            // Check cache
            var cached = await _service
                .GetResultAsync<TRes>(cmd.IdempotencyKey.Value);
            if (cached != null) return cached;
        }
        
        var result = await next(request, ct);
        // Store result...
        return result;
    }
}
```

---

## Appendix: Outbox Pattern

**Problem**: Notification fails after DB commit

**Solution**: Transactional outbox

```sql
BEGIN;
  INSERT INTO Items VALUES (...);
  INSERT INTO OutboxMessages 
    VALUES ('ItemCreated', '{...}');
COMMIT;
```

Background service polls outbox → sends → marks processed

**Benefits**: Reliability • Retry • Ordering
