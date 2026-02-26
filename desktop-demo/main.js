const { app, BrowserWindow, Menu, dialog } = require("electron");
const http = require("http");
const fs = require("fs");
const path = require("path");
const { spawn } = require("child_process");

const BACKEND_PORT = 18765;
const FRONTEND_PORT = 18766;
const BACKEND_URL = `http://127.0.0.1:${BACKEND_PORT}`;
const FRONTEND_URL = `http://127.0.0.1:${FRONTEND_PORT}`;
const FRONTEND_ORIGIN = FRONTEND_URL;
const BACKEND_START_TIMEOUT_MS = 60000;

/** @type {BrowserWindow | null} */
let mainWindow = null;
/** @type {import("child_process").ChildProcessWithoutNullStreams | null} */
let backendProcess = null;
/** @type {http.Server | null} */
let frontendServer = null;

function resolveRuntimeRoot() {
  if (app.isPackaged) {
    return path.join(process.resourcesPath, "runtime");
  }
  return path.join(__dirname, "runtime");
}

function mimeTypeByExtension(filePath) {
  const ext = path.extname(filePath).toLowerCase();
  switch (ext) {
    case ".html":
      return "text/html; charset=utf-8";
    case ".js":
      return "application/javascript; charset=utf-8";
    case ".css":
      return "text/css; charset=utf-8";
    case ".json":
      return "application/json; charset=utf-8";
    case ".svg":
      return "image/svg+xml";
    case ".png":
      return "image/png";
    case ".jpg":
    case ".jpeg":
      return "image/jpeg";
    case ".webp":
      return "image/webp";
    case ".ico":
      return "image/x-icon";
    default:
      return "application/octet-stream";
  }
}

function safeFilePathFromUrl(frontendDir, requestUrl) {
  const basePath = requestUrl.split("?")[0].split("#")[0];
  const decoded = decodeURIComponent(basePath);
  const normalized = path.normalize(decoded).replace(/^(\.\.(\/|\\|$))+/, "");
  const targetPath = path.join(frontendDir, normalized);
  const resolved = path.resolve(targetPath);
  const frontendResolved = path.resolve(frontendDir);
  if (!resolved.startsWith(frontendResolved)) {
    return null;
  }
  return resolved;
}

function createFrontendServer(frontendDir) {
  const indexFile = path.join(frontendDir, "index.html");
  if (!fs.existsSync(indexFile)) {
    throw new Error(
      `Frontend assets are missing: ${indexFile}. Run: npm run prepare:assets in desktop-demo`,
    );
  }

  return http.createServer((req, res) => {
    try {
      const reqUrl = req.url || "/";
      const target = safeFilePathFromUrl(frontendDir, reqUrl === "/" ? "/index.html" : reqUrl);
      if (!target) {
        res.writeHead(403, { "Content-Type": "text/plain; charset=utf-8" });
        res.end("Forbidden");
        return;
      }

      if (fs.existsSync(target) && fs.statSync(target).isFile()) {
        res.writeHead(200, { "Content-Type": mimeTypeByExtension(target) });
        fs.createReadStream(target).pipe(res);
        return;
      }

      // SPA fallback.
      res.writeHead(200, { "Content-Type": "text/html; charset=utf-8" });
      fs.createReadStream(indexFile).pipe(res);
    } catch (error) {
      res.writeHead(500, { "Content-Type": "text/plain; charset=utf-8" });
      res.end(`Static server error: ${error instanceof Error ? error.message : "unknown"}`);
    }
  });
}

function waitForBackendReady(timeoutMs) {
  const startedAt = Date.now();

  return new Promise((resolve, reject) => {
    const probe = () => {
      const request = http.get(`${BACKEND_URL}/health`, (response) => {
        if (response.statusCode && response.statusCode >= 200 && response.statusCode < 500) {
          resolve();
          return;
        }
        response.resume();
        scheduleRetry();
      });

      request.on("error", scheduleRetry);
      request.setTimeout(2500, () => request.destroy());
    };

    const scheduleRetry = () => {
      if (Date.now() - startedAt >= timeoutMs) {
        reject(new Error("Backend startup timed out."));
        return;
      }
      setTimeout(probe, 500);
    };

    probe();
  });
}

function killProcessTree(proc) {
  if (!proc || proc.killed) {
    return;
  }

  try {
    if (process.platform === "win32") {
      const killer = spawn("taskkill", ["/pid", String(proc.pid), "/t", "/f"], {
        windowsHide: true,
        stdio: "ignore",
      });
      killer.on("error", () => {
        try {
          proc.kill("SIGKILL");
        } catch {
          // Ignore.
        }
      });
    } else {
      proc.kill("SIGTERM");
    }
  } catch {
    // Ignore shutdown errors.
  }
}

