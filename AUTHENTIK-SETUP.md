# Authentik OIDC Setup Guide

## Local Development Setup

### 1. Start Authentik Services

```bash
# In WSL
cd /mnt/c/Projekt/AI-demo
sudo docker compose up -d
```

Wait for all services to be healthy (takes ~30-60 seconds):
```bash
sudo docker compose ps
```

### 2. Access Authentik Admin UI

Open browser: **http://localhost:9000/if/flow/initial-setup/**

**Initial Setup:**
- Email: `admin@localhost`
- Password: `admin`

**Note**: Change these credentials in production!

### 3. Create OIDC Application

#### Step 1: Create Provider

1. Navigate to **Applications** → **Providers**
2. Click **Create** → **OAuth2/OpenID Provider**
3. Configure:
   - **Name**: `AiDemo Desktop Client`
   - **Authorization flow**: `default-provider-authorization-implicit-consent`
   - **Client type**: `Public`
   - **Client ID**: `ai-demo-desktop` (copy this)
   - **Redirect URIs**: 
     ```
     http://localhost:7890/callback
     ```
   - **Signing Key**: `authentik Self-signed Certificate` (auto-generated)
   - **Subject mode**: `Based on the User's UUID`
   - **Include claims in id_token**: ✅ Enabled
4. Click **Create**

#### Step 2: Create Application

1. Navigate to **Applications** → **Applications**
2. Click **Create**
3. Configure:
   - **Name**: `AiDemo Desktop`
   - **Slug**: `ai-demo`
   - **Provider**: `AiDemo Desktop Client` (select the provider created above)
   - **Launch URL**: `http://localhost:5000`
4. Click **Create**

#### Step 3: Configure Audience

1. Go back to **Providers** → **AiDemo Desktop Client** → **Edit**
2. Scroll to **Advanced protocol settings**
3. Set **Token validity**: `hours=24` (or desired duration)
4. Click **Update**

### 4. Update Application Configuration

Update `src/AiDemo.Server/appsettings.Development.json`:

```json
{
  "Oidc": {
    "Authority": "http://localhost:9000/application/o/ai-demo",
    "ClientId": "ai-demo-desktop",
    "Audience": "ai-demo-server"
  }
}
```

**Note**: The Authority URL format is `http://localhost:9000/application/o/{slug}` where `slug` is the application slug.

### 5. Create Test User (Optional)

1. Navigate to **Directory** → **Users**
2. Click **Create**
3. Configure:
   - **Username**: `testuser`
   - **Name**: `Test User`
   - **Email**: `testuser@localhost`
   - **Password**: `testpass123`
4. Click **Create**

### 6. Verify Setup

Test the OIDC discovery endpoint:

```bash
curl http://localhost:9000/application/o/ai-demo/.well-known/openid-configuration
```

Should return JSON with `issuer`, `authorization_endpoint`, `token_endpoint`, etc.

## Client Application Configuration

The client (`AvaloniaApp`) is already configured to use Authentik via the `AuthService` class.

When you run the client:
1. Click "Login" button
2. Browser opens to Authentik login page
3. Enter credentials (admin@localhost / admin or testuser / testpass123)
4. Grants consent
5. Redirects back with authorization code
6. Client exchanges code for JWT token
7. Client connects to SignalR hub with JWT

## Production Configuration

For production, replace Authentik with your organization's OIDC provider:

- **Azure AD**: `https://login.microsoftonline.com/{tenant}/v2.0`
- **Auth0**: `https://{domain}.auth0.com`
- **Keycloak**: `https://{domain}/realms/{realm}`

Update `appsettings.json` accordingly:

```json
{
  "Oidc": {
    "Authority": "https://your-oidc-provider.com",
    "ClientId": "your-client-id",
    "Audience": "your-api-audience"
  }
}
```

## Troubleshooting

### Authentik not starting
```bash
# Check logs
sudo docker compose logs authentik-server
sudo docker compose logs authentik-postgres

# Restart services
sudo docker compose restart
```

### "Invalid client" error
- Verify Client ID matches in both Authentik and appsettings
- Ensure redirect URIs include the exact callback URL

### Token validation fails
- Check `Audience` claim in JWT matches server configuration
- Verify `Issuer` matches Authentik Authority URL
- Ensure token hasn't expired

### Cannot access http://localhost:9000
- Verify Authentik containers are running: `sudo docker compose ps`
- Check port 9000 isn't already in use: `sudo netstat -tulpn | grep 9000`

## Security Notes

⚠️ **Development Only**: Current configuration uses:
- Weak SECRET_KEY
- Default admin credentials
- HTTP (not HTTPS)
- Public client (no client secret)

For production:
- Generate strong SECRET_KEY
- Use strong admin credentials
- Enable HTTPS with valid certificates
- Consider confidential client with client secret
- Enable audit logging
- Configure backup strategy
