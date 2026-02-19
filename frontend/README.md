# Mozaika Frontend

React + TypeScript production UI for:

- Mosaic generation (image upload, field/cell/gap, grout color, include/exclude colors, max colors, offsets).
- Palette admin (single add, bulk import, edit, deactivate).
- System settings admin (default dimensions, cell and gap values).
- Grout color admin (add, edit, deactivate).

## Local Run

```bash
cd frontend
npm install
npm run dev
```

By default, Vite proxies `/api` and `/health` to `http://127.0.0.1:8000`.

## Production Build

```bash
cd frontend
npm run build
npm run preview
```

