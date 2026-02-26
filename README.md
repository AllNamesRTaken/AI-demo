# AI-Demo: Real-Time Application with SignalR & Authentik OIDC

A demonstration of Clean Architecture with real-time bidirectional communication using SignalR, Authentik OIDC authentication, and PostgreSQL. Built with AI assistance using structured task tracking.

## 🚀 Quick Start

### Prerequisites

- **Windows** with WSL 2 installed
- **.NET 10 SDK** ([Download](https://dotnet.microsoft.com/download/dotnet/10.0))
- **Docker Desktop** or Docker in WSL
- **Git**

### Step 1: Clone and Setup

```powershell
# Clone the repository
git clone <your-repo-url> AI-demo
cd AI-demo
```

### Step 2: Start Infrastructure (PostgreSQL + Authentik)

**Important:** Run Docker commands from WSL for proper networking:

```powershell
# Enter WSL
wsl

# Navigate to project directory
cd /mnt/c/Projekt/AI-demo

# Start PostgreSQL and Authentik
sudo docker compose up -d

# Verify containers are running
sudo docker compose ps

# Check logs if needed
sudo docker compose logs -f
```

Expected containers:
- `aidemo-postgres` - PostgreSQL 16 database
- `aidemo-authentik-server` - Authentik OIDC provider
- `aidemo-authentik-worker` - Authentik background worker
- `aidemo-authentik-postgresql` - Authentik's internal database
- `aidemo-redis` - Authentik's cache

### Step 3: Configure Authentik OIDC (First Time Only)

1. Open Authentik admin panel: http://webinfo.local:9000
2. Login with credentials from `docker-compose.yml` (default: `admin@localhost` / see `.env` file)
3. **Create Application:**
   - Navigate to **Applications** → **Create**
   - Name: `AI Demo Desktop`
   - Slug: `ai-demo-desktop`
   - Provider: Create new **OAuth2/OpenID Provider**
4. **Configure Provider:**
   - Client Type: `Public`
   - Redirect URIs: `http://localhost:7890/callback`
   - Signing Key: Choose default
   - Save
5. **Note the Client ID** from the provider details

### Step 4: Update Configuration

Edit `src/AiDemo.Server/appsettings.Development.json`:

```json
{
  "Oidc": {
    "Authority": "http://webinfo.local:9000/application/o/ai-demo/",
    "ClientId": "<your-client-id-from-authentik>"
  }
}
```

Edit `src/AvaloniaApp/appsettings.json` (if exists) or configure in code with the same values.

### Step 5: Run Database Migrations (First Time Only)

**From WSL** (to access Docker PostgreSQL):

```bash
# Still in WSL from Step 2
cd /mnt/c/Projekt/AI-demo

# Run migrations
dotnet ef database update --project src/AiDemo.Infrastructure --startup-project src/AiDemo.Server

# Verify tables created
sudo docker exec -it aidemo-postgres psql -U postgres -d aidemo -c "\dt"
```

Expected tables:
- `Items`
- `OutboxMessages`
- `IdempotencyRecords`
- `__EFMigrationsHistory`

### Step 6: Start the Server

**From WSL** (must run in WSL to connect to Docker PostgreSQL):

```bash
# In WSL
cd /mnt/c/Projekt/AI-demo

# Run the server
dotnet run --project src/AiDemo.Server
```

Server should start on `http://localhost:5000` and display:
```
[HH:mm:ss INF] Now listening on: http://localhost:5000
[HH:mm:ss INF] Application started.
```

Verify health endpoint: http://localhost:5000/health

### Step 7: Start the Client

**From Windows PowerShell** (new terminal):

```powershell
# Navigate to project
cd C:\Projekt\AI-demo

# Run the Avalonia client
dotnet run --project src/AvaloniaApp
```

The desktop application window will open.

### Step 8: Login and Test

1. Click **"Login with Authentik"** button
2. Browser opens to Authentik login page
3. Login with Authentik credentials
4. Browser redirects → app captures token → client connects to SignalR
5. You're authenticated and can now:
   - Create new items
   - View items list (real-time)
   - Update/delete items
   - See real-time updates from other clients

## 📋 Architecture Overview

```
┌─────────────────┐         SignalR          ┌─────────────────┐
│ Avalonia Client │ ←────────────────────────→ │  ASP.NET Server │
│   (Windows)     │    JWT Token (OIDC)      │     (WSL)       │
└─────────────────┘                           └─────────────────┘
                                                      │
                                    ┌─────────────────┼─────────────────┐
                                    │                 │                 │
                              ┌──────────┐    ┌──────────────┐   ┌──────────┐
                              │PostgreSQL│    │   Authentik  │   │  Redis   │
                              │ (Docker) │    │   (Docker)   │   │ (Docker) │
                              └──────────┘    └──────────────┘   └──────────┘
```

**Key Technologies:**
- **Frontend:** Avalonia 11.3 (Cross-platform .NET desktop)
- **Backend:** ASP.NET Core 10 with SignalR
- **Auth:** Authentik OIDC (local), Cloud OIDC (production)
- **Database:** PostgreSQL 16 with Entity Framework Core
- **Architecture:** Clean Architecture + CQRS (Mediator pattern)
- **Communication:** SignalR WebSocket with MessagePack

## 🛠️ Project Structure

```
src/
├── AiDemo.Domain/          # Core business entities (no dependencies)
├── AiDemo.Application/     # Business logic (Commands/Queries/Handlers)
├── AiDemo.Infrastructure/  # EF Core, external services, background jobs
├── AiDemo.Contracts/       # Shared DTOs and Hub interfaces
├── AiDemo.Server/          # ASP.NET Core host + SignalR hub
└── AvaloniaApp/            # Desktop client UI

docker/                     # Docker Compose for infrastructure
├── docker-compose.yml
└── .env                    # Environment variables

ARCHITECTURE.md             # Detailed architecture documentation
AGENTS.md                   # AI agent development guidelines
presentation.md             # MARP slide deck about the project
```

## 🔧 Common Tasks

### Restart Infrastructure

```bash
# In WSL
cd /mnt/c/Projekt/AI-demo
sudo docker compose restart
```

### View Logs

```bash
# Server logs (in WSL where server runs)
# ... output shows in terminal ...

# PostgreSQL logs
sudo docker compose logs -f postgres

# Authentik logs
sudo docker compose logs -f authentik-server
```

### Reset Database

```bash
# In WSL
cd /mnt/c/Projekt/AI-demo

# Drop database
sudo docker exec -it aidemo-postgres psql -U postgres -c "DROP DATABASE aidemo;"
sudo docker exec -it aidemo-postgres psql -U postgres -c "CREATE DATABASE aidemo;"

# Reapply migrations
dotnet ef database update --project src/AiDemo.Infrastructure --startup-project src/AiDemo.Server
```

### Create New Migration

```bash
# In WSL
cd /mnt/c/Projekt/AI-demo

dotnet ef migrations add <MigrationName> \
  --project src/AiDemo.Infrastructure \
  --startup-project src/AiDemo.Server \
  --output-dir Persistence/Migrations
```

### Build Solution

```powershell
# From Windows PowerShell or WSL
dotnet build AI-demo.sln
```

### Run Tests

```powershell
dotnet test
```

## ⚠️ Troubleshooting

### Issue: "Connection refused" to PostgreSQL

**Cause:** Server not running in WSL  
**Solution:** The server MUST run from WSL to access Docker containers:

```bash
wsl
cd /mnt/c/Projekt/AI-demo
dotnet run --project src/AiDemo.Server
```

### Issue: "Invalid client identifier" during login

**Cause:** Authentik configuration mismatch  
**Solutions:**
1. Verify Client ID matches in Authentik provider and `appsettings.Development.json`
2. Check redirect URI is exactly `http://localhost:7890/callback`
3. Ensure client type is set to "Public" in Authentik

### Issue: Client shows "401 Unauthorized"

**Cause:** Authentication token issue  
**Solutions:**
1. Check server logs for JWT validation errors
2. Verify `Oidc:Authority` URL matches Authentik application URL exactly
3. Try logging out and back in to get fresh token
4. Check Authentik is running: http://webinfo.local:9000

### Issue: Docker containers won't start

**Cause:** Port conflicts or insufficient resources  
**Solutions:**
```bash
# Check what's using ports
sudo docker compose ps
netstat -ano | findstr "5432"  # PostgreSQL
netstat -ano | findstr "9000"  # Authentik

# Stop conflicting services or change ports in docker-compose.yml
```

### Issue: Avalonia client doesn't show logs

**Cause:** GUI apps don't show console output without debugger  
**Solution:** Attach VS Code debugger or check Debug Console in VS Code

### Issue: SignalR connection fails

**Checklist:**
1. ✅ Server running and showing "Now listening on: http://localhost:5000"
2. ✅ Health endpoint returns 200: http://localhost:5000/health
3. ✅ Client successfully authenticated (has token)
4. ✅ No firewall blocking localhost:5000

## 📚 Documentation

- **[ARCHITECTURE.md](./ARCHITECTURE.md)** - Comprehensive architecture documentation
  - System design and patterns
  - ADRs (Architecture Decision Records)
  - Code examples and conventions
  - Deployment guide

- **[AGENTS.md](./AGENTS.md)** - AI agent development workflow
  - Instructions for AI coding assistants
  - Project conventions and rules
  - Common patterns and anti-patterns

- **[AUTHENTIK-SETUP.md](./AUTHENTIK-SETUP.md)** - Authentik configuration details
  - Step-by-step OIDC setup
  - User management
  - Production considerations

- **[presentation.md](./presentation.md)** - MARP presentation
  - Project overview slides
  - Architecture diagrams
  - Development journey

## 🔐 Security Notes

### Development
- Authentik runs without HTTPS (localhost only)
- JWT tokens in query strings (standard for SignalR)
- Default credentials should be changed

### Production
- **Enable HTTPS** for all services
- Use managed OIDC provider (Azure AD, Auth0, Okta)
- Implement proper secrets management
- Enable HSTS and other security headers
- Review [Security Considerations](./ARCHITECTURE.md#security-considerations) in ARCHITECTURE.md

## 🤝 Contributing

This project was built with AI assistance using structured task tracking:

1. **Planning Agent** - Created task breakdown and architectural decisions
2. **Action Agent** - Implemented features incrementally with tracking
3. **Tracking Files** - Maintained context across sessions

See [AGENTS.md](./AGENTS.md) for the AI-assisted workflow used in this project.

## 📝 License

[Your License Here]

## 🙋 Support

- **Issues:** Check the troubleshooting section above
- **Architecture Questions:** See [ARCHITECTURE.md](./ARCHITECTURE.md)
- **Authentik Setup:** See [AUTHENTIK-SETUP.md](./AUTHENTIK-SETUP.md)

---

**Built with:** .NET 10 • Avalonia • SignalR • PostgreSQL • Authentik • AI Assistance
