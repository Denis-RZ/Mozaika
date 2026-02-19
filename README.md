# Mozaika Production Demo

Full-stack mosaic generator:

- `backend-dotnet/`: ASP.NET Core Web API + EF Core + universal DB provider abstraction.
- `frontend/`: React + TypeScript UI for studio and admin workflows.
- `backend/`: legacy FastAPI implementation (kept for reference).
- `docker-compose.yml`: production-like deployment with PostgreSQL, .NET backend, and nginx-served frontend.

## Quick Start (Docker)

```bash
docker compose up --build
```

Open:

- App: `http://localhost:8080`
- Backend health (proxied): `http://localhost:8080/health`

## Local Dev

1. Start backend (.NET):

```bash
cd backend-dotnet
dotnet restore
dotnet run --urls http://127.0.0.1:8000
```

2. Start frontend:

```bash
cd frontend
npm install
npm run dev
```

Frontend runs at `http://localhost:5173` and proxies API requests to backend.

## Local One-Command (Windows PowerShell)

Start:

```powershell
.\start-local.ps1
```

Stop:

```powershell
.\stop-local.ps1
```

## Environment

Backend (.NET) variables:

- `MOZAIKA__DATABASE__PROVIDER` (`sqlite` | `postgres` | `sqlserver`)
- `MOZAIKA__DATABASE__CONNECTIONSTRING`
- `MOZAIKA__MAXUPLOADMB`
- `MOZAIKA__CORSORIGINS__0` (for array items)

Frontend optional variable:

- `VITE_API_BASE_URL` (empty by default; use same-origin/proxy)