function resolveBackendExecutable(backendDir) {
  const winExe = path.join(backendDir, "Mozaika.Api.exe");
  if (fs.existsSync(winExe)) {
    return winExe;
  }

  const unixExe = path.join(backendDir, "Mozaika.Api");
  if (fs.existsSync(unixExe)) {
    return unixExe;
  }

  throw new Error(
    `Backend executable not found in ${backendDir}. Run: npm run prepare:assets in desktop-demo`,
  );
}

function startBackend(backendDir) {
  const executable = resolveBackendExecutable(backendDir);
  const storageDir = path.join(app.getPath("userData"), "data");
  const storagePath = path.join(storageDir, "mozaika.storage.json");
  fs.mkdirSync(storageDir, { recursive: true });

  const env = {
    ...process.env,
    ASPNETCORE_URLS: BACKEND_URL,
    MOZAIKA__DATABASE__PROVIDER: "json",
    MOZAIKA__DATABASE__CONNECTIONSTRING: `Data Source=${storagePath}`,
    MOZAIKA__DATABASE__ECHO: "false",
    MOZAIKA__CORSORIGINS__0: FRONTEND_ORIGIN,
    MOZAIKA__MAXUPLOADMB: "25",
  };

  const logsDir = path.join(app.getPath("userData"), "logs");
  fs.mkdirSync(logsDir, { recursive: true });
  const stdoutPath = path.join(logsDir, "backend.stdout.log");
  const stderrPath = path.join(logsDir, "backend.stderr.log");
  const stdoutStream = fs.createWriteStream(stdoutPath, { flags: "a" });
  const stderrStream = fs.createWriteStream(stderrPath, { flags: "a" });

  const child = spawn(executable, [], {
    cwd: backendDir,
    env,
    windowsHide: true,
    stdio: ["ignore", "pipe", "pipe"],
  });

  child.stdout.pipe(stdoutStream);
  child.stderr.pipe(stderrStream);

  child.once("exit", () => {
    stdoutStream.end();
    stderrStream.end();
  });

  return child;
}

function stopFrontendServer() {
  if (!frontendServer) {
    return;
  }
  frontendServer.close();
  frontendServer = null;
}

async function bootstrap() {
  const runtimeRoot = resolveRuntimeRoot();
  const backendDir = path.join(runtimeRoot, "backend");
  const frontendDir = path.join(runtimeRoot, "frontend");

  if (!fs.existsSync(backendDir) || !fs.existsSync(frontendDir)) {
    throw new Error(
      `Runtime assets not found in ${runtimeRoot}. Build assets first: npm run prepare:assets`,
    );
  }

  backendProcess = startBackend(backendDir);
  await waitForBackendReady(BACKEND_START_TIMEOUT_MS);

  frontendServer = createFrontendServer(frontendDir);
  await new Promise((resolve, reject) => {
    frontendServer.once("error", reject);
    frontendServer.listen(FRONTEND_PORT, "127.0.0.1", () => resolve());
  });
}

function createMainWindow() {
  mainWindow = new BrowserWindow({
    width: 1600,
    height: 980,
    minWidth: 1200,
    minHeight: 760,
    autoHideMenuBar: true,
    title: "Mozaika Demo",
    show: false,
    webPreferences: {
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: true,
      devTools: false,
    },
  });

  mainWindow.removeMenu();
  mainWindow.loadURL(FRONTEND_URL);

  mainWindow.once("ready-to-show", () => {
    if (mainWindow) {
      mainWindow.show();
    }
  });

  mainWindow.on("closed", () => {
    mainWindow = null;
  });
}

async function shutdown() {
  stopFrontendServer();
  if (backendProcess) {
    killProcessTree(backendProcess);
    backendProcess = null;
  }
}

app.on("window-all-closed", () => {
  if (process.platform !== "darwin") {
    app.quit();
  }
});

app.on("before-quit", () => {
  shutdown();
});

app.whenReady().then(async () => {
  Menu.setApplicationMenu(null);

  try {
    await bootstrap();
    createMainWindow();
  } catch (error) {
    const detail = error instanceof Error ? error.message : String(error);
    await dialog.showMessageBox({
      type: "error",
      title: "Mozaika Demo startup failed",
      message: "Не удалось запустить desktop-демо.",
      detail,
    });
    await shutdown();
    app.exit(1);
  }
});
