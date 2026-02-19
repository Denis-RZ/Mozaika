# Mozaika Backend

FastAPI backend for mosaic generation and admin management.

## Features

- Universal DB provider abstraction (`app/db/provider.py`) with backend registry.
- Palette API:
  - `GET /api/colors`
  - `POST /api/colors`
  - `POST /api/colors/bulk`
  - `PATCH /api/colors/{id}`
  - `DELETE /api/colors/{id}` (soft deactivate)
- Admin API:
  - `GET /api/admin/settings`
  - `PUT /api/admin/settings`
  - `GET /api/admin/grout-colors`
  - `POST /api/admin/grout-colors`
  - `PATCH /api/admin/grout-colors/{id}`
  - `DELETE /api/admin/grout-colors/{id}` (soft deactivate)
- Bootstrap API:
  - `GET /api/bootstrap`
- Mosaic API:
  - `POST /api/mosaic/generate`
  - Supports field size, cell size, gap, grout color, include/exclude palette ids, max colors, and mosaic offset.
- Startup seeding for default system settings and default grout color options.

## Database Provider

Set `MOZAIKA_DATABASE_URL` to switch databases without changing service/repository code.

Examples:

- SQLite: `sqlite:///./mozaika.db`
- PostgreSQL: `postgresql+psycopg://user:pass@host:5432/mozaika`
- MySQL: `mysql+pymysql://user:pass@host:3306/mozaika`

## Local Run

```bash
cd backend
python -m venv .venv
.venv\Scripts\activate
pip install -e .[dev,postgres]
uvicorn app.main:app --reload
```

Health check:

```bash
curl http://127.0.0.1:8000/health
```

## Tests

```bash
cd backend
python -m pytest -q
```

