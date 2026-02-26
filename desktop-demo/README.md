# Mozaika Desktop Demo (Electron)

Desktop wrapper for customer demos:

- no browser address bar
- local frontend (static server inside Electron)
- local backend (self-contained executable)
- local data storage in JSON file

## Requirements

- Windows x64
- Node.js (for build stage)
- .NET SDK (for preparing backend assets)

## Build demo assets

From repository root:

```powershell
cd desktop-demo
npm install
npm run prepare:assets
```

This command:

1. Builds frontend with `VITE_API_BASE_URL=http://127.0.0.1:18765`
2. Publishes backend as self-contained `win-x64` executable
3. Puts runtime files into `desktop-demo/runtime`

## Run desktop app

```powershell
cd desktop-demo
npm start
```

## Build portable EXE

```powershell
cd desktop-demo
npm run dist
```

Output folder:

- `desktop-demo/dist/`

## Data storage path in desktop mode

- `%APPDATA%/mozaika-desktop-demo/data/mozaika.storage.json` (Electron userData path)

Logs path:

- `%APPDATA%/mozaika-desktop-demo/logs`
