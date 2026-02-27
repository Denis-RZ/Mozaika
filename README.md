# Mozaika Production Demo

Full-stack mosaic generator:

- `backend-dotnet/`: ASP.NET Core Web API + EF Core + universal DB provider abstraction.
- `frontend/`: React + TypeScript UI for studio and admin workflows.
- `archive/`: archived legacy assets (including previous Python backend snapshot).
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
- `MOZAIKA__PRICING__CURRENCY` (default `RUB`)
- `MOZAIKA__PRICING__PRICEPERCHIP`
- `MOZAIKA__PRICING__PRICEPERUSEDCOLOR`
- `MOZAIKA__PRICING__COMPLEXITYTHRESHOLDCOLORS`
- `MOZAIKA__PRICING__EXTRAPRICEPERCOLORABOVETHRESHOLD`
- `MOZAIKA__PRICING__GROUTPRICEPERSQUAREMETER`
- `MOZAIKA__PRICING__SETUPPRICE`
- `MOZAIKA__PRICING__MINORDERPRICE`
- `MOZAIKA__CORSORIGINS__0` (for array items)
- `MOZAIKA_AUTH_ADMIN_PASSWORD`
- `MOZAIKA_AUTH_CUSTOMER_PASSWORD`
- `MOZAIKA_AUTH_VIEWER_PASSWORD`

Frontend optional variable:

- `VITE_API_BASE_URL` (empty by default; use same-origin/proxy)

## Auth (v1)

`backend-dotnet/appsettings.json` now stores only usernames/roles.
Passwords are taken from environment variables listed above.

For local development:

- `backend-dotnet/appsettings.Development.json` enables fallback demo passwords.
- `start-local.ps1` explicitly sets:
  - `admin / admin123`
  - `customer / customer123`
  - `viewer / viewer123`

## Planning

- Roadmap draft (RU): `docs/roadmap-ru.md`
- TZ progress tracker: `docs/tz-progress.md`

## Encoding Check

To verify there is no Russian text mojibake in UI/docs/backend source files:

```bash
node scripts/check-ru-mojibake.mjs
```

Frontend production build now runs this check automatically:

```bash
cd frontend
npm run build
```
