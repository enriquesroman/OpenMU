# Deploying OpenMU to Railway

This directory contains the deployment configuration for hosting OpenMU on [Railway](https://railway.com).

## Architecture Overview

```
[ Browser Players ]
        │
        ▼ (HTTPS / WSS)
┌────────────────────────────────────────────────────────┐
│  Railway Project                                       │
│                                                        │
│  1. Web Client & WS Proxy Service                      │
│     (Ignies/OpenMu-Client-Babylon)                     │
│        │ (internal private network)                    │
│        ▼                                               │
│  2. OpenMU Server Service (Startup/Dockerfile)         │
│     • Connect Server: 44405 (internal)                 │
│     • Game Server:    45901 (internal)                 │
│     • Admin Panel:    8080 (public HTTPS domain)       │
│        │ (internal private network)                    │
│        ▼                                               │
│  3. PostgreSQL Database (Railway Managed Postgres)     │
└────────────────────────────────────────────────────────┘
```

## Step 1: Provision Postgres

1. In your Railway project, click **New** -> **Database** -> **Add PostgreSQL**.
2. Keep the service name as `Postgres` (default).

## Step 2: Deploy OpenMU Server

1. In the same project, click **New** -> **GitHub Repo** and select your `OpenMU` fork.
2. In the service **Settings**:
   - **Root Directory**: `src`
   - **Config File Path**: `/deploy/railway/railway.json`
   - In **Networking** -> **Public Networking**, click **Generate Domain** (exposing port `8080` for the Admin Panel).
   - In **General** -> Set service name to `openmu` (ensures `openmu.railway.internal` resolves correctly).
3. In **Variables**, add:
   - `DB_HOST`: `${{Postgres.RAILWAY_PRIVATE_DOMAIN}}`
   - `DB_ADMIN_USER`: `${{Postgres.PGUSER}}`
   - `DB_ADMIN_PW`: `${{Postgres.PGPASSWORD}}`
   - `ASPNETCORE_URLS`: `http://+:8080`
   - `RESOLVE_IP`: `openmu.railway.internal`
   - `OPENMU_ADMIN_USER`: `admin`
   - `OPENMU_ADMIN_PASSWORD`: `<your-secure-password>`

Once deployed, visit your generated domain to log into the Admin Panel.

## Step 3: Connect Babylon Web Client

The [OpenMu-Client-Babylon](https://github.com/Ignies/OpenMu-Client-Babylon) WebSocket proxy can run as a 3rd service in Railway:
- Set its target connect server to `openmu.railway.internal:44405`.
- Set `ALLOW_TARGETS="openmu.railway.internal:44405,openmu.railway.internal:45901"`.
- Expose the Babylon client/proxy via Railway domain so players can connect in-browser!
