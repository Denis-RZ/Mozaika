# Mozaika Production Demo

Full-stack mosaic generator:

- `backend/`: FastAPI + SQLAlchemy + universal DB provider abstraction.
- `frontend/`: React + TypeScript UI for studio and admin workflows.
- `docker-compose.yml`: production-like deployment with PostgreSQL, backend, and nginx-served frontend.

## Quick Start (Docker)

```bash
docker compose up --build
```

Open:

- App: `http://localhost:8080`
- Backend health (proxied): `http://localhost:8080/health`

## Local Dev

1. Start backend:

```bash
cd backend
python -m venv .venv
.venv\Scripts\activate
pip install -e .[dev,postgres]
uvicorn app.main:app --reload
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

Backend variables (see `backend/.env.example`):

- `MOZAIKA_DATABASE_URL`
- `MOZAIKA_DATABASE_ECHO`
- `MOZAIKA_MAX_UPLOAD_MB`

Frontend optional variable:

- `VITE_API_BASE_URL` (empty by default; use same-origin/proxy)
