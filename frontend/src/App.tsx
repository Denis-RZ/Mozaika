import { useCallback, useEffect, useMemo, useRef, useState } from "react";

import {
  activateProjectGeneration,
  configureDatabase,
  createColor,
  createColorsBulk,
  createProjectOrder,
  createProject,
  createProjectShare,
  createGroutColor,
  deactivateColor,
  deactivateGroutColor,
  fetchAuthSession,
  fetchBootstrap,
  fetchDatabaseConfig,
  listProjectOrders,
  listProjectShares,
  login,
  logout,
  fetchProject,
  fetchProjectGeneration,
  fetchProjects,
  exportMosaic,
  generateMosaic,
  patchColor,
  patchGroutColor,
  replaceMosaicColor,
  resolveProjectShare,
  revokeProjectShare,
  saveProjectGeneration,
  setApiAuthToken,
  testDatabaseConnection,
  updateProjectOrderStatus,
  updateProject,
  updateSettings,
} from "./api";
import type {
  AuthSessionRead,
  BootstrapResponse,
  ColorCreate,
  ColorRead,
  DatabaseConfigUpdate,
  GroutColorCreate,
  GroutColorRead,
  MosaicGenerateResponse,
  ProjectGenerationRead,
  ProjectListItem,
  ProjectOrderCreateRequest,
  ProjectOrderRead,
  ProjectOrderStatus,
  ProjectRead,
  ProjectSaveGenerationRequest,
  ProjectShareRead,
  StudioFormState,
  TabKey,
  UserRole,
} from "./types";

const DEFAULT_MAX_COLORS = 0;

const tabs: Array<{ key: TabKey; label: string }> = [
  { key: "studio", label: "Генерация" },
  { key: "projects", label: "Проекты" },
  { key: "palette", label: "Палитра" },
  { key: "settings", label: "Настройки" },
];

type StudioViewMode = "basic" | "advanced";

const ORDER_STATUS_OPTIONS: Array<{ value: ProjectOrderStatus; label: string }> = [
  { value: "submitted", label: "Новый" },
  { value: "in_review", label: "На согласовании" },
  { value: "approved", label: "Подтвержден" },
  { value: "rejected", label: "Отклонен" },
  { value: "cancelled", label: "Отменен" },
];

function FieldHint({ text }: { text: string }) {
  return (
    <span className="field-hint" tabIndex={0} aria-label={text}>
      ?
      <span className="field-hint-popup">{text}</span>
    </span>
  );
}

function LabelTitle({ text, hint }: { text: string; hint: string }) {
  return (
    <span className="label-title">
      {text}
      <FieldHint text={hint} />
    </span>
  );
}

function normalizeHexIfPossible(value: string): string {
  const raw = value.trim();
  if (/^#?[0-9A-Fa-f]{6}$/.test(raw)) {
    return `#${raw.replace("#", "").toUpperCase()}`;
  }
  return value;
}

function pickerHexValue(value: string): string {
  const raw = value.trim();
  if (/^#[0-9A-Fa-f]{6}$/.test(raw)) {
    return raw;
  }
  if (/^[0-9A-Fa-f]{6}$/.test(raw)) {
    return `#${raw}`;
  }
  return "#000000";
}

const COLOR_NAME_SUGGESTIONS_RU = [
  "Белый",
  "Черный",
  "Серый",
  "Красный",
  "Оранжевый",
  "Желтый",
  "Зеленый",
  "Бирюзовый",
  "Синий",
  "Фиолетовый",
  "Розовый",
  "Коричневый",
];

const RAL_SUGGESTIONS = [
  "RAL 9016",
  "RAL 9005",
  "RAL 7040",
  "RAL 3020",
  "RAL 2004",
  "RAL 1023",
  "RAL 6018",
  "RAL 5018",
  "RAL 5015",
  "RAL 4008",
  "RAL 4010",
  "RAL 8004",
];

function hexToRgb(hexValue: string): { r: number; g: number; b: number } | null {
  const hex = normalizeHexIfPossible(hexValue);
  if (!/^#[0-9A-F]{6}$/.test(hex)) {
    return null;
  }
  return {
    r: Number.parseInt(hex.slice(1, 3), 16),
    g: Number.parseInt(hex.slice(3, 5), 16),
    b: Number.parseInt(hex.slice(5, 7), 16),
  };
}

function rgbToHsv(r: number, g: number, b: number): { h: number; s: number; v: number } {
  const rn = r / 255;
  const gn = g / 255;
  const bn = b / 255;

  const max = Math.max(rn, gn, bn);
  const min = Math.min(rn, gn, bn);
  const delta = max - min;

  let h = 0;
  if (delta !== 0) {
    if (max === rn) {
      h = ((gn - bn) / delta) % 6;
    } else if (max === gn) {
      h = (bn - rn) / delta + 2;
    } else {
      h = (rn - gn) / delta + 4;
    }
    h *= 60;
    if (h < 0) {
      h += 360;
    }
  }

  const s = max === 0 ? 0 : delta / max;
  const v = max;
  return { h, s, v };
}

function suggestColorMeta(hexValue: string): { name: string; ralCode: string } | null {
  const rgb = hexToRgb(hexValue);
  if (!rgb) {
    return null;
  }
  const hsv = rgbToHsv(rgb.r, rgb.g, rgb.b);

  let baseName = "Серый";
  if (hsv.s < 0.1) {
    if (hsv.v > 0.92) {
      baseName = "Белый";
    } else if (hsv.v < 0.12) {
      baseName = "Черный";
    } else {
      baseName = "Серый";
    }
  } else if (hsv.h < 15 || hsv.h >= 345) {
    baseName = "Красный";
  } else if (hsv.h < 40) {
    baseName = "Оранжевый";
  } else if (hsv.h < 66) {
    baseName = "Желтый";
  } else if (hsv.h < 170) {
    baseName = "Зеленый";
  } else if (hsv.h < 200) {
    baseName = "Бирюзовый";
  } else if (hsv.h < 255) {
    baseName = "Синий";
  } else if (hsv.h < 290) {
    baseName = "Фиолетовый";
  } else {
    baseName = "Розовый";
  }

  const prefix =
    baseName !== "Белый" &&
      baseName !== "Черный" &&
      baseName !== "Серый" &&
      hsv.v > 0.82 &&
      hsv.s < 0.55
      ? "Светлый "
      : baseName !== "Черный" && hsv.v < 0.35
        ? "Темный "
        : "";

  const ralMap: Record<string, string> = {
    "Белый": "RAL 9016",
    "Черный": "RAL 9005",
    "Серый": "RAL 7040",
    "Красный": "RAL 3020",
    "Оранжевый": "RAL 2004",
    "Желтый": "RAL 1023",
    "Зеленый": "RAL 6018",
    "Бирюзовый": "RAL 5018",
    "Синий": "RAL 5015",
    "Фиолетовый": "RAL 4008",
    "Розовый": "RAL 4010",
  };

  return {
    name: `${prefix}${baseName}`.trim(),
    ralCode: ralMap[baseName] ?? "RAL 0000",
  };
}

function computeMosaicSize(fieldWidth: number, fieldHeight: number, cellSize: number, gap: number) {
  const pitch = cellSize + gap;
  if (pitch <= 0) {
    return { rows: 0, columns: 0, mosaicWidth: 0, mosaicHeight: 0 };
  }
  const columns = Math.ceil((fieldWidth + gap) / pitch);
  const rows = Math.ceil((fieldHeight + gap) / pitch);
  if (rows <= 0 || columns <= 0) {
    return { rows: 0, columns: 0, mosaicWidth: 0, mosaicHeight: 0 };
  }
  const mosaicWidth = columns * cellSize + (columns - 1) * gap;
  const mosaicHeight = rows * cellSize + (rows - 1) * gap;
  return { rows, columns, mosaicWidth, mosaicHeight };
}

function computeOffsetGuides(fieldWidth: number, fieldHeight: number, cellSize: number, gap: number) {
  const size = computeMosaicSize(fieldWidth, fieldHeight, cellSize, gap);
  const overflowX = Math.max(0, size.mosaicWidth - fieldWidth);
  const overflowY = Math.max(0, size.mosaicHeight - fieldHeight);
  const pitchMm = Math.max(0.1, cellSize + gap);

  return {
    overflowX,
    overflowY,
    left: 0,
    right: -overflowX,
    top: 0,
    bottom: -overflowY,
    centerX: -overflowX / 2,
    centerY: -overflowY / 2,
    pitchMm,
  };
}

function clampNumber(value: number, min: number, max: number): number {
  return Math.min(max, Math.max(min, value));
}

function parseNumberValue(raw: string): number {
  const normalized = raw
    .trim()
    .replace(/[\s\u00A0]+/g, "")
    .replace(/,/g, ".");
  return Number(normalized);
}

function parseBulkPaletteRows(raw: string): ColorCreate[] {
  const rows = raw
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter((line) => line.length > 0);
  if (rows.length === 0) {
    throw new Error("Список для пакетного импорта пуст.");
  }

  return rows.map((row, index) => {
    const parts = row.split(";").map((part) => part.trim());
    if (parts.length < 3) {
      throw new Error(`Строка ${index + 1}: ожидается формат 'название;RAL;#HEX'.`);
    }
    return {
      name: parts[0],
      ral_code: parts[1],
      rgb_hex: parts[2],
      is_active: true,
    };
  });
}

interface DbConnectionForm {
  dataSource: string;
  host: string;
  port: string;
  database: string;
  username: string;
  password: string;
  sslMode: string;
  encrypt: boolean;
  trustServerCertificate: boolean;
  extra: string;
}

function defaultDbConnectionForm(provider: string): DbConnectionForm {
  switch (provider) {
    case "sqlite":
      return {
        dataSource: "",
        host: "",
        port: "",
        database: "",
        username: "",
        password: "",
        sslMode: "Require",
        encrypt: true,
        trustServerCertificate: true,
        extra: "",
      };
    case "json":
      return {
        dataSource: "../data/mozaika.storage.json",
        host: "",
        port: "",
        database: "",
        username: "",
        password: "",
        sslMode: "Require",
        encrypt: true,
        trustServerCertificate: true,
        extra: "",
      };
    case "postgres":
      return {
        dataSource: "",
        host: "localhost",
        port: "5432",
        database: "mozaika",
        username: "postgres",
        password: "postgres",
        sslMode: "Require",
        encrypt: true,
        trustServerCertificate: true,
        extra: "",
      };
    case "mysql":
      return {
        dataSource: "",
        host: "localhost",
        port: "3306",
        database: "mozaika",
        username: "root",
        password: "",
        sslMode: "Required",
        encrypt: true,
        trustServerCertificate: true,
        extra: "",
      };
    case "sqlserver":
      return {
        dataSource: "",
        host: "localhost",
        port: "1433",
        database: "mozaika",
        username: "sa",
        password: "",
        sslMode: "Require",
        encrypt: true,
        trustServerCertificate: true,
        extra: "",
      };
    default:
      return {
        dataSource: "",
        host: "",
        port: "",
        database: "",
        username: "",
        password: "",
        sslMode: "Require",
        encrypt: true,
        trustServerCertificate: true,
        extra: "",
      };
  }
}

function parseConnectionStringPairs(connectionString: string): Record<string, string> {
  const pairs: Record<string, string> = {};
  for (const part of connectionString.split(";")) {
    const chunk = part.trim();
    if (chunk.length === 0) {
      continue;
    }
    const index = chunk.indexOf("=");
    if (index <= 0) {
      continue;
    }
    const key = chunk.slice(0, index).trim().toLowerCase();
    const value = chunk.slice(index + 1).trim();
    pairs[key] = value;
  }
  return pairs;
}

function pickPairValue(pairs: Record<string, string>, keys: string[]): string {
  for (const key of keys) {
    const value = pairs[key.toLowerCase()];
    if (value !== undefined) {
      return value;
    }
  }
  return "";
}

function parseBoolValue(value: string, fallback: boolean): boolean {
  if (value.length === 0) {
    return fallback;
  }
  const normalized = value.trim().toLowerCase();
  if (normalized === "true" || normalized === "1" || normalized === "yes") {
    return true;
  }
  if (normalized === "false" || normalized === "0" || normalized === "no") {
    return false;
  }
  return fallback;
}

function extractExtraPairs(connectionString: string, knownKeys: string[]): string {
  const known = new Set(knownKeys.map((key) => key.toLowerCase()));
  const extras: string[] = [];

  for (const part of connectionString.split(";")) {
    const chunk = part.trim();
    if (chunk.length === 0) {
      continue;
    }
    const index = chunk.indexOf("=");
    if (index <= 0) {
      continue;
    }
    const key = chunk.slice(0, index).trim().toLowerCase();
    if (!known.has(key)) {
      extras.push(chunk);
    }
  }

  return extras.join(";");
}

function parseDbConnectionForm(provider: string, connectionString: string): DbConnectionForm {
  const defaults = defaultDbConnectionForm(provider);
  const pairs = parseConnectionStringPairs(connectionString);

  if (provider === "sqlite" || provider === "json") {
    return {
      ...defaults,
      dataSource:
        pickPairValue(pairs, ["Data Source", "Filename", "Path", "Json", "JsonFile"]) ||
        connectionString.trim() ||
        defaults.dataSource,
      extra: extractExtraPairs(connectionString, ["Data Source", "Filename", "Path", "Json", "JsonFile"]),
    };
  }

  const next: DbConnectionForm = {
    ...defaults,
    host: pickPairValue(pairs, ["Host", "Server", "Data Source"]) || defaults.host,
    port: pickPairValue(pairs, ["Port"]) || defaults.port,
    database: pickPairValue(pairs, ["Database", "Initial Catalog"]) || defaults.database,
    username: pickPairValue(pairs, ["Username", "User", "User Id", "Uid"]) || defaults.username,
    password: pickPairValue(pairs, ["Password", "Pwd"]) || defaults.password,
    sslMode: pickPairValue(pairs, ["SSL Mode", "SslMode"]) || defaults.sslMode,
    encrypt: parseBoolValue(pickPairValue(pairs, ["Encrypt"]), defaults.encrypt),
    trustServerCertificate: parseBoolValue(
      pickPairValue(pairs, ["TrustServerCertificate", "Trust Server Certificate"]),
      defaults.trustServerCertificate,
    ),
    extra: "",
  };

  if (provider === "sqlserver") {
    const hostRaw = pickPairValue(pairs, ["Server", "Data Source"]);
    if (hostRaw.startsWith("tcp:")) {
      const withoutPrefix = hostRaw.slice(4);
      const splitIndex = withoutPrefix.lastIndexOf(",");
      if (splitIndex > 0) {
        next.host = withoutPrefix.slice(0, splitIndex);
        next.port = withoutPrefix.slice(splitIndex + 1);
      } else {
        next.host = withoutPrefix;
      }
    }
  }

  if (provider === "postgres") {
    next.extra = extractExtraPairs(connectionString, [
      "Host",
      "Server",
      "Data Source",
      "Port",
      "Database",
      "Initial Catalog",
      "Username",
      "User",
      "User Id",
      "Uid",
      "Password",
      "Pwd",
      "SSL Mode",
      "SslMode",
      "TrustServerCertificate",
      "Trust Server Certificate",
    ]);
  } else if (provider === "mysql") {
    next.extra = extractExtraPairs(connectionString, [
      "Server",
      "Host",
      "Port",
      "Database",
      "User",
      "Username",
      "User Id",
      "Uid",
      "Password",
      "Pwd",
      "SslMode",
      "SSL Mode",
    ]);
  } else if (provider === "sqlserver") {
    next.extra = extractExtraPairs(connectionString, [
      "Server",
      "Data Source",
      "Port",
      "Database",
      "Initial Catalog",
      "User",
      "Username",
      "User Id",
      "Uid",
      "Password",
      "Pwd",
      "Encrypt",
      "TrustServerCertificate",
      "Trust Server Certificate",
    ]);
  }

  return next;
}

function withExtra(base: string, extra: string): string {
  const trimmed = extra.trim();
  if (trimmed.length === 0) {
    return base;
  }
  const normalized = trimmed.endsWith(";") ? trimmed : `${trimmed};`;
  return `${base}${normalized}`;
}

function buildConnectionString(provider: string, form: DbConnectionForm): string {
  if (provider === "sqlite" || provider === "json") {
    return withExtra(`Data Source=${form.dataSource.trim()};`, form.extra);
  }

  if (provider === "postgres") {
    return withExtra(
      `Host=${form.host.trim()};Port=${form.port.trim()};Database=${form.database.trim()};Username=${form.username.trim()};Password=${form.password};SSL Mode=${form.sslMode.trim()};Trust Server Certificate=${form.trustServerCertificate ? "true" : "false"};`,
      form.extra,
    );
  }

  if (provider === "mysql") {
    return withExtra(
      `Server=${form.host.trim()};Port=${form.port.trim()};Database=${form.database.trim()};User=${form.username.trim()};Password=${form.password};SslMode=${form.sslMode.trim()};`,
      form.extra,
    );
  }

  if (provider === "sqlserver") {
    return withExtra(
      `Server=tcp:${form.host.trim()},${form.port.trim()};Database=${form.database.trim()};User Id=${form.username.trim()};Password=${form.password};Encrypt=${form.encrypt ? "True" : "False"};TrustServerCertificate=${form.trustServerCertificate ? "True" : "False"};`,
      form.extra,
    );
  }

  return "";
}

function providerConnectionHint(provider: string): string {
  switch (provider) {
    case "sqlite":
      return "SQLite example: Data Source=/path/to/mozaika.db";
    case "json":
      return "JSON example: Data Source=../data/mozaika.storage.json";
    case "postgres":
      return "PostgreSQL example: Host=localhost;Port=5432;Database=mozaika;Username=postgres;Password=postgres";
    case "sqlserver":
      return "SQL Server example: Server=localhost;Database=mozaika;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True";
    case "mysql":
      return "MySQL example: Server=localhost;Port=3306;Database=mozaika;User=root;Password=pass;";
    default:
      return "Specify a connection string for the selected provider.";
  }
}
function formatDateTime(value: string | null): string {
  if (!value) {
    return "-";
  }
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    return value;
  }
  return parsed.toLocaleString();
}

function formatMoney(value: number, currency: string): string {
  const normalizedCurrency = currency.trim().toUpperCase();
  try {
    return new Intl.NumberFormat("ru-RU", {
      style: "currency",
      currency: normalizedCurrency,
      maximumFractionDigits: 2,
    }).format(value);
  } catch {
    return `${value.toFixed(2)} ${normalizedCurrency}`;
  }
}

function roleLabel(role: UserRole | null): string {
  if (role === "admin") {
    return "Админ";
  }
  if (role === "customer") {
    return "Заказчик";
  }
  if (role === "viewer") {
    return "Просмотр";
  }
  return "Гость";
}

function orderStatusLabel(status: ProjectOrderStatus): string {
  const found = ORDER_STATUS_OPTIONS.find((item) => item.value === status);
  return found?.label ?? status;
}

function fileToBase64(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onerror = () => reject(new Error("Не удалось прочитать исходное изображение."));
    reader.onload = () => {
      if (typeof reader.result !== "string") {
        reject(new Error("Не удалось прочитать исходное изображение."));
        return;
      }
      const commaIndex = reader.result.indexOf(",");
      if (commaIndex < 0) {
        resolve(reader.result);
        return;
      }
      resolve(reader.result.slice(commaIndex + 1));
    };
    reader.readAsDataURL(file);
  });
}

function base64ToArrayBuffer(base64: string): ArrayBuffer {
  const normalized = base64.replace(/\s+/g, "");
  const binary = window.atob(normalized);
  const bytes = new Uint8Array(binary.length);
  for (let index = 0; index < binary.length; index += 1) {
    bytes[index] = binary.charCodeAt(index);
  }
  return bytes.buffer;
}

function normalizeMosaicResult(response: MosaicGenerateResponse): MosaicGenerateResponse {
  const fallbackTotalChips = response.rows * response.columns;
  const totalChips =
    typeof response.total_chips === "number" && Number.isFinite(response.total_chips)
      ? response.total_chips
      : fallbackTotalChips;

  const fallbackPrice = {
    currency: "RUB",
    total_chips: totalChips,
    area_sq_m: Number(((response.field_width_mm * response.field_height_mm) / 1_000_000).toFixed(4)),
    setup_price: 0,
    chips_price: 0,
    colors_price: 0,
    complexity_price: 0,
    grout_price: 0,
    subtotal_price: 0,
    min_order_price: 0,
    min_order_applied: false,
    total_price: 0,
  };

  const price = response.price ? { ...fallbackPrice, ...response.price } : fallbackPrice;
  return {
    ...response,
    total_chips: totalChips,
    price: {
      ...price,
      total_chips:
        typeof price.total_chips === "number" && Number.isFinite(price.total_chips)
          ? price.total_chips
          : totalChips,
    },
  };
}

export default function App() {
  const [activeTab, setActiveTab] = useState<TabKey>("studio");
  const [bootstrap, setBootstrap] = useState<BootstrapResponse | null>(null);
  const [loadingBootstrap, setLoadingBootstrap] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [authToken, setAuthToken] = useState<string | null>(() => {
    const stored = localStorage.getItem("mozaika_auth_token");
    if (!stored || stored.trim().length === 0) {
      return null;
    }
    // Check if stored token has an associated expiry (saved alongside the token)
    const expiry = localStorage.getItem("mozaika_auth_expires_at");
    if (expiry) {
      try {
        if (new Date(expiry) <= new Date()) {
          // Token already expired — clear storage proactively
          localStorage.removeItem("mozaika_auth_token");
          localStorage.removeItem("mozaika_auth_expires_at");
          return null;
        }
      } catch {
        // Invalid date string — ignore
      }
    }
    return stored.trim();
  });
  const [authSession, setAuthSession] = useState<AuthSessionRead>({
    is_authenticated: false,
    expires_at: null,
    requires_password_change: false,
    user: null,
  });
  const [authBusy, setAuthBusy] = useState(false);
  const [authInitialized, setAuthInitialized] = useState(false);
  const [loginUsername, setLoginUsername] = useState("customer");
  const [loginPassword, setLoginPassword] = useState("customer123");

  const [imageFile, setImageFile] = useState<File | null>(null);
  const [imagePreviewUrl, setImagePreviewUrl] = useState<string | null>(null);
  const [mosaicResult, setMosaicResult] = useState<MosaicGenerateResponse | null>(null);
  const [previewZoom, setPreviewZoom] = useState(1);
  const [previewPan, setPreviewPan] = useState({ x: 0, y: 0 });
  const [positionStepMm, setPositionStepMm] = useState(12);
  const [previewPanStepPx, setPreviewPanStepPx] = useState(24);
  const [previewZoomStep, setPreviewZoomStep] = useState(0.2);
  const [studioViewMode, setStudioViewMode] = useState<StudioViewMode>("basic");
  const [previewDragging, setPreviewDragging] = useState(false);
  const previewDragRef = useRef<{
    pointerId: number;
    startX: number;
    startY: number;
    originX: number;
    originY: number;
  } | null>(null);
  const [replaceFromColorId, setReplaceFromColorId] = useState<number | null>(null);
  const [replaceToColorId, setReplaceToColorId] = useState<number | null>(null);
  const [exportDpi, setExportDpi] = useState(200);
  const [exportMirrorHorizontal, setExportMirrorHorizontal] = useState(false);
  const [exportIncludeLegend, setExportIncludeLegend] = useState(true);
  const [moduleChipColumns, setModuleChipColumns] = useState(32);
  const [moduleChipRows, setModuleChipRows] = useState(32);
  const [moduleStartNumber, setModuleStartNumber] = useState(1);
  const [exportIncludeColorNumbers, setExportIncludeColorNumbers] = useState(true);
  const [projects, setProjects] = useState<ProjectListItem[]>([]);
  const [loadingProjects, setLoadingProjects] = useState(false);
  const [projectsPage, setProjectsPage] = useState(1);
  const [projectsTotal, setProjectsTotal] = useState(0);
  const [projectsLimit] = useState(20);
  const [selectedProjectId, setSelectedProjectId] = useState<number | null>(null);
  const [selectedProject, setSelectedProject] = useState<ProjectRead | null>(null);
  const [projectNameDraft, setProjectNameDraft] = useState("");
  const [projectDescriptionDraft, setProjectDescriptionDraft] = useState("");
  const [generationNameDraft, setGenerationNameDraft] = useState("");
  const [generationNoteDraft, setGenerationNoteDraft] = useState("");
  const [projectShares, setProjectShares] = useState<ProjectShareRead[]>([]);
  const [projectOrders, setProjectOrders] = useState<ProjectOrderRead[]>([]);
  const [shareExpiresInDays, setShareExpiresInDays] = useState(14);
  const [shareGenerationId, setShareGenerationId] = useState<number | null>(null);
  const [orderForm, setOrderForm] = useState<ProjectOrderCreateRequest>({
    generation_id: null,
    customer_name: "",
    customer_email: "",
    customer_phone: "",
    comment: "",
  });
  const [orderStatusDraftByOrder, setOrderStatusDraftByOrder] = useState<Record<number, ProjectOrderStatus>>(
    {},
  );
  const [orderStatusCommentByOrder, setOrderStatusCommentByOrder] = useState<Record<number, string>>(
    {},
  );

  const [studioForm, setStudioForm] = useState<StudioFormState>({
    fieldWidthMm: 1200,
    fieldHeightMm: 800,
    cellSizeMm: 10,
    gapMm: 2,
    maxColors: DEFAULT_MAX_COLORS,
    groutColorId: null,
    offsetXmm: 0,
    offsetYmm: 0,
  });
  const [gridDraft, setGridDraft] = useState({ columns: 100, rows: 66 });
  const [includeColorIds, setIncludeColorIds] = useState<number[]>([]);
  const [excludeColorIds, setExcludeColorIds] = useState<number[]>([]);
  const [liveRegenerateEnabled, setLiveRegenerateEnabled] = useState(true);
  const [liveRegeneratePending, setLiveRegeneratePending] = useState(false);
  const [liveRegenerateRunning, setLiveRegenerateRunning] = useState(false);
  const [liveRegenerateError, setLiveRegenerateError] = useState<string | null>(null);
  const [hasGeneratedSnapshot, setHasGeneratedSnapshot] = useState(false);
  const [showGenerateFirstPopup, setShowGenerateFirstPopup] = useState(false);
  const [muteGenerateFirstPopupUntilGeneration, setMuteGenerateFirstPopupUntilGeneration] = useState(false);
  const [hideGenerateFirstPopupThisSession, setHideGenerateFirstPopupThisSession] = useState(false);
  const liveRegenerateTimerRef = useRef<number | null>(null);
  const liveRegenerateAbortRef = useRef<AbortController | null>(null);
  const liveRegenerateRequestIdRef = useRef(0);

  const [newColorForm, setNewColorForm] = useState<ColorCreate>({
    name: "",
    ral_code: "",
    rgb_hex: "#C1121F",
    is_active: true,
  });
  const [newColorTouched, setNewColorTouched] = useState({ name: false, ral: false });
  const [bulkPaletteText, setBulkPaletteText] = useState("");
  const [editingColorId, setEditingColorId] = useState<number | null>(null);
  const [editingColorForm, setEditingColorForm] = useState<ColorCreate | null>(null);
  const [editingColorTouched, setEditingColorTouched] = useState({ name: false, ral: false });

  const [settingsDraft, setSettingsDraft] = useState({
    default_field_width_mm: 1200,
    default_field_height_mm: 800,
    default_cell_size_mm: 10,
    default_gap_mm: 2,
  });
  const [dbDraft, setDbDraft] = useState<DatabaseConfigUpdate>({
    provider: "sqlite",
    connection_string: "Data Source=../data/mozaika.local.db",
    echo: false,
    create_schema: true,
    seed_defaults: true,
  });
  const [dbConnectionForm, setDbConnectionForm] = useState<DbConnectionForm>(
    defaultDbConnectionForm("sqlite"),
  );
  const [supportedDbProviders, setSupportedDbProviders] = useState<string[]>([
    "sqlite",
    "postgres",
    "sqlserver",
    "mysql",
  ]);

  const [newGroutForm, setNewGroutForm] = useState<GroutColorCreate>({
    name: "",
    rgb_hex: "#F2EFEA",
    is_active: true,
  });
  const [newGroutNameTouched, setNewGroutNameTouched] = useState(false);
  const [editingGroutId, setEditingGroutId] = useState<number | null>(null);
  const [editingGroutForm, setEditingGroutForm] = useState<GroutColorCreate | null>(null);
  const [editingGroutNameTouched, setEditingGroutNameTouched] = useState(false);

  const activePalette = useMemo(
    () => (bootstrap?.colors ?? []).filter((color) => color.is_active),
    [bootstrap?.colors],
  );
  const activeGroutColors = useMemo(
    () => (bootstrap?.grout_colors ?? []).filter((color) => color.is_active),
    [bootstrap?.grout_colors],
  );
  const currentRole = authSession.user?.role ?? null;
  const isAuthenticated = authSession.is_authenticated && authSession.user !== null;
  const isAdmin = currentRole === "admin";
  const canEditProjects = currentRole === "admin" || currentRole === "customer";
  const canUseStudio = canEditProjects;
  const isStudioAdvancedMode = studioViewMode === "advanced";
  const visibleTabs = useMemo(() => {
    return tabs.filter((item) => {
      if (item.key === "palette" || item.key === "settings") {
        return isAdmin;
      }
      return true;
    });
  }, [isAdmin]);

  const gridEstimate = useMemo(
    () =>
      computeMosaicSize(
        studioForm.fieldWidthMm,
        studioForm.fieldHeightMm,
        studioForm.cellSizeMm,
        studioForm.gapMm,
      ),
    [studioForm.fieldWidthMm, studioForm.fieldHeightMm, studioForm.cellSizeMm, studioForm.gapMm],
  );
  const offsetGuides = useMemo(
    () =>
      computeOffsetGuides(
        studioForm.fieldWidthMm,
        studioForm.fieldHeightMm,
        studioForm.cellSizeMm,
        studioForm.gapMm,
      ),
    [studioForm.fieldWidthMm, studioForm.fieldHeightMm, studioForm.cellSizeMm, studioForm.gapMm],
  );
  const moduleEstimate = useMemo(() => {
    if (!mosaicResult) {
      return null;
    }

    const columns = Math.max(1, Math.floor(moduleChipColumns));
    const rows = Math.max(1, Math.floor(moduleChipRows));
    const modulesX = Math.ceil(mosaicResult.columns / columns);
    const modulesY = Math.ceil(mosaicResult.rows / rows);
    const modulesTotal = modulesX * modulesY;
    const pagesTotal = 1 + modulesTotal + (exportIncludeLegend ? 1 : 0);
    return {
      modulesX,
      modulesY,
      modulesTotal,
      pagesTotal,
      chipColumns: columns,
      chipRows: rows,
    };
  }, [mosaicResult, moduleChipColumns, moduleChipRows, exportIncludeLegend]);

  const composedConnectionString = useMemo(
    () => buildConnectionString(dbDraft.provider, dbConnectionForm),
    [dbDraft.provider, dbConnectionForm],
  );
  const previewSource = mosaicResult
    ? `data:image/png;base64,${mosaicResult.preview_png_base64}`
    : imagePreviewUrl;
  const previewAlt = mosaicResult
    ? "Mosaic preview"
    : "Source image preview";
  const fallbackSourceImageFile = useMemo(() => {
    if (!selectedProject || selectedProject.source_image_base64.trim().length === 0) {
      return null;
    }
    const sourceMime =
      selectedProject.source_image_mime_type.trim().length > 0
        ? selectedProject.source_image_mime_type
        : "image/png";
    try {
      const buffer = base64ToArrayBuffer(selectedProject.source_image_base64);
      return new File([buffer], "project-source-image", { type: sourceMime });
    } catch {
      return null;
    }
  }, [
    selectedProject?.id,
    selectedProject?.source_image_base64,
    selectedProject?.source_image_mime_type,
  ]);
  const generationSourceImage = imageFile ?? fallbackSourceImageFile;
  const hasGenerationSourceImage = generationSourceImage !== null;
  const maybeShowGenerateFirstPopup = useCallback(() => {
    if (hideGenerateFirstPopupThisSession || muteGenerateFirstPopupUntilGeneration) {
      return;
    }
    if (!canUseStudio || hasGeneratedSnapshot || !hasGenerationSourceImage) {
      return;
    }
    setShowGenerateFirstPopup(true);
  }, [
    canUseStudio,
    hasGeneratedSnapshot,
    hasGenerationSourceImage,
    hideGenerateFirstPopupThisSession,
    muteGenerateFirstPopupUntilGeneration,
  ]);
  const dismissGenerateFirstPopup = useCallback(() => {
    setShowGenerateFirstPopup(false);
    setMuteGenerateFirstPopupUntilGeneration(true);
  }, []);

  const cancelLiveRegeneration = useCallback(() => {
    if (liveRegenerateTimerRef.current !== null) {
      window.clearTimeout(liveRegenerateTimerRef.current);
      liveRegenerateTimerRef.current = null;
    }
    if (liveRegenerateAbortRef.current) {
      liveRegenerateAbortRef.current.abort();
      liveRegenerateAbortRef.current = null;
    }
    setLiveRegeneratePending(false);
    setLiveRegenerateRunning(false);
  }, []);

  const refreshBootstrap = useCallback(async () => {
    setLoadingBootstrap(true);
    setError(null);
    try {
      const payload = await fetchBootstrap();
      setBootstrap(payload);
      setSettingsDraft({
        default_field_width_mm: payload.settings.default_field_width_mm,
        default_field_height_mm: payload.settings.default_field_height_mm,
        default_cell_size_mm: payload.settings.default_cell_size_mm,
        default_gap_mm: payload.settings.default_gap_mm,
      });

      if (isAdmin) {
        const dbConfig = await fetchDatabaseConfig();
        setDbDraft((current) => ({
          ...current,
          provider: dbConfig.provider,
          connection_string: dbConfig.connection_string,
          echo: dbConfig.echo,
        }));
        setDbConnectionForm(parseDbConnectionForm(dbConfig.provider, dbConfig.connection_string));
        if (dbConfig.supported_providers.length > 0) {
          setSupportedDbProviders(dbConfig.supported_providers);
        }
      }

      const firstActiveGrout = payload.grout_colors.find((color) => color.is_active) ?? null;
      const activeColorCount = Math.max(
        1,
        payload.colors.filter((color) => color.is_active).length,
      );
      const defaultMax = DEFAULT_MAX_COLORS > 0 ? Math.min(DEFAULT_MAX_COLORS, activeColorCount) : activeColorCount;
      setStudioForm((current) => ({
        ...current,
        maxColors:
          current.maxColors > 0
            ? Math.max(1, Math.min(current.maxColors, activeColorCount))
            : defaultMax,
        groutColorId: current.groutColorId ?? firstActiveGrout?.id ?? null,
      }));
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : "Не удалось загрузить данные.");
    } finally {
      setLoadingBootstrap(false);
    }
  }, [isAdmin]);

  const refreshProjects = useCallback(
    async (page?: number) => {
      if (!isAuthenticated) {
        setProjects([]);
        setProjectsTotal(0);
        setSelectedProjectId(null);
        setSelectedProject(null);
        setProjectShares([]);
        setProjectOrders([]);
        return;
      }

      const targetPage = page ?? projectsPage;
      setLoadingProjects(true);
      try {
        const payload = await fetchProjects(targetPage, projectsLimit);
        setProjects(payload.items);
        setProjectsTotal(payload.total);
        setProjectsPage(payload.page);
        setSelectedProjectId((current) => {
          if (current !== null && payload.items.some((item) => item.id === current)) {
            return current;
          }
          return payload.items[0]?.id ?? null;
        });
      } catch (requestError) {
        setError(
          requestError instanceof Error
            ? requestError.message
            : "Не удалось загрузить список проектов.",
        );
      } finally {
        setLoadingProjects(false);
      }
    },
    [isAuthenticated, projectsPage, projectsLimit],
  );

  const refreshProjectWorkflow = useCallback(
    async (projectId: number) => {
      if (!isAuthenticated) {
        setProjectShares([]);
        setProjectOrders([]);
        return;
      }

      const [shares, orders] = await Promise.all([
        listProjectShares(projectId),
        listProjectOrders(projectId),
      ]);
      setProjectShares(shares);
      setProjectOrders(orders);
      setOrderStatusDraftByOrder(() => {
        const next: Record<number, ProjectOrderStatus> = {};
        for (const order of orders) {
          next[order.id] = order.status;
        }
        return next;
      });
      setOrderStatusCommentByOrder(() => {
        const next: Record<number, string> = {};
        for (const order of orders) {
          next[order.id] = order.status_comment ?? "";
        }
        return next;
      });
    },
    [isAuthenticated],
  );

  const refreshAuthState = useCallback(async () => {
    try {
      const session = await fetchAuthSession();
      setAuthSession(session);
      if (!session.is_authenticated) {
        setAuthToken(null);
      }
    } catch {
      setAuthSession({
        is_authenticated: false,
        expires_at: null,
        requires_password_change: false,
        user: null,
      });
      setAuthToken(null);
    } finally {
      setAuthInitialized(true);
    }
  }, []);

  useEffect(() => {
    setApiAuthToken(authToken);
    if (authToken) {
      localStorage.setItem("mozaika_auth_token", authToken);
    } else {
      localStorage.removeItem("mozaika_auth_token");
    }
    void refreshAuthState();
  }, [authToken, refreshAuthState]);

  useEffect(() => {
    void refreshBootstrap();
  }, [refreshBootstrap]);

  useEffect(() => {
    void refreshProjects();
  }, [refreshProjects, isAuthenticated]);

  useEffect(() => {
    if (isAdmin) {
      return;
    }

    if (activeTab === "palette" || activeTab === "settings") {
      setActiveTab("studio");
    }
  }, [activeTab, isAdmin]);

  useEffect(() => {
    return () => {
      cancelLiveRegeneration();
    };
  }, [cancelLiveRegeneration]);

  useEffect(() => {
    const token = new URLSearchParams(window.location.search).get("share");
    if (!token) {
      return;
    }

    let cancelled = false;
    setBusy(true);
    setError(null);
    setNotice(null);
    void resolveProjectShare(token)
      .then((payload) => {
        if (cancelled) {
          return;
        }

        const project: ProjectRead = {
          id: payload.project_id,
          name: payload.project_name,
          description: payload.project_description,
          owner_username: null,
          source_image_mime_type: payload.source_image_mime_type,
          source_image_base64: payload.source_image_base64,
          created_at: new Date().toISOString(),
          updated_at: new Date().toISOString(),
          generations_count: 1,
          active_generation_id: payload.generation.id,
          last_generation_at: payload.generation.created_at,
          active_generation: payload.generation,
          generations: [
            {
              id: payload.generation.id,
              version: payload.generation.version,
              name: payload.generation.name,
              note: payload.generation.note,
              created_at: payload.generation.created_at,
            },
          ],
        };

        setSelectedProject(project);
        setSelectedProjectId(project.id);
        setProjectNameDraft(project.name);
        setProjectDescriptionDraft(project.description);
        applyProjectGeneration(payload.generation, project);
        setActiveTab("studio");
        setNotice(`Открыта shared-ссылка проекта: ${project.name}.`);
      })
      .catch((requestError) => {
        if (cancelled) {
          return;
        }

        setError(
          requestError instanceof Error
            ? requestError.message
            : "Не удалось открыть shared-ссылку.",
        );
      })
      .finally(() => {
        if (!cancelled) {
          setBusy(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    if (activePalette.length === 0) {
      return;
    }
    const activeIds = new Set(activePalette.map((color) => color.id));
    setIncludeColorIds((items) => items.filter((id) => activeIds.has(id)));
    setExcludeColorIds((items) => items.filter((id) => activeIds.has(id)));
    const defaultMax =
      DEFAULT_MAX_COLORS > 0 ? Math.min(DEFAULT_MAX_COLORS, activePalette.length) : activePalette.length;
    setStudioForm((current) => ({
      ...current,
      maxColors:
        current.maxColors > 0
          ? Math.max(1, Math.min(current.maxColors, activePalette.length))
          : defaultMax,
    }));
  }, [activePalette]);

  useEffect(() => {
    if (!liveRegenerateEnabled || !canUseStudio || !hasGeneratedSnapshot || !hasGenerationSourceImage) {
      setLiveRegeneratePending(false);
      setLiveRegenerateRunning(false);
      return;
    }

    if (liveRegenerateTimerRef.current !== null) {
      window.clearTimeout(liveRegenerateTimerRef.current);
      liveRegenerateTimerRef.current = null;
    }

    setLiveRegenerateError(null);
    setLiveRegeneratePending(true);

    liveRegenerateTimerRef.current = window.setTimeout(() => {
      liveRegenerateTimerRef.current = null;

      if (!generationSourceImage) {
        setLiveRegeneratePending(false);
        return;
      }

      if (liveRegenerateAbortRef.current) {
        liveRegenerateAbortRef.current.abort();
      }

      const controller = new AbortController();
      liveRegenerateAbortRef.current = controller;
      const requestId = ++liveRegenerateRequestIdRef.current;

      setLiveRegeneratePending(false);
      setLiveRegenerateRunning(true);

      void generateMosaic(
        generationSourceImage,
        studioForm,
        includeColorIds,
        excludeColorIds,
        controller.signal,
      )
        .then((response) => {
          if (requestId !== liveRegenerateRequestIdRef.current) {
            return;
          }
          setMosaicResult(normalizeMosaicResult(response));
          setError(null);
        })
        .catch((requestError) => {
          if (controller.signal.aborted || requestId !== liveRegenerateRequestIdRef.current) {
            return;
          }
          setLiveRegenerateError(requestError instanceof Error ? requestError.message : "Ошибка автопересчета.");
        })
        .finally(() => {
          if (requestId !== liveRegenerateRequestIdRef.current) {
            return;
          }
          if (liveRegenerateAbortRef.current === controller) {
            liveRegenerateAbortRef.current = null;
          }
          setLiveRegenerateRunning(false);
          setLiveRegeneratePending(false);
        });
    }, 450);

    return () => {
      if (liveRegenerateTimerRef.current !== null) {
        window.clearTimeout(liveRegenerateTimerRef.current);
        liveRegenerateTimerRef.current = null;
      }
    };
  }, [
    canUseStudio,
    excludeColorIds,
    generationSourceImage,
    hasGeneratedSnapshot,
    hasGenerationSourceImage,
    includeColorIds,
    liveRegenerateEnabled,
    studioForm,
  ]);

  useEffect(() => {
    if (!mosaicResult) {
      setReplaceFromColorId(null);
      setReplaceToColorId(null);
      return;
    }

    const usedColorIds = mosaicResult.used_colors.map((item) => item.id);
    const activeColorIds = activePalette.map((item) => item.id);

    setReplaceFromColorId((current) => {
      if (current !== null && usedColorIds.includes(current)) {
        return current;
      }
      return usedColorIds[0] ?? null;
    });

    setReplaceToColorId((current) => {
      const fallbackFrom = usedColorIds[0] ?? null;
      if (
        current !== null &&
        activeColorIds.includes(current) &&
        (fallbackFrom === null || current !== fallbackFrom)
      ) {
        return current;
      }

      if (fallbackFrom !== null) {
        const alternative = activeColorIds.find((id) => id !== fallbackFrom);
        if (alternative !== undefined) {
          return alternative;
        }
      }

      return activeColorIds[0] ?? null;
    });
  }, [mosaicResult, activePalette]);

  useEffect(() => {
    setGridDraft({
      columns: Math.max(1, gridEstimate.columns),
      rows: Math.max(1, gridEstimate.rows),
    });
  }, [gridEstimate.columns, gridEstimate.rows]);

  useEffect(() => {
    return () => {
      if (imagePreviewUrl) {
        URL.revokeObjectURL(imagePreviewUrl);
      }
    };
  }, [imagePreviewUrl]);

  useEffect(() => {
    if (previewSource) {
      return;
    }

    previewDragRef.current = null;
    setPreviewDragging(false);
    setPreviewPan({ x: 0, y: 0 });
    setPreviewZoom(1);
  }, [previewSource]);

  useEffect(() => {
    setDbDraft((current) => ({
      ...current,
      connection_string: composedConnectionString,
    }));
  }, [composedConnectionString]);

  useEffect(() => {
    if (!selectedProject) {
      setProjectShares([]);
      setProjectOrders([]);
      return;
    }

    setShareGenerationId(selectedProject.active_generation_id ?? null);
    setOrderForm((current) => ({
      ...current,
      generation_id: selectedProject.active_generation_id ?? null,
    }));
  }, [selectedProject]);

  const setStudioField = <K extends keyof StudioFormState>(field: K, value: StudioFormState[K]) => {
    setStudioForm((current) => {
      if (typeof value === "number" && !Number.isFinite(value)) {
        return current;
      }
      return { ...current, [field]: value };
    });
  };

  const setStudioFieldFromUser = <K extends keyof StudioFormState>(
    field: K,
    value: StudioFormState[K],
  ) => {
    setStudioField(field, value);
    maybeShowGenerateFirstPopup();
  };

  const nudgeMosaicOffset = useCallback((deltaXmm: number, deltaYmm: number) => {
    setStudioForm((current) => ({
      ...current,
      offsetXmm: Number((current.offsetXmm + deltaXmm).toFixed(3)),
      offsetYmm: Number((current.offsetYmm + deltaYmm).toFixed(3)),
    }));
    maybeShowGenerateFirstPopup();
  }, [maybeShowGenerateFirstPopup]);

  const alignMosaicOffset = useCallback(
    (anchor: "top-left" | "top-right" | "bottom-left" | "bottom-right" | "center") => {
      setStudioForm((current) => {
        const guides = computeOffsetGuides(
          current.fieldWidthMm,
          current.fieldHeightMm,
          current.cellSizeMm,
          current.gapMm,
        );

        if (anchor === "top-left") {
          return { ...current, offsetXmm: Number(guides.left.toFixed(3)), offsetYmm: Number(guides.top.toFixed(3)) };
        }
        if (anchor === "top-right") {
          return { ...current, offsetXmm: Number(guides.right.toFixed(3)), offsetYmm: Number(guides.top.toFixed(3)) };
        }
        if (anchor === "bottom-left") {
          return { ...current, offsetXmm: Number(guides.left.toFixed(3)), offsetYmm: Number(guides.bottom.toFixed(3)) };
        }
        if (anchor === "bottom-right") {
          return { ...current, offsetXmm: Number(guides.right.toFixed(3)), offsetYmm: Number(guides.bottom.toFixed(3)) };
        }

        return {
          ...current,
          offsetXmm: Number(guides.centerX.toFixed(3)),
          offsetYmm: Number(guides.centerY.toFixed(3)),
        };
      });
      maybeShowGenerateFirstPopup();
    },
    [maybeShowGenerateFirstPopup],
  );

  const nudgePreviewPan = useCallback((deltaXpx: number, deltaYpx: number) => {
    setPreviewPan((current) => ({
      x: Number((current.x + deltaXpx).toFixed(1)),
      y: Number((current.y + deltaYpx).toFixed(1)),
    }));
  }, []);

  useEffect(() => {
    if (activeTab !== "studio" || !canUseStudio) {
      return;
    }

    const handleKeyDown = (event: KeyboardEvent) => {
      if (!["ArrowLeft", "ArrowRight", "ArrowUp", "ArrowDown"].includes(event.key)) {
        return;
      }

      const target = event.target as HTMLElement | null;
      if (
        target &&
        (target.tagName === "INPUT" ||
          target.tagName === "TEXTAREA" ||
          target.tagName === "SELECT" ||
          target.isContentEditable)
      ) {
        return;
      }

      const baseStep = Math.max(0.1, positionStepMm);
      const step = event.shiftKey ? baseStep * 5 : event.altKey ? baseStep / 5 : baseStep;
      if (event.key === "ArrowLeft") {
        nudgeMosaicOffset(-step, 0);
      } else if (event.key === "ArrowRight") {
        nudgeMosaicOffset(step, 0);
      } else if (event.key === "ArrowUp") {
        nudgeMosaicOffset(0, -step);
      } else if (event.key === "ArrowDown") {
        nudgeMosaicOffset(0, step);
      }
      event.preventDefault();
    };

    window.addEventListener("keydown", handleKeyDown);
    return () => {
      window.removeEventListener("keydown", handleKeyDown);
    };
  }, [activeTab, canUseStudio, positionStepMm, nudgeMosaicOffset]);

  const buildProjectGenerationPayload = (): ProjectSaveGenerationRequest | null => {
    if (!mosaicResult) {
      return null;
    }

    return {
      name: generationNameDraft.trim(),
      note: generationNoteDraft.trim(),
      snapshot: {
        mosaic: mosaicResult,
        include_color_ids: includeColorIds,
        exclude_color_ids: excludeColorIds,
        grout_color_id: studioForm.groutColorId,
        preview_zoom: previewZoom,
        preview_pan_x: previewPan.x,
        preview_pan_y: previewPan.y,
      },
    };
  };

  const applyProjectGeneration = (generation: ProjectGenerationRead, project?: ProjectRead) => {
    setMosaicResult(normalizeMosaicResult(generation.snapshot.mosaic));
    setHasGeneratedSnapshot(true);
    setShowGenerateFirstPopup(false);
    setMuteGenerateFirstPopupUntilGeneration(false);
    setLiveRegenerateError(null);
    setIncludeColorIds(generation.snapshot.include_color_ids);
    setExcludeColorIds(generation.snapshot.exclude_color_ids);
    setStudioForm((current) => ({
      ...current,
      fieldWidthMm: generation.snapshot.mosaic.field_width_mm,
      fieldHeightMm: generation.snapshot.mosaic.field_height_mm,
      cellSizeMm: generation.snapshot.mosaic.cell_size_mm,
      gapMm: generation.snapshot.mosaic.gap_mm,
      maxColors: generation.snapshot.mosaic.requested_max_colors,
      groutColorId: generation.snapshot.grout_color_id,
      offsetXmm: generation.snapshot.mosaic.offset_x_mm,
      offsetYmm: generation.snapshot.mosaic.offset_y_mm,
    }));
    setPreviewZoom(clampNumber(generation.snapshot.preview_zoom, 0.4, 8));
    setPreviewPan({
      x: generation.snapshot.preview_pan_x,
      y: generation.snapshot.preview_pan_y,
    });

    if (project && project.source_image_base64.trim().length > 0) {
      const sourceMime =
        project.source_image_mime_type.trim().length > 0
          ? project.source_image_mime_type
          : "image/png";
      setImagePreviewUrl(`data:${sourceMime};base64,${project.source_image_base64}`);
      setImageFile(null);
    }
  };

  const onDbProviderChange = (provider: string) => {
    const nextProvider = provider.trim().toLowerCase();
    const defaults = defaultDbConnectionForm(nextProvider);
    setDbConnectionForm(defaults);
    setDbDraft((current) => ({
      ...current,
      provider: nextProvider,
      connection_string: buildConnectionString(nextProvider, defaults),
    }));
  };

  const setDbConnectionField = <K extends keyof DbConnectionForm>(
    field: K,
    value: DbConnectionForm[K],
  ) => {
    setDbConnectionForm((current) => ({ ...current, [field]: value }));
  };

  const onTabClick = (tab: TabKey) => {
    if (tab === activeTab && tab === "studio") {
      setError(null);
      setNotice(
        hasGenerationSourceImage
          ? 'Вы уже на вкладке "Генерация". Нажмите "Сгенерировать мозаику", чтобы обновить превью.'
          : 'Вы уже на вкладке "Генерация". Сначала загрузите изображение, затем нажмите "Сгенерировать мозаику".',
      );
      return;
    }
    setActiveTab(tab);
  };

  const onLogin = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setAuthBusy(true);
    setError(null);
    setNotice(null);
    try {
      const response = await login({
        username: loginUsername.trim(),
        password: loginPassword,
      });
      setAuthToken(response.token);
      setAuthSession({
        is_authenticated: true,
        expires_at: response.expires_at,
        requires_password_change: response.requires_password_change,
        user: response.user,
      });
      setNotice(`Вход выполнен: ${response.user.display_name} (${roleLabel(response.user.role)}).`);
      await refreshBootstrap();
      await refreshProjects();
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : "Не удалось выполнить вход.");
    } finally {
      setAuthBusy(false);
    }
  };

  const onLogout = async () => {
    setAuthBusy(true);
    setError(null);
    setNotice(null);
    try {
      if (authToken) {
        await logout();
      }
    } catch {
      // Ignore logout API failures and clear local session anyway.
    } finally {
      cancelLiveRegeneration();
      setAuthToken(null);
      setAuthSession({
        is_authenticated: false,
        expires_at: null,
        requires_password_change: false,
        user: null,
      });
      setProjects([]);
      setSelectedProjectId(null);
      setSelectedProject(null);
      setProjectShares([]);
      setProjectOrders([]);
      setHasGeneratedSnapshot(false);
      setShowGenerateFirstPopup(false);
      setMuteGenerateFirstPopupUntilGeneration(false);
      setHideGenerateFirstPopupThisSession(false);
      setLiveRegenerateError(null);
      setAuthBusy(false);
      setNotice("Сессия завершена.");
    }
  };

  const applyGridDraft = () => {
    const columns = Math.max(1, Math.floor(gridDraft.columns));
    const rows = Math.max(1, Math.floor(gridDraft.rows));
    const cellFromWidth = (studioForm.fieldWidthMm - (columns - 1) * studioForm.gapMm) / columns;
    const cellFromHeight = (studioForm.fieldHeightMm - (rows - 1) * studioForm.gapMm) / rows;
    const nextCell = Math.min(cellFromWidth, cellFromHeight);
    if (!Number.isFinite(nextCell) || nextCell <= 0) {
      setError("Сетка не помещается в поле с текущим расстоянием между ячейками.");
      return;
    }
    setError(null);
    setStudioForm((current) => ({ ...current, cellSizeMm: Number(nextCell.toFixed(3)) }));
    maybeShowGenerateFirstPopup();
    setNotice("Параметры сетки применены: размер ячейки пересчитан автоматически.");
  };

  const applyNewColorHex = (hexValue: string) => {
    const normalized = normalizeHexIfPossible(hexValue);
    const suggested = suggestColorMeta(normalized);
    setNewColorForm((current) => {
      const next: ColorCreate = { ...current, rgb_hex: normalized };
      if (suggested) {
        if (!newColorTouched.name || current.name.trim() === "") {
          next.name = suggested.name;
        }
        if (!newColorTouched.ral || current.ral_code.trim() === "") {
          next.ral_code = suggested.ralCode;
        }
      }
      return next;
    });
  };

  const applyEditingColorHex = (hexValue: string) => {
    const normalized = normalizeHexIfPossible(hexValue);
    const suggested = suggestColorMeta(normalized);
    setEditingColorForm((current) => {
      if (!current) {
        return current;
      }
      const next: ColorCreate = { ...current, rgb_hex: normalized };
      if (suggested) {
        if (!editingColorTouched.name || current.name.trim() === "") {
          next.name = suggested.name;
        }
        if (!editingColorTouched.ral || current.ral_code.trim() === "") {
          next.ral_code = suggested.ralCode;
        }
      }
      return next;
    });
  };

  const applyNewGroutHex = (hexValue: string) => {
    const normalized = normalizeHexIfPossible(hexValue);
    const suggested = suggestColorMeta(normalized);
    setNewGroutForm((current) => ({
      ...current,
      rgb_hex: normalized,
      name:
        suggested && (!newGroutNameTouched || current.name.trim() === "")
          ? suggested.name
          : current.name,
    }));
  };

  const applyEditingGroutHex = (hexValue: string) => {
    const normalized = normalizeHexIfPossible(hexValue);
    const suggested = suggestColorMeta(normalized);
    setEditingGroutForm((current) => {
      if (!current) {
        return current;
      }
      return {
        ...current,
        rgb_hex: normalized,
        name:
          suggested && (!editingGroutNameTouched || current.name.trim() === "")
            ? suggested.name
            : current.name,
      };
    });
  };

  const colorMode = (colorId: number): "neutral" | "include" | "exclude" => {
    if (includeColorIds.includes(colorId)) {
      return "include";
    }
    if (excludeColorIds.includes(colorId)) {
      return "exclude";
    }
    return "neutral";
  };

  const cycleColorMode = (colorId: number) => {
    const mode = colorMode(colorId);
    if (mode === "neutral") {
      setIncludeColorIds((items) => [...items, colorId]);
      setExcludeColorIds((items) => items.filter((id) => id !== colorId));
      maybeShowGenerateFirstPopup();
      return;
    }
    if (mode === "include") {
      setIncludeColorIds((items) => items.filter((id) => id !== colorId));
      setExcludeColorIds((items) => [...items, colorId]);
      maybeShowGenerateFirstPopup();
      return;
    }
    setExcludeColorIds((items) => items.filter((id) => id !== colorId));
    maybeShowGenerateFirstPopup();
  };

  const resetPreviewTransform = () => {
    setPreviewZoom(1);
    setPreviewPan({ x: 0, y: 0 });
  };

  const onPreviewWheel = (event: React.WheelEvent<HTMLDivElement>) => {
    if (!previewSource) {
      return;
    }

    event.preventDefault();
    const direction = event.deltaY < 0 ? 1 : -1;
    const zoomStep = direction > 0 ? 0.12 : -0.12;
    setPreviewZoom((current) => clampNumber(Number((current + zoomStep).toFixed(2)), 0.4, 8));
  };

  const onPreviewPointerDown = (event: React.PointerEvent<HTMLDivElement>) => {
    if (!previewSource) {
      return;
    }

    previewDragRef.current = {
      pointerId: event.pointerId,
      startX: event.clientX,
      startY: event.clientY,
      originX: previewPan.x,
      originY: previewPan.y,
    };

    setPreviewDragging(true);
    event.currentTarget.setPointerCapture(event.pointerId);
  };

  const onPreviewPointerMove = (event: React.PointerEvent<HTMLDivElement>) => {
    if (!previewDragging || !previewDragRef.current) {
      return;
    }

    if (event.pointerId !== previewDragRef.current.pointerId) {
      return;
    }

    const dx = event.clientX - previewDragRef.current.startX;
    const dy = event.clientY - previewDragRef.current.startY;
    setPreviewPan({
      x: previewDragRef.current.originX + dx,
      y: previewDragRef.current.originY + dy,
    });
  };

  const onPreviewPointerUp = (event: React.PointerEvent<HTMLDivElement>) => {
    if (previewDragRef.current && event.pointerId === previewDragRef.current.pointerId) {
      previewDragRef.current = null;
      setPreviewDragging(false);
      if (event.currentTarget.hasPointerCapture(event.pointerId)) {
        event.currentTarget.releasePointerCapture(event.pointerId);
      }
    }
  };

  const onPreviewZoomChange = (value: number) => {
    setPreviewZoom(clampNumber(value, 0.4, 8));
  };

  const onImageChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0] ?? null;
    cancelLiveRegeneration();
    setImageFile(file);
    // Reset previous result so preview switches to the newly selected source image.
    setMosaicResult(null);
    setHasGeneratedSnapshot(false);
    setShowGenerateFirstPopup(false);
    setMuteGenerateFirstPopupUntilGeneration(false);
    setLiveRegenerateError(null);
    setError(null);
    if (imagePreviewUrl) {
      URL.revokeObjectURL(imagePreviewUrl);
      setImagePreviewUrl(null);
    }
    resetPreviewTransform();
    if (file) {
      setImagePreviewUrl(URL.createObjectURL(file));
    }
  };

  const onGenerateMosaic = async () => {
    if (!canUseStudio) {
      setError("Роль просмотра не может запускать генерацию.");
      return;
    }

    if (!generationSourceImage) {
      setError("Перед генерацией загрузите изображение или откройте проект с сохраненным исходником.");
      return;
    }
    setBusy(true);
    cancelLiveRegeneration();
    setError(null);
    setNotice(null);
    setLiveRegenerateError(null);
    try {
      const response = await generateMosaic(
        generationSourceImage,
        studioForm,
        includeColorIds,
        excludeColorIds,
      );
      setMosaicResult(normalizeMosaicResult(response));
      setHasGeneratedSnapshot(true);
      setShowGenerateFirstPopup(false);
      setMuteGenerateFirstPopupUntilGeneration(false);
      setNotice("Мозаика успешно сгенерирована.");
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : "Ошибка генерации.");
    } finally {
      setBusy(false);
    }
  };

  const onReplaceMosaicColor = async () => {
    if (!canUseStudio) {
      setError("Роль просмотра не может менять цвета.");
      return;
    }

    if (!mosaicResult) {
      return;
    }
    if (replaceFromColorId === null || replaceToColorId === null) {
      setError("Выберите исходный и целевой цвет.");
      return;
    }
    if (replaceFromColorId === replaceToColorId) {
      setError("Выберите разные цвета для замены.");
      return;
    }

    setBusy(true);
    setError(null);
    setNotice(null);

    try {
      const response = await replaceMosaicColor({
        grid_color_ids: mosaicResult.grid_color_ids,
        from_color_id: replaceFromColorId,
        to_color_id: replaceToColorId,
        field_width_mm: mosaicResult.field_width_mm,
        field_height_mm: mosaicResult.field_height_mm,
        cell_size_mm: mosaicResult.cell_size_mm,
        gap_mm: mosaicResult.gap_mm,
        offset_x_mm: mosaicResult.offset_x_mm,
        offset_y_mm: mosaicResult.offset_y_mm,
        grout_color_hex: mosaicResult.grout_color_hex,
        requested_max_colors: mosaicResult.requested_max_colors,
      });

      setMosaicResult(normalizeMosaicResult(response));
      setHasGeneratedSnapshot(true);
      setShowGenerateFirstPopup(false);
      setMuteGenerateFirstPopupUntilGeneration(false);
      setLiveRegenerateError(null);
      setNotice("Цвет успешно заменен во всей мозаике.");
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : "Ошибка замены цвета.");
    } finally {
      setBusy(false);
    }
  };

  const onExportMosaic = async (
    format:
      | "png"
      | "jpeg"
      | "svg"
      | "pdf"
      | "materials-csv"
      | "grid-csv"
      | "modules-csv"
      | "assembly-kit-pdf",
  ) => {
    if (!canUseStudio) {
      setError("Роль просмотра не может экспортировать файлы.");
      return;
    }

    if (!mosaicResult) {
      setError("Сначала сгенерируйте мозаику, затем выгружайте файл.");
      return;
    }

    setBusy(true);
    setError(null);
    setNotice(null);
    try {
      const baseFileName = (
        selectedProject?.name ??
        generationNameDraft.trim() ??
        "mozaika"
      )
        .trim()
        .replace(/[\\/:*?"<>|]+/g, "-");

      const response = await exportMosaic(format, {
        grid_color_ids: mosaicResult.grid_color_ids,
        field_width_mm: mosaicResult.field_width_mm,
        field_height_mm: mosaicResult.field_height_mm,
        cell_size_mm: mosaicResult.cell_size_mm,
        gap_mm: mosaicResult.gap_mm,
        offset_x_mm: mosaicResult.offset_x_mm,
        offset_y_mm: mosaicResult.offset_y_mm,
        grout_color_hex: mosaicResult.grout_color_hex,
        dpi: Math.max(72, Math.min(600, Math.round(exportDpi))),
        mirror_horizontal: exportMirrorHorizontal,
        include_legend: exportIncludeLegend,
        module_chip_columns: Math.max(1, Math.min(256, Math.floor(moduleChipColumns))),
        module_chip_rows: Math.max(1, Math.min(256, Math.floor(moduleChipRows))),
        module_start_number: Math.max(1, Math.floor(moduleStartNumber)),
        include_color_numbers: exportIncludeColorNumbers,
        file_name: baseFileName.length > 0 ? baseFileName : "mozaika",
      });

      const link = document.createElement("a");
      const url = URL.createObjectURL(response.blob);
      link.href = url;
      link.download = response.fileName;
      document.body.append(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(url);

      setNotice(`Файл готов: ${response.fileName}`);
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : "Не удалось экспортировать файл.");
    } finally {
      setBusy(false);
    }
  };

  const onCreateProject = async () => {
    if (!canEditProjects) {
      setError("Роль просмотра не может создавать проекты.");
      return;
    }

    if (!mosaicResult) {
      setError("Сначала сгенерируйте мозаику, затем сохраняйте проект.");
      return;
    }

    const projectName = projectNameDraft.trim();
    if (projectName.length < 2) {
      setError("Введите название проекта (минимум 2 символа).");
      return;
    }

    const generationPayload = buildProjectGenerationPayload();
    if (!generationPayload) {
      setError("Для сохранения версии нужна сгенерированная мозаика.");
      return;
    }

    setBusy(true);
    setError(null);
    setNotice(null);
    try {
      let sourceImageBase64 = "";
      let sourceImageMimeType = "";
      if (imageFile) {
        sourceImageBase64 = await fileToBase64(imageFile);
        sourceImageMimeType = imageFile.type ?? "";
      } else if (selectedProject) {
        sourceImageBase64 = selectedProject.source_image_base64;
        sourceImageMimeType = selectedProject.source_image_mime_type;
      }

      const response = await createProject({
        name: projectName,
        description: projectDescriptionDraft.trim(),
        source_image_mime_type: sourceImageMimeType,
        source_image_base64: sourceImageBase64,
        initial_generation: generationPayload,
      });
      setSelectedProject(response);
      setSelectedProjectId(response.id);
      setProjectNameDraft(response.name);
      setProjectDescriptionDraft(response.description);
      if (response.active_generation) {
        applyProjectGeneration(response.active_generation, response);
      }
      setOrderForm((current) => ({
        ...current,
        generation_id: response.active_generation_id ?? null,
      }));
      setShareGenerationId(response.active_generation_id ?? null);
      await refreshProjectWorkflow(response.id);
      await refreshProjects();
      setNotice(`Проект сохранен: ${response.name}`);
    } catch (requestError) {
      setError(
        requestError instanceof Error ? requestError.message : "Не удалось создать проект.",
      );
    } finally {
      setBusy(false);
    }
  };

  const onSaveGenerationToProject = async () => {
    if (!canEditProjects) {
      setError("Роль просмотра не может сохранять версии.");
      return;
    }

    if (selectedProjectId === null) {
      setError("Выберите проект для сохранения новой версии.");
      return;
    }

    const generationPayload = buildProjectGenerationPayload();
    if (!generationPayload) {
      setError("Сначала сгенерируйте мозаику.");
      return;
    }

    setBusy(true);
    setError(null);
    setNotice(null);
    try {
      await saveProjectGeneration(selectedProjectId, generationPayload);
      const project = await fetchProject(selectedProjectId);
      setSelectedProject(project);
      if (project.active_generation) {
        applyProjectGeneration(project.active_generation, project);
      }
      setOrderForm((current) => ({
        ...current,
        generation_id: project.active_generation_id ?? null,
      }));
      setShareGenerationId(project.active_generation_id ?? null);
      await refreshProjectWorkflow(project.id);
      await refreshProjects();
      setNotice(`Версия сохранена в проект #${selectedProjectId}.`);
    } catch (requestError) {
      setError(
        requestError instanceof Error
          ? requestError.message
          : "Не удалось сохранить версию в проект.",
      );
    } finally {
      setBusy(false);
    }
  };

  const onOpenProject = async (projectId: number) => {
    setBusy(true);
    setError(null);
    setNotice(null);
    try {
      const project = await fetchProject(projectId);
      setSelectedProject(project);
      setSelectedProjectId(project.id);
      setProjectNameDraft(project.name);
      setProjectDescriptionDraft(project.description);

      if (project.active_generation) {
        applyProjectGeneration(project.active_generation, project);
      }
      setOrderForm((current) => ({
        ...current,
        generation_id: project.active_generation_id ?? null,
      }));
      setShareGenerationId(project.active_generation_id ?? null);
      await refreshProjectWorkflow(project.id);

      setNotice(`Проект загружен: ${project.name}`);
    } catch (requestError) {
      setError(
        requestError instanceof Error ? requestError.message : "Не удалось загрузить проект.",
      );
    } finally {
      setBusy(false);
    }
  };

  const onActivateProjectGeneration = async (projectId: number, generationId: number) => {
    if (!canEditProjects) {
      setError("Роль просмотра не может активировать версии.");
      return;
    }

    setBusy(true);
    setError(null);
    setNotice(null);
    try {
      const project = await activateProjectGeneration(projectId, generationId);
      setSelectedProject(project);
      setSelectedProjectId(project.id);
      setProjectNameDraft(project.name);
      setProjectDescriptionDraft(project.description);
      if (project.active_generation) {
        applyProjectGeneration(project.active_generation, project);
      } else {
        const generation = await fetchProjectGeneration(projectId, generationId);
        applyProjectGeneration(generation, project);
      }
      setOrderForm((current) => ({
        ...current,
        generation_id: project.active_generation_id ?? generationId,
      }));
      setShareGenerationId(project.active_generation_id ?? generationId);
      await refreshProjectWorkflow(project.id);
      await refreshProjects();
      setNotice(`Загружена версия #${generationId} из проекта #${projectId}.`);
    } catch (requestError) {
      setError(
        requestError instanceof Error ? requestError.message : "Не удалось загрузить сохраненную версию.",
      );
    } finally {
      setBusy(false);
    }
  };

  const onUpdateProjectMeta = async () => {
    if (!canEditProjects) {
      setError("Роль просмотра не может менять метаданные проекта.");
      return;
    }

    if (selectedProjectId === null) {
      setError("Выберите проект.");
      return;
    }
    if (projectNameDraft.trim().length < 2) {
      setError("Название проекта должно содержать минимум 2 символа.");
      return;
    }

    setBusy(true);
    setError(null);
    setNotice(null);
    try {
      const project = await updateProject(selectedProjectId, {
        name: projectNameDraft.trim(),
        description: projectDescriptionDraft.trim(),
      });
      setSelectedProject(project);
      await refreshProjects();
      setNotice("Метаданные проекта обновлены.");
    } catch (requestError) {
      setError(
        requestError instanceof Error ? requestError.message : "Не удалось обновить проект.",
      );
    } finally {
      setBusy(false);
    }
  };

  const onCreateProjectShare = async () => {
    if (selectedProjectId === null) {
      setError("Сначала выберите проект.");
      return;
    }

    if (!canEditProjects) {
      setError("Недостаточно прав для создания shared-ссылки.");
      return;
    }

    setBusy(true);
    setError(null);
    setNotice(null);
    try {
      const share = await createProjectShare(selectedProjectId, {
        generation_id: shareGenerationId,
        expires_in_days: shareExpiresInDays > 0 ? shareExpiresInDays : null,
      });
      await refreshProjectWorkflow(selectedProjectId);
      const shareUrl = `${window.location.origin}${window.location.pathname}?share=${encodeURIComponent(share.token)}`;
      try {
        await navigator.clipboard.writeText(shareUrl);
        setNotice(`Ссылка создана и скопирована: ${shareUrl}`);
      } catch {
        setNotice(`Ссылка создана: ${shareUrl}`);
      }
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : "Не удалось создать shared-ссылку.");
    } finally {
      setBusy(false);
    }
  };

  const onCopyShareUrl = async (token: string) => {
    const shareUrl = `${window.location.origin}${window.location.pathname}?share=${encodeURIComponent(token)}`;
    try {
      await navigator.clipboard.writeText(shareUrl);
      setNotice("Shared-ссылка скопирована в буфер.");
    } catch {
      setNotice(shareUrl);
    }
  };

  const onRevokeShare = async (shareId: number) => {
    if (selectedProjectId === null) {
      return;
    }

    if (!canEditProjects) {
      setError("Недостаточно прав для отзыва ссылки.");
      return;
    }

    setBusy(true);
    setError(null);
    setNotice(null);
    try {
      await revokeProjectShare(selectedProjectId, shareId);
      await refreshProjectWorkflow(selectedProjectId);
      setNotice("Ссылка отозвана.");
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : "Не удалось отозвать ссылку.");
    } finally {
      setBusy(false);
    }
  };

  const onCreateProjectOrder = async () => {
    if (selectedProjectId === null) {
      setError("Сначала выберите проект.");
      return;
    }
    if (!canEditProjects) {
      setError("Недостаточно прав для отправки предварительного заказа.");
      return;
    }
    if (orderForm.customer_name.trim().length < 2) {
      setError("Введите контактное лицо (минимум 2 символа).");
      return;
    }

    setBusy(true);
    setError(null);
    setNotice(null);
    try {
      await createProjectOrder(selectedProjectId, {
        ...orderForm,
        customer_name: orderForm.customer_name.trim(),
        customer_email: orderForm.customer_email.trim(),
        customer_phone: orderForm.customer_phone.trim(),
        comment: orderForm.comment.trim(),
      });
      await refreshProjectWorkflow(selectedProjectId);
      setNotice("Предварительный заказ отправлен на согласование.");
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : "Не удалось отправить предварительный заказ.");
    } finally {
      setBusy(false);
    }
  };

  const onUpdateOrderStatus = async (orderId: number) => {
    if (selectedProjectId === null) {
      return;
    }
    if (!isAdmin) {
      setError("Только администратор может менять статус заказа.");
      return;
    }

    const nextStatus = orderStatusDraftByOrder[orderId] ?? "in_review";
    const statusComment = orderStatusCommentByOrder[orderId] ?? "";

    setBusy(true);
    setError(null);
    setNotice(null);
    try {
      await updateProjectOrderStatus(selectedProjectId, orderId, {
        status: nextStatus,
        status_comment: statusComment,
      });
      await refreshProjectWorkflow(selectedProjectId);
      setNotice("Статус заказа обновлен.");
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : "Не удалось обновить статус заказа.");
    } finally {
      setBusy(false);
    }
  };

  const onCreateColor = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setBusy(true);
    setError(null);
    setNotice(null);
    try {
      await createColor(newColorForm);
      setNewColorForm({ name: "", ral_code: "", rgb_hex: "#C1121F", is_active: true });
      setNewColorTouched({ name: false, ral: false });
      await refreshBootstrap();
      setNotice("Цвет добавлен.");
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : "Не удалось добавить цвет.");
    } finally {
      setBusy(false);
    }
  };

  const onBulkImportColors = async () => {
    setBusy(true);
    setError(null);
    setNotice(null);
    try {
      const colors = parseBulkPaletteRows(bulkPaletteText);
      await createColorsBulk({ colors });
      setBulkPaletteText("");
      await refreshBootstrap();
      setNotice(`Импортировано цветов: ${colors.length}.`);
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : "Ошибка пакетного импорта.");
    } finally {
      setBusy(false);
    }
  };

  const onStartEditColor = (color: ColorRead) => {
    setEditingColorId(color.id);
    setEditingColorTouched({ name: false, ral: false });
    setEditingColorForm({
      name: color.name,
      ral_code: color.ral_code,
      rgb_hex: color.rgb_hex,
      is_active: color.is_active,
    });
  };

  const onSaveColorEdit = async () => {
    if (editingColorId === null || editingColorForm === null) {
      return;
    }
    setBusy(true);
    setError(null);
    setNotice(null);
    try {
      await patchColor(editingColorId, editingColorForm);
      setEditingColorId(null);
      setEditingColorForm(null);
      setEditingColorTouched({ name: false, ral: false });
      await refreshBootstrap();
      setNotice("Цвет обновлен.");
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : "Не удалось обновить цвет.");
    } finally {
      setBusy(false);
    }
  };

  const onDeactivateColor = async (colorId: number) => {
    setBusy(true);
    setError(null);
    setNotice(null);
    try {
      await deactivateColor(colorId);
      await refreshBootstrap();
      setNotice("Цвет деактивирован.");
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : "Не удалось деактивировать цвет.");
    } finally {
      setBusy(false);
    }
  };

  const onSaveSettings = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setBusy(true);
    setError(null);
    setNotice(null);
    try {
      const payload = await updateSettings(settingsDraft);
      setStudioForm((current) => ({
        ...current,
        fieldWidthMm: payload.default_field_width_mm,
        fieldHeightMm: payload.default_field_height_mm,
        cellSizeMm: payload.default_cell_size_mm,
        gapMm: payload.default_gap_mm,
      }));
      await refreshBootstrap();
      setNotice("Настройки сохранены.");
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : "Не удалось сохранить настройки.");
    } finally {
      setBusy(false);
    }
  };

  const onSaveDatabaseConfig = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setBusy(true);
    setError(null);
    setNotice(null);
    const connectionString = composedConnectionString.trim();
    if (connectionString.length === 0) {
      setBusy(false);
      setError("Заполните параметры подключения к базе данных.");
      return;
    }
    try {
      const payload = await configureDatabase({
        provider: dbDraft.provider.trim().toLowerCase(),
        connection_string: connectionString,
        echo: Boolean(dbDraft.echo),
        create_schema: Boolean(dbDraft.create_schema),
        seed_defaults: Boolean(dbDraft.seed_defaults),
      });
      setDbDraft((current) => ({
        ...current,
        provider: payload.provider,
        connection_string: payload.connection_string,
        echo: payload.echo,
      }));
      setDbConnectionForm(parseDbConnectionForm(payload.provider, payload.connection_string));
      if (payload.supported_providers.length > 0) {
        setSupportedDbProviders(payload.supported_providers);
      }
      await refreshBootstrap();
      setNotice("Провайдер БД применен. Структура базы подготовлена.");
    } catch (requestError) {
      setError(
        requestError instanceof Error
          ? requestError.message
          : "Не удалось применить подключение к базе данных.",
      );
    } finally {
      setBusy(false);
    }
  };

  const onTestDatabaseConfig = async () => {
    setBusy(true);
    setError(null);
    setNotice(null);
    const connectionString = composedConnectionString.trim();
    if (connectionString.length === 0) {
      setBusy(false);
      setError("Заполните параметры подключения к базе данных.");
      return;
    }
    try {
      const payload = await testDatabaseConnection({
        provider: dbDraft.provider.trim().toLowerCase(),
        connection_string: connectionString,
        echo: Boolean(dbDraft.echo),
        create_schema: false,
        seed_defaults: false,
      });
      setNotice(payload.message);
    } catch (requestError) {
      setError(
        requestError instanceof Error
          ? requestError.message
          : "Не удалось проверить подключение к базе данных.",
      );
    } finally {
      setBusy(false);
    }
  };

  const onCreateGroutColor = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setBusy(true);
    setError(null);
    setNotice(null);
    try {
      await createGroutColor(newGroutForm);
      setNewGroutForm({ name: "", rgb_hex: "#F2EFEA", is_active: true });
      setNewGroutNameTouched(false);
      await refreshBootstrap();
      setNotice("Цвет заполнения добавлен.");
    } catch (requestError) {
      setError(
        requestError instanceof Error ? requestError.message : "Не удалось добавить цвет заполнения.",
      );
    } finally {
      setBusy(false);
    }
  };

  const onStartEditGrout = (color: GroutColorRead) => {
    setEditingGroutId(color.id);
    setEditingGroutNameTouched(false);
    setEditingGroutForm({
      name: color.name,
      rgb_hex: color.rgb_hex,
      is_active: color.is_active,
    });
  };

  const onSaveEditGrout = async () => {
    if (editingGroutId === null || editingGroutForm === null) {
      return;
    }
    setBusy(true);
    setError(null);
    setNotice(null);
    try {
      await patchGroutColor(editingGroutId, editingGroutForm);
      setEditingGroutId(null);
      setEditingGroutForm(null);
      setEditingGroutNameTouched(false);
      await refreshBootstrap();
      setNotice("Цвет заполнения обновлен.");
    } catch (requestError) {
      setError(
        requestError instanceof Error ? requestError.message : "Не удалось обновить цвет заполнения.",
      );
    } finally {
      setBusy(false);
    }
  };

  const onDeactivateGrout = async (groutColorId: number) => {
    setBusy(true);
    setError(null);
    setNotice(null);
    try {
      await deactivateGroutColor(groutColorId);
      await refreshBootstrap();
      setNotice("Цвет заполнения деактивирован.");
    } catch (requestError) {
      setError(
        requestError instanceof Error ? requestError.message : "Не удалось деактивировать цвет заполнения.",
      );
    } finally {
      setBusy(false);
    }
  };

  if (!authInitialized) {
    return <div className="app-loading">Загрузка студии Mozaika...</div>;
  }

  if (!isAuthenticated) {
    return (
      <div className="login-shell">
        <section className="login-panel">
          <p className="login-kicker">Mozaika</p>
          <h1 className="login-title">Вход в студию</h1>
          <p className="login-subtitle">
            Авторизуйтесь, чтобы открыть генератор мозаики, проекты, палитру и заказы.
          </p>
          <form className="auth-form" onSubmit={onLogin}>
            <input
              type="text"
              value={loginUsername}
              onChange={(event) => setLoginUsername(event.target.value)}
              placeholder="Логин"
              autoComplete="username"
            />
            <input
              type="password"
              value={loginPassword}
              onChange={(event) => setLoginPassword(event.target.value)}
              placeholder="Пароль"
              autoComplete="current-password"
            />
            <button type="submit" className="button-primary" disabled={authBusy}>
              {authBusy ? "Вход..." : "Войти"}
            </button>
          </form>
          {error && <div className="alert alert-error">{error}</div>}
          {notice && <div className="alert alert-success">{notice}</div>}
          <div className="login-demo">
            <small>Демо-аккаунты: admin/admin123, customer/customer123, viewer/viewer123</small>
          </div>
        </section>
      </div>
    );
  }

  if (loadingBootstrap && bootstrap === null) {
    return <div className="app-loading">Загрузка студии Mozaika...</div>;
  }

  return (
    <div className="app-shell">
      <header className="hero">
        <div>
          <p className="eyebrow">Демо-версия</p>
          <h1>Студия Mozaika</h1>
          <p className="hero-note">
            Генерация мозаики, управление палитрой и админ-настройками в одном интерфейсе.
          </p>
        </div>
        <div className="hero-metrics">
          <article>
            <span>Активных цветов</span>
            <strong>{activePalette.length}</strong>
          </article>
          <article>
            <span>Цветов заполнения</span>
            <strong>{activeGroutColors.length}</strong>
          </article>
          <article>
            <span>Оценка сетки</span>
            <strong>
              {gridEstimate.columns} x {gridEstimate.rows}
            </strong>
          </article>
          <article className="auth-card">
            <span>Пользователь</span>
            <div className="auth-inline">
              <strong>
                {authSession.user?.display_name} ({roleLabel(authSession.user?.role ?? "viewer")})
              </strong>
              <small>до {formatDateTime(authSession.expires_at)}</small>
              <button
                type="button"
                className="button-ghost"
                onClick={() => void onLogout()}
                disabled={authBusy}
              >
                Выйти
              </button>
            </div>
          </article>
        </div>
      </header>

      <nav className="tabs">
        {visibleTabs.map((tab) => (
          <button
            key={tab.key}
            type="button"
            className={tab.key === activeTab ? "tab is-active" : "tab"}
            onClick={() => onTabClick(tab.key)}
          >
            {tab.label}
          </button>
        ))}
      </nav>
      {activeTab === "studio" && (
        <p className="tabs-help">
          Вкладка открывает рабочую область. Расчет выполняется кнопкой "Сгенерировать мозаику" ниже.
        </p>
      )}
      {activeTab === "projects" && (
        <p className="tabs-help">
          Вкладка хранит историю проектов и версий генерации. Любую сохраненную версию можно загрузить обратно в студию.
        </p>
      )}
      {!isAuthenticated && (
        <p className="tabs-help">
          Для сохранения проектов, генерации и экспорта войдите под ролью заказчика или администратора.
        </p>
      )}

      {error && <section className="alert alert-error">{error}</section>}
      {notice && <section className="alert alert-success">{notice}</section>}
      {showGenerateFirstPopup && (
        <div className="modal-overlay" onClick={dismissGenerateFirstPopup}>
          <section
            className="modal-card"
            role="dialog"
            aria-modal="true"
            aria-labelledby="generate-first-title"
            onClick={(event) => event.stopPropagation()}
          >
            <h3 id="generate-first-title">Сначала выполните первый расчет</h3>
            <p>
              Параметры меняются, но мозаика появится только после первого нажатия кнопки
              «Сгенерировать мозаику».
            </p>
            <p className="muted">
              После первого расчета автообновление сможет пересчитывать мозаику автоматически.
            </p>
            <label className="checkbox">
              <input
                type="checkbox"
                checked={hideGenerateFirstPopupThisSession}
                onChange={(event) => setHideGenerateFirstPopupThisSession(event.target.checked)}
              />
              Не показывать это сообщение до выхода из аккаунта
            </label>
            <div className="actions">
              <button
                type="button"
                className="button-primary"
                onClick={dismissGenerateFirstPopup}
              >
                Понятно
              </button>
            </div>
          </section>
        </div>
      )}

      {activeTab === "studio" && (
        <section className="workspace">
          <article className="panel">
            <h2>Параметры генерации</h2>
            <div className="studio-mode-switch" role="tablist" aria-label="Режим интерфейса студии">
              <button
                type="button"
                className={studioViewMode === "basic" ? "mode-chip is-active" : "mode-chip"}
                onClick={() => setStudioViewMode("basic")}
              >
                Базовый
              </button>
              <button
                type="button"
                className={studioViewMode === "advanced" ? "mode-chip is-active" : "mode-chip"}
                onClick={() => setStudioViewMode("advanced")}
              >
                Профи
              </button>
            </div>
            <p className="muted">
              {isStudioAdvancedMode
                ? "Режим Профи: доступны управление смещением, оценка сетки и точная настройка палитры."
                : "Режим Базовый: только ключевые параметры, чтобы не перегружать экран."}
            </p>

            <label className="file-upload">
              <input type="file" accept="image/*" onChange={onImageChange} />
              <span>{imageFile ? imageFile.name : "Нажмите, чтобы выбрать изображение"}</span>
            </label>

            <div className="form-grid two-col">
              <label>
                <LabelTitle
                  text="Ширина поля (мм)"
                  hint="Физическая ширина рабочей зоны, на которую раскладывается мозаика."
                />
                <div className="slider-row">
                  <input
                    type="range"
                    min={200}
                    max={10000}
                    step={10}
                    className="slider-control"
                    value={clampNumber(studioForm.fieldWidthMm, 200, 10000)}
                    onChange={(event) => setStudioFieldFromUser("fieldWidthMm", parseNumberValue(event.target.value))}
                  />
                  <input
                    type="number"
                    min={1}
                    step={1}
                    value={studioForm.fieldWidthMm}
                    onChange={(event) => setStudioFieldFromUser("fieldWidthMm", parseNumberValue(event.target.value))}
                  />
                </div>
              </label>
              <label>
                <LabelTitle
                  text="Высота поля (мм)"
                  hint="Физическая высота рабочей зоны, на которую раскладывается мозаика."
                />
                <div className="slider-row">
                  <input
                    type="range"
                    min={200}
                    max={6000}
                    step={10}
                    className="slider-control"
                    value={clampNumber(studioForm.fieldHeightMm, 200, 6000)}
                    onChange={(event) => setStudioFieldFromUser("fieldHeightMm", parseNumberValue(event.target.value))}
                  />
                  <input
                    type="number"
                    min={1}
                    step={1}
                    value={studioForm.fieldHeightMm}
                    onChange={(event) => setStudioFieldFromUser("fieldHeightMm", parseNumberValue(event.target.value))}
                  />
                </div>
              </label>
              <label>
                <LabelTitle
                  text="Размер ячейки (мм)"
                  hint="Размер одной квадратной плитки. Чем меньше размер, тем больше детализация."
                />
                <div className="slider-row">
                  <input
                    type="range"
                    min={5}
                    max={40}
                    step={0.1}
                    className="slider-control"
                    value={clampNumber(studioForm.cellSizeMm, 5, 40)}
                    onChange={(event) => setStudioFieldFromUser("cellSizeMm", parseNumberValue(event.target.value))}
                  />
                  <input
                    type="text"
                    inputMode="decimal"
                    min={1}
                    step={0.1}
                    value={studioForm.cellSizeMm}
                    onChange={(event) => setStudioFieldFromUser("cellSizeMm", parseNumberValue(event.target.value))}
                  />
                </div>
              </label>
              <label>
                <LabelTitle
                  text="Расстояние между ячейками (мм)"
                  hint="Ширина шва между плитками."
                />
                <div className="slider-row">
                  <input
                    type="range"
                    min={0}
                    max={10}
                    step={0.1}
                    className="slider-control"
                    value={clampNumber(studioForm.gapMm, 0, 10)}
                    onChange={(event) => setStudioFieldFromUser("gapMm", parseNumberValue(event.target.value))}
                  />
                  <input
                    type="text"
                    inputMode="decimal"
                    min={0}
                    step={0.1}
                    value={studioForm.gapMm}
                    onChange={(event) => setStudioFieldFromUser("gapMm", parseNumberValue(event.target.value))}
                  />
                </div>
              </label>
              <label>
                <LabelTitle
                  text="Макс. цветов"
                  hint="Верхний предел количества цветов плиток в мозаике. Это значение «до N», а не строго N."
                />
                <div className="slider-row">
                  <input
                    type="range"
                    min={1}
                    max={Math.max(1, activePalette.length)}
                    step={1}
                    className="slider-control"
                    value={clampNumber(studioForm.maxColors, 1, Math.max(1, activePalette.length))}
                    onChange={(event) => setStudioFieldFromUser("maxColors", Number(event.target.value))}
                  />
                  <input
                    type="number"
                    min={1}
                    max={Math.max(1, activePalette.length)}
                    value={studioForm.maxColors}
                    onChange={(event) => setStudioFieldFromUser("maxColors", Number(event.target.value))}
                  />
                </div>
              </label>
              <label>
                <LabelTitle
                  text="Цвет заполнения"
                  hint="Цвет швов (пространства между ячейками), не цвет самих плиток."
                />
                <select
                  value={studioForm.groutColorId ?? ""}
                  onChange={(event) =>
                    setStudioFieldFromUser(
                      "groutColorId",
                      event.target.value ? Number(event.target.value) : null,
                    )
                  }
                >
                  <option value="">Выберите цвет заполнения</option>
                  {activeGroutColors.map((color) => (
                    <option key={color.id} value={color.id}>
                      {color.name} ({color.rgb_hex})
                    </option>
                  ))}
                </select>
              </label>
              {isStudioAdvancedMode && (
                <>
                  <label>
                    <LabelTitle
                      text="Колонок (оценка)"
                      hint="Целевое число колонок. После применения размер ячейки пересчитается."
                    />
                    <input
                      type="number"
                      min={1}
                      value={gridDraft.columns}
                      onChange={(event) =>
                        setGridDraft((current) => ({
                          ...current,
                          columns: Number(event.target.value),
                        }))
                      }
                    />
                  </label>
                  <label>
                    <LabelTitle
                      text="Рядов (оценка)"
                      hint="Целевое число рядов. После применения размер ячейки пересчитается."
                    />
                    <input
                      type="number"
                      min={1}
                      value={gridDraft.rows}
                      onChange={(event) =>
                        setGridDraft((current) => ({
                          ...current,
                          rows: Number(event.target.value),
                        }))
                      }
                    />
                  </label>
                  <label>
                    <LabelTitle
                      text="Смещение X (мм)"
                      hint="Сдвиг мозаики по оси X. Допускаются отрицательные значения, лишняя часть обрезается по рабочему полю."
                    />
                    <div className="slider-row">
                      <input
                        type="range"
                        min={-5000}
                        max={5000}
                        step={0.1}
                        className="slider-control"
                        value={clampNumber(studioForm.offsetXmm, -5000, 5000)}
                        onChange={(event) => setStudioFieldFromUser("offsetXmm", parseNumberValue(event.target.value))}
                      />
                      <input
                        type="text"
                        inputMode="decimal"
                        step={0.1}
                        value={studioForm.offsetXmm}
                        onChange={(event) => setStudioFieldFromUser("offsetXmm", parseNumberValue(event.target.value))}
                      />
                    </div>
                  </label>
                  <label>
                    <LabelTitle
                      text="Смещение Y (мм)"
                      hint="Сдвиг мозаики по оси Y. Допускаются отрицательные значения, лишняя часть обрезается по рабочему полю."
                    />
                    <div className="slider-row">
                      <input
                        type="range"
                        min={-5000}
                        max={5000}
                        step={0.1}
                        className="slider-control"
                        value={clampNumber(studioForm.offsetYmm, -5000, 5000)}
                        onChange={(event) => setStudioFieldFromUser("offsetYmm", parseNumberValue(event.target.value))}
                      />
                      <input
                        type="text"
                        inputMode="decimal"
                        step={0.1}
                        value={studioForm.offsetYmm}
                        onChange={(event) => setStudioFieldFromUser("offsetYmm", parseNumberValue(event.target.value))}
                      />
                    </div>
                  </label>
                  <label>
                    <LabelTitle
                      text="Шаг позиционирования (мм)"
                      hint="Шаг для кнопок сдвига и горячих клавиш стрелок. Shift+стрелка = крупный шаг, Alt+стрелка = точный шаг."
                    />
                    <div className="slider-row">
                      <input
                        type="range"
                        min={0.1}
                        max={500}
                        step={0.1}
                        className="slider-control"
                        value={clampNumber(positionStepMm, 0.1, 500)}
                        onChange={(event) =>
                          setPositionStepMm(Math.max(0.1, parseNumberValue(event.target.value)))
                        }
                      />
                      <input
                        type="text"
                        inputMode="decimal"
                        min={0.1}
                        step={0.1}
                        value={positionStepMm}
                        onChange={(event) => {
                          const parsed = parseNumberValue(event.target.value);
                          if (!Number.isFinite(parsed)) {
                            return;
                          }
                          setPositionStepMm(Math.max(0.1, parsed));
                        }}
                      />
                    </div>
                  </label>
                  <div className="positioning-info">
                    <span>
                      Перекрытие по X/Y: {offsetGuides.overflowX.toFixed(1)} / {offsetGuides.overflowY.toFixed(1)} мм
                    </span>
                    <span>
                      Рекомендованный шаг по сетке: {offsetGuides.pitchMm.toFixed(1)} мм
                    </span>
                  </div>
                </>
              )}
            </div>
            {isStudioAdvancedMode && (
              <>
                <div className="positioning-pad">
                  <button
                    type="button"
                    className="button-ghost"
                    onClick={() => nudgeMosaicOffset(0, -positionStepMm)}
                    disabled={busy || !canUseStudio}
                  >
                    ↑
                  </button>
                  <button
                    type="button"
                    className="button-ghost"
                    onClick={() => nudgeMosaicOffset(-positionStepMm, 0)}
                    disabled={busy || !canUseStudio}
                  >
                    ←
                  </button>
                  <button
                    type="button"
                    className="button-ghost"
                    onClick={() => nudgeMosaicOffset(positionStepMm, 0)}
                    disabled={busy || !canUseStudio}
                  >
                    →
                  </button>
                  <button
                    type="button"
                    className="button-ghost"
                    onClick={() => nudgeMosaicOffset(0, positionStepMm)}
                    disabled={busy || !canUseStudio}
                  >
                    ↓
                  </button>
                  <button
                    type="button"
                    className="button-ghost"
                    onClick={() => setPositionStepMm(Number(offsetGuides.pitchMm.toFixed(1)))}
                    disabled={busy || !canUseStudio}
                  >
                    Шаг = сетка
                  </button>
                </div>
                <div className="actions position-actions">
                  <button
                    type="button"
                    className="button-secondary"
                    onClick={() => alignMosaicOffset("top-left")}
                    disabled={busy || !canUseStudio}
                  >
                    Левый верх
                  </button>
                  <button
                    type="button"
                    className="button-secondary"
                    onClick={() => alignMosaicOffset("center")}
                    disabled={busy || !canUseStudio}
                  >
                    Центр
                  </button>
                  <button
                    type="button"
                    className="button-secondary"
                    onClick={() => alignMosaicOffset("bottom-right")}
                    disabled={busy || !canUseStudio}
                  >
                    Правый низ
                  </button>
                  <button
                    type="button"
                    className="button-ghost"
                    onClick={() => alignMosaicOffset("top-right")}
                    disabled={busy || !canUseStudio}
                  >
                    Правый верх
                  </button>
                  <button
                    type="button"
                    className="button-ghost"
                    onClick={() => alignMosaicOffset("bottom-left")}
                    disabled={busy || !canUseStudio}
                  >
                    Левый низ
                  </button>
                  <button
                    type="button"
                    className="button-ghost"
                    onClick={() => {
                      setStudioFieldFromUser("offsetXmm", 0);
                      setStudioFieldFromUser("offsetYmm", 0);
                    }}
                    disabled={busy || !canUseStudio}
                  >
                    Сброс 0/0
                  </button>
                </div>
                <div className="actions">
                  <button
                    type="button"
                    className="button-secondary"
                    onClick={applyGridDraft}
                    disabled={busy || !canUseStudio}
                  >
                    Применить оценку сетки
                  </button>
                </div>
              </>
            )}
            <p className="muted">
              Для нормальной детализации ставьте больше 1 цвета. Сейчас: {studioForm.maxColors}.
            </p>

            <details className="inline-editor fold-card" open={isStudioAdvancedMode}>
              <summary>
                Стратегия палитры
                <FieldHint text="Нажатие по цвету переключает режим: нейтрально -> включить -> исключить." />
              </summary>
              <div className="fold-content">
                <p className="muted">
                  Нажимайте на цвет для переключения режима: нейтрально - включить - исключить.
                </p>
                <div className="color-chip-grid">
                  {activePalette.map((color) => (
                    <button
                      type="button"
                      key={color.id}
                      className={`color-chip mode-${colorMode(color.id)}`}
                      onClick={() => cycleColorMode(color.id)}
                    >
                      <span style={{ backgroundColor: color.rgb_hex }} className="chip-swatch" />
                      <span className="chip-meta">
                        <strong>{color.name}</strong>
                        <small>
                          {color.ral_code} | {color.rgb_hex}
                        </small>
                      </span>
                    </button>
                  ))}
                </div>
              </div>
            </details>
            <p className="muted">
              Кнопка ниже запускает генерацию и обновляет превью с учетом текущих параметров.
            </p>
            <div className="auto-regenerate-panel">
              <label className="checkbox">
                <input
                  type="checkbox"
                  checked={liveRegenerateEnabled}
                  onChange={(event) => {
                    const nextValue = event.target.checked;
                    setLiveRegenerateEnabled(nextValue);
                    setLiveRegenerateError(null);
                    if (!nextValue) {
                      cancelLiveRegeneration();
                    }
                  }}
                />
                Автообновление мозаики при изменении параметров
              </label>
              <p className="muted">
                {liveRegenerateEnabled
                  ? hasGeneratedSnapshot
                    ? liveRegenerateRunning
                      ? "Идет автопересчет..."
                      : liveRegeneratePending
                        ? "Изменение обнаружено, пересчет начнется через 0.45 с."
                        : "Автообновление включено."
                    : "Сначала выполните первый расчет кнопкой «Сгенерировать мозаику»."
                  : "Автообновление выключено. Пересчет запускается только кнопкой."}
              </p>
              {liveRegenerateError && <p className="muted auto-regenerate-error">{liveRegenerateError}</p>}
            </div>

            <button
              type="button"
              className="button-primary"
              onClick={onGenerateMosaic}
              disabled={busy || !canUseStudio}
            >
              {busy ? "Генерация..." : "Сгенерировать мозаику"}
            </button>
            {!canUseStudio && (
              <p className="muted">
                Текущая роль: {roleLabel(currentRole)}. Генерация и экспорт доступны ролям Заказчик/Админ.
              </p>
            )}
          </article>

          <article className="panel preview-panel">
            <h2>Превью</h2>
            {previewSource && (
              <>
                <div className="preview-toolbar">
                  <label>
                    Zoom
                    <input
                      type="range"
                      min={0.4}
                      max={8}
                      step={0.01}
                      value={previewZoom}
                      onChange={(event) => onPreviewZoomChange(Number(event.target.value))}
                    />
                  </label>
                  <label>
                    %
                    <input
                      type="number"
                      min={40}
                      max={800}
                      step={1}
                      value={Math.round(previewZoom * 100)}
                      onChange={(event) =>
                        onPreviewZoomChange(Number(event.target.value) / 100)
                      }
                    />
                  </label>
                  {isStudioAdvancedMode && (
                    <label>
                      Шаг zoom
                      <input
                        type="text"
                        inputMode="decimal"
                        min={0.01}
                        max={2}
                        step={0.01}
                        value={previewZoomStep}
                        onChange={(event) => {
                          const parsed = parseNumberValue(event.target.value);
                          if (!Number.isFinite(parsed)) {
                            return;
                          }
                          setPreviewZoomStep(Math.max(0.01, parsed));
                        }}
                      />
                    </label>
                  )}
                  <button
                    type="button"
                    className="button-ghost"
                    onClick={() => onPreviewZoomChange(previewZoom - previewZoomStep)}
                    disabled={previewZoom <= 0.4}
                  >
                    -
                  </button>
                  <button
                    type="button"
                    className="button-ghost"
                    onClick={() => onPreviewZoomChange(previewZoom + previewZoomStep)}
                    disabled={previewZoom >= 8}
                  >
                    +
                  </button>
                  {isStudioAdvancedMode && (
                    <>
                      <label>
                        Pan шаг px
                        <input
                          type="number"
                          min={1}
                          step={1}
                          value={previewPanStepPx}
                          onChange={(event) =>
                            setPreviewPanStepPx(Math.max(1, Math.round(Number(event.target.value))))
                          }
                        />
                      </label>
                      <label>
                        Pan X
                        <input
                          type="number"
                          step={1}
                          value={previewPan.x}
                          onChange={(event) =>
                            setPreviewPan((current) => ({
                              ...current,
                              x: Number(event.target.value),
                            }))
                          }
                        />
                      </label>
                      <label>
                        Pan Y
                        <input
                          type="number"
                          step={1}
                          value={previewPan.y}
                          onChange={(event) =>
                            setPreviewPan((current) => ({
                              ...current,
                              y: Number(event.target.value),
                            }))
                          }
                        />
                      </label>
                      <button
                        type="button"
                        className="button-ghost"
                        onClick={() => nudgePreviewPan(0, -previewPanStepPx)}
                      >
                        Pan ↑
                      </button>
                      <button
                        type="button"
                        className="button-ghost"
                        onClick={() => nudgePreviewPan(-previewPanStepPx, 0)}
                      >
                        Pan ←
                      </button>
                      <button
                        type="button"
                        className="button-ghost"
                        onClick={() => nudgePreviewPan(previewPanStepPx, 0)}
                      >
                        Pan →
                      </button>
                      <button
                        type="button"
                        className="button-ghost"
                        onClick={() => nudgePreviewPan(0, previewPanStepPx)}
                      >
                        Pan ↓
                      </button>
                    </>
                  )}
                  <button type="button" className="button-secondary" onClick={resetPreviewTransform}>
                    Reset view
                  </button>
                </div>
                <div
                  className={previewDragging ? "preview-stage is-dragging" : "preview-stage"}
                  onWheel={onPreviewWheel}
                  onPointerDown={onPreviewPointerDown}
                  onPointerMove={onPreviewPointerMove}
                  onPointerUp={onPreviewPointerUp}
                  onPointerCancel={onPreviewPointerUp}
                  onPointerLeave={onPreviewPointerUp}
                >
                  <img
                    className="preview-image preview-image-transform"
                    src={previewSource}
                    alt={previewAlt}
                    style={{
                      transform: `translate(${previewPan.x}px, ${previewPan.y}px) scale(${previewZoom})`,
                    }}
                    draggable={false}
                  />
                </div>
                <p className="muted">
                  Drag to pan, use mouse wheel to zoom. Для смещения мозаики по мм используйте блок позиционирования и стрелки клавиатуры.
                </p>
              </>
            )}
            {mosaicResult && (
              <>
                <div className="stats-grid">
                  <div>
                    <span>Сетка</span>
                    <strong>
                      {mosaicResult.columns} x {mosaicResult.rows}
                    </strong>
                  </div>
                  <div>
                    <span>Размер мозаики</span>
                    <strong>
                      {mosaicResult.mosaic_width_mm.toFixed(0)} x{" "}
                      {mosaicResult.mosaic_height_mm.toFixed(0)} мм
                    </strong>
                  </div>
                  <div>
                    <span>Использовано цветов</span>
                    <strong>{mosaicResult.actual_colors_used}</strong>
                  </div>
                  <div>
                    <span>Всего чипов</span>
                    <strong>{mosaicResult.total_chips}</strong>
                  </div>
                  <div>
                    <span>Цена</span>
                    <strong>{formatMoney(mosaicResult.price.total_price, mosaicResult.price.currency)}</strong>
                  </div>
                </div>

                <h3>Использованные цвета</h3>
                <div className="usage-list">
                  {mosaicResult.used_colors.map((item) => (
                    <div key={item.id} className="usage-row">
                      <div className="usage-head">
                        <span className="swatch" style={{ backgroundColor: item.rgb_hex }} />
                        <span>
                          {item.name} ({item.ral_code})
                        </span>
                      </div>
                      <div className="usage-values">
                        <span>{item.cells} ячеек</span>
                        <span>{(item.ratio * 100).toFixed(1)}%</span>
                      </div>
                    </div>
                  ))}
                </div>

                <details className="inline-editor fold-card" open={isStudioAdvancedMode}>
                  <summary>Расчет цены</summary>
                  <div className="fold-content">
                    <div className="usage-list">
                      <div className="usage-row">
                        <div>База</div>
                        <div className="usage-values">
                          {formatMoney(mosaicResult.price.setup_price, mosaicResult.price.currency)}
                        </div>
                      </div>
                      <div className="usage-row">
                        <div>Чипы ({mosaicResult.price.total_chips})</div>
                        <div className="usage-values">
                          {formatMoney(mosaicResult.price.chips_price, mosaicResult.price.currency)}
                        </div>
                      </div>
                      <div className="usage-row">
                        <div>Цвета ({mosaicResult.actual_colors_used})</div>
                        <div className="usage-values">
                          {formatMoney(mosaicResult.price.colors_price, mosaicResult.price.currency)}
                        </div>
                      </div>
                      <div className="usage-row">
                        <div>Сложность (сверх порога цветов)</div>
                        <div className="usage-values">
                          {formatMoney(mosaicResult.price.complexity_price, mosaicResult.price.currency)}
                        </div>
                      </div>
                      <div className="usage-row">
                        <div>Затирка ({mosaicResult.price.area_sq_m.toFixed(2)} м²)</div>
                        <div className="usage-values">
                          {formatMoney(mosaicResult.price.grout_price, mosaicResult.price.currency)}
                        </div>
                      </div>
                      <div className="usage-row">
                        <div>Промежуточный итог</div>
                        <div className="usage-values">
                          {formatMoney(mosaicResult.price.subtotal_price, mosaicResult.price.currency)}
                        </div>
                      </div>
                      <div className="usage-row">
                        <div>Минимальный заказ</div>
                        <div className="usage-values">
                          {formatMoney(mosaicResult.price.min_order_price, mosaicResult.price.currency)}
                        </div>
                      </div>
                      <div className="usage-row">
                        <div>
                          <strong>Итого</strong>
                          {mosaicResult.price.min_order_applied ? " (с учетом минимального заказа)" : ""}
                        </div>
                        <div className="usage-values">
                          <strong>
                            {formatMoney(mosaicResult.price.total_price, mosaicResult.price.currency)}
                          </strong>
                        </div>
                      </div>
                    </div>
                  </div>
                </details>

                <details className="inline-editor fold-card" open={isStudioAdvancedMode}>
                  <summary>Замена цвета</summary>
                  <div className="fold-content">
                    <div className="form-grid two-col">
                      <label>
                        Исходный цвет
                        <select
                          value={replaceFromColorId ?? ""}
                          onChange={(event) =>
                            setReplaceFromColorId(
                              event.target.value.length > 0 ? Number(event.target.value) : null,
                            )
                          }
                        >
                          <option value="">Выберите цвет</option>
                          {mosaicResult.used_colors.map((item) => (
                            <option key={item.id} value={item.id}>
                              {item.name} ({item.ral_code})
                            </option>
                          ))}
                        </select>
                      </label>
                      <label>
                        Целевой цвет
                        <select
                          value={replaceToColorId ?? ""}
                          onChange={(event) =>
                            setReplaceToColorId(
                              event.target.value.length > 0 ? Number(event.target.value) : null,
                            )
                          }
                        >
                          <option value="">Выберите цвет</option>
                          {activePalette.map((item) => (
                            <option key={item.id} value={item.id}>
                              {item.name} ({item.ral_code})
                            </option>
                          ))}
                        </select>
                      </label>
                    </div>
                    <div className="actions">
                      <button
                        type="button"
                        className="button-secondary"
                        onClick={onReplaceMosaicColor}
                        disabled={
                          busy ||
                          !canUseStudio ||
                          replaceFromColorId === null ||
                          replaceToColorId === null
                        }
                      >
                        Заменить во всей мозаике
                      </button>
                    </div>
                  </div>
                </details>

                <details className="inline-editor fold-card" open>
                  <summary>Экспорт и печать</summary>
                  <div className="fold-content">
                    <div className="form-grid two-col">
                      <label>
                        <LabelTitle
                          text="DPI"
                          hint="Разрешение для PNG/JPEG/PDF. Для черновой проверки 120-150, для печати обычно 200-300."
                        />
                        <input
                          type="number"
                          min={72}
                          max={600}
                          value={exportDpi}
                          onChange={(event) => setExportDpi(Number(event.target.value))}
                        />
                      </label>
                      {isStudioAdvancedMode && (
                        <>
                          <label>
                            <LabelTitle
                              text="Размер плитки (чипов по X)"
                              hint="Сколько чипов по горизонтали входит в одну монтажную плитку."
                            />
                            <input
                              type="number"
                              min={1}
                              max={256}
                              value={moduleChipColumns}
                              onChange={(event) => setModuleChipColumns(Number(event.target.value))}
                            />
                          </label>
                          <label>
                            <LabelTitle
                              text="Размер плитки (чипов по Y)"
                              hint="Сколько чипов по вертикали входит в одну монтажную плитку."
                            />
                            <input
                              type="number"
                              min={1}
                              max={256}
                              value={moduleChipRows}
                              onChange={(event) => setModuleChipRows(Number(event.target.value))}
                            />
                          </label>
                          <label>
                            <LabelTitle
                              text="Стартовый номер плитки"
                              hint="Нумерация модулей идет снизу-слева по горизонтали, начиная с этого номера."
                            />
                            <input
                              type="number"
                              min={1}
                              value={moduleStartNumber}
                              onChange={(event) => setModuleStartNumber(Number(event.target.value))}
                            />
                          </label>
                        </>
                      )}
                      <label className="checkbox">
                        <input
                          type="checkbox"
                          checked={exportMirrorHorizontal}
                          onChange={(event) => setExportMirrorHorizontal(event.target.checked)}
                        />
                        Зеркалить по горизонтали
                      </label>
                      <label className="checkbox">
                        <input
                          type="checkbox"
                          checked={exportIncludeLegend}
                          onChange={(event) => setExportIncludeLegend(event.target.checked)}
                        />
                        Добавлять легенду в PDF
                      </label>
                      <label className="checkbox">
                        <input
                          type="checkbox"
                          checked={exportIncludeColorNumbers}
                          onChange={(event) => setExportIncludeColorNumbers(event.target.checked)}
                        />
                        Печатать номера цветов в ячейках
                      </label>
                    </div>
                    {isStudioAdvancedMode && moduleEstimate && (
                      <div className="stats-grid">
                        <div>
                          <span>Плиток по X</span>
                          <strong>{moduleEstimate.modulesX}</strong>
                        </div>
                        <div>
                          <span>Плиток по Y</span>
                          <strong>{moduleEstimate.modulesY}</strong>
                        </div>
                        <div>
                          <span>Всего плиток</span>
                          <strong>{moduleEstimate.modulesTotal}</strong>
                        </div>
                        <div>
                          <span>Листов PDF-комплекта</span>
                          <strong>{moduleEstimate.pagesTotal}</strong>
                        </div>
                      </div>
                    )}
                    <div className="actions export-actions">
                      <button
                        type="button"
                        className="button-secondary"
                        onClick={() => void onExportMosaic("png")}
                        disabled={busy || !canUseStudio}
                      >
                        Скачать PNG
                      </button>
                      <button
                        type="button"
                        className="button-secondary"
                        onClick={() => void onExportMosaic("jpeg")}
                        disabled={busy || !canUseStudio}
                      >
                        Скачать JPEG
                      </button>
                      <button
                        type="button"
                        className="button-secondary"
                        onClick={() => void onExportMosaic("svg")}
                        disabled={busy || !canUseStudio}
                      >
                        Скачать SVG
                      </button>
                      <button
                        type="button"
                        className="button-primary"
                        onClick={() => void onExportMosaic("pdf")}
                        disabled={busy || !canUseStudio}
                      >
                        Скачать PDF
                      </button>
                      <button
                        type="button"
                        className="button-ghost"
                        onClick={() => void onExportMosaic("materials-csv")}
                        disabled={busy || !canUseStudio}
                      >
                        Ведомость CSV
                      </button>
                      <button
                        type="button"
                        className="button-ghost"
                        onClick={() => void onExportMosaic("grid-csv")}
                        disabled={busy || !canUseStudio}
                      >
                        Координаты CSV
                      </button>
                      <button
                        type="button"
                        className="button-ghost"
                        onClick={() => void onExportMosaic("modules-csv")}
                        disabled={busy || !canUseStudio}
                      >
                        Плитки CSV
                      </button>
                      <button
                        type="button"
                        className="button-primary"
                        onClick={() => void onExportMosaic("assembly-kit-pdf")}
                        disabled={busy || !canUseStudio}
                      >
                        Комплект укладчика PDF
                      </button>
                    </div>
                    <p className="muted">
                      Форматы экспорта: PNG/JPEG/SVG/PDF, CSV по материалам/координатам/плиткам и PDF-комплект укладчика.
                      Нумерация плиток в комплекте: снизу-слева, слева-направо. Опция зеркалирования нужна для печати схемы «лицом вниз».
                    </p>
                  </div>
                </details>
              </>
            )}
            {!previewSource && (
              <div className="placeholder">Загрузите изображение и запустите генерацию.</div>
            )}
          </article>
        </section>
      )}

      {activeTab === "projects" && (
        <section className="workspace">
          <article className="panel">
            <h2>Сохранение проекта</h2>
            {!isAuthenticated && (
              <div className="placeholder">
                Войдите в систему, чтобы сохранять проекты, версионировать генерации и отправлять предзаказы.
              </div>
            )}
            <p className="muted">
              Проект хранит состояние мозаики, параметры генерации, ограничения палитры и положение превью.
            </p>
            <div className="form-grid">
              <label>
                <LabelTitle
                  text="Название проекта"
                  hint="Читаемое название для списка проектов."
                />
                <input
                  type="text"
                  value={projectNameDraft}
                  onChange={(event) => setProjectNameDraft(event.target.value)}
                  placeholder="Например: Стена гостиной №1"
                />
              </label>
              <label>
                <LabelTitle
                  text="Описание проекта"
                  hint="Технический комментарий или заметки по заказу."
                />
                <textarea
                  rows={3}
                  value={projectDescriptionDraft}
                  onChange={(event) => setProjectDescriptionDraft(event.target.value)}
                  placeholder="Размер стены, требования к сборке и др."
                />
              </label>
              <label>
                <LabelTitle
                  text="Название версии"
                  hint="Как назвать сохраняемую генерацию (если пусто, система подставит имя автоматически)."
                />
                <input
                  type="text"
                  value={generationNameDraft}
                  onChange={(event) => setGenerationNameDraft(event.target.value)}
                  placeholder="Например: 20 цветов, теплая затирка"
                />
              </label>
              <label>
                <LabelTitle
                  text="Комментарий к версии"
                  hint="Что изменилось в этой версии по сравнению с прошлой."
                />
                <textarea
                  rows={3}
                  value={generationNoteDraft}
                  onChange={(event) => setGenerationNoteDraft(event.target.value)}
                  placeholder="Например: Заменили черный на темно-коричневый"
                />
              </label>
              <div className="actions">
                <button
                  type="button"
                  className="button-primary"
                  onClick={onCreateProject}
                  disabled={busy || !mosaicResult || !canEditProjects}
                >
                  Создать проект
                </button>
                <button
                  type="button"
                  className="button-secondary"
                  onClick={onSaveGenerationToProject}
                  disabled={busy || !mosaicResult || selectedProjectId === null || !canEditProjects}
                >
                  Сохранить версию в выбранный проект
                </button>
              </div>
              {!mosaicResult && (
                <p className="muted">
                  Сейчас нет сгенерированной мозаики. Откройте вкладку «Генерация», чтобы получить сохраняемую версию.
                </p>
              )}
            </div>
          </article>

          <article className="panel">
            <h2>Список проектов</h2>
            <div className="actions">
              <button
                type="button"
                className="button-ghost"
                onClick={() => void refreshProjects()}
                disabled={busy || loadingProjects || !isAuthenticated}
              >
                {loadingProjects ? "Обновление..." : "Обновить список"}
              </button>
            </div>

            {projects.length === 0 && (
              <div className="placeholder">Пока нет сохраненных проектов.</div>
            )}

            {projects.length > 0 && (
              <div className="usage-list project-list">
                <div className="muted" style={{ marginBottom: "0.5rem" }}>
                  Всего: {projectsTotal}
                </div>
                {projects.map((project) => (
                  <div
                    key={project.id}
                    className={
                      project.id === selectedProjectId
                        ? "usage-row project-row project-row-active"
                        : "usage-row project-row"
                    }
                  >
                    <div>
                      <strong>{project.name}</strong>
                      <div className="muted">
                        Версий: {project.generations_count} | Обновлен: {formatDateTime(project.updated_at)}
                      </div>
                    </div>
                    <div className="row-actions">
                      <button
                        type="button"
                        className="button-ghost"
                        onClick={() => void onOpenProject(project.id)}
                        disabled={busy}
                      >
                        Открыть
                      </button>
                    </div>
                  </div>
                ))}
              </div>
            )}

            {selectedProject && (
              <div className="inline-editor">
                <h3>Проект #{selectedProject.id}</h3>
                <p className="muted">
                  Создан: {formatDateTime(selectedProject.created_at)} | Активная версия:{" "}
                  {selectedProject.active_generation_id ?? "-"}
                </p>
                <div className="actions">
                  <button
                    type="button"
                    className="button-secondary"
                    onClick={onUpdateProjectMeta}
                    disabled={busy || !canEditProjects}
                  >
                    Обновить название/описание
                  </button>
                </div>

                <h3>История генераций</h3>
                <div className="usage-list">
                  {selectedProject.generations.map((generation) => (
                    <div key={generation.id} className="usage-row">
                      <div>
                        <strong>
                          v{generation.version}: {generation.name}
                        </strong>
                        <div className="muted">
                          {formatDateTime(generation.created_at)}
                          {generation.note ? ` | ${generation.note}` : ""}
                        </div>
                      </div>
                      <div className="row-actions">
                        <button
                          type="button"
                          className="button-ghost"
                          onClick={() =>
                            void onActivateProjectGeneration(selectedProject.id, generation.id)
                          }
                          disabled={busy}
                        >
                          Загрузить в студию
                        </button>
                      </div>
                    </div>
                  ))}
                </div>

                <div className="inline-editor">
                  <h3>Shared-ссылки</h3>
                  <p className="muted">
                    Ссылка открывает выбранную версию проекта без авторизации. Можно задать срок действия и отозвать ссылку.
                  </p>
                  <div className="form-grid two-col">
                    <label>
                      Версия для ссылки
                      <select
                        value={shareGenerationId ?? ""}
                        onChange={(event) =>
                          setShareGenerationId(
                            event.target.value.length > 0 ? Number(event.target.value) : null,
                          )
                        }
                      >
                        <option value="">Активная версия</option>
                        {selectedProject.generations.map((generation) => (
                          <option key={generation.id} value={generation.id}>
                            v{generation.version}: {generation.name}
                          </option>
                        ))}
                      </select>
                    </label>
                    <label>
                      Срок действия (дней)
                      <input
                        type="number"
                        min={1}
                        max={3650}
                        value={shareExpiresInDays}
                        onChange={(event) => setShareExpiresInDays(Number(event.target.value))}
                      />
                    </label>
                  </div>
                  <div className="actions">
                    <button
                      type="button"
                      className="button-secondary"
                      onClick={() => void onCreateProjectShare()}
                      disabled={busy || !canEditProjects}
                    >
                      Создать shared-ссылку
                    </button>
                  </div>
                  {projectShares.length === 0 ? (
                    <div className="placeholder">Пока нет созданных shared-ссылок.</div>
                  ) : (
                    <div className="usage-list">
                      {projectShares.map((share) => (
                        <div key={share.id} className="usage-row">
                          <div>
                            <strong>
                              #{share.id} | v{share.generation_version}: {share.generation_name}
                            </strong>
                            <div className="muted">
                              Создана: {formatDateTime(share.created_at)} | Истекает:{" "}
                              {formatDateTime(share.expires_at)}
                              {share.is_revoked ? " | Отозвана" : ""}
                            </div>
                          </div>
                          <div className="row-actions">
                            <button
                              type="button"
                              className="button-ghost"
                              onClick={() => void onCopyShareUrl(share.token)}
                            >
                              Копировать ссылку
                            </button>
                            {!share.is_revoked && (
                              <button
                                type="button"
                                className="button-ghost danger"
                                onClick={() => void onRevokeShare(share.id)}
                                disabled={busy || !canEditProjects}
                              >
                                Отозвать
                              </button>
                            )}
                          </div>
                        </div>
                      ))}
                    </div>
                  )}
                </div>

                <div className="inline-editor">
                  <h3>Предварительный заказ</h3>
                  <p className="muted">
                    Отправка на согласование с фиксацией версии, цены и состава мозаики.
                  </p>
                  <div className="form-grid two-col">
                    <label>
                      Версия проекта
                      <select
                        value={orderForm.generation_id ?? ""}
                        onChange={(event) =>
                          setOrderForm((current) => ({
                            ...current,
                            generation_id: event.target.value.length > 0 ? Number(event.target.value) : null,
                          }))
                        }
                      >
                        <option value="">Активная версия</option>
                        {selectedProject.generations.map((generation) => (
                          <option key={generation.id} value={generation.id}>
                            v{generation.version}: {generation.name}
                          </option>
                        ))}
                      </select>
                    </label>
                    <label>
                      Контактное лицо
                      <input
                        type="text"
                        value={orderForm.customer_name}
                        onChange={(event) =>
                          setOrderForm((current) => ({ ...current, customer_name: event.target.value }))
                        }
                        placeholder="Имя и фамилия"
                      />
                    </label>
                    <label>
                      E-mail
                      <input
                        type="email"
                        value={orderForm.customer_email}
                        onChange={(event) =>
                          setOrderForm((current) => ({ ...current, customer_email: event.target.value }))
                        }
                        placeholder="client@example.com"
                      />
                    </label>
                    <label>
                      Телефон
                      <input
                        type="text"
                        value={orderForm.customer_phone}
                        onChange={(event) =>
                          setOrderForm((current) => ({ ...current, customer_phone: event.target.value }))
                        }
                        placeholder="+7 ..."
                      />
                    </label>
                    <label>
                      Комментарий
                      <textarea
                        rows={2}
                        value={orderForm.comment}
                        onChange={(event) =>
                          setOrderForm((current) => ({ ...current, comment: event.target.value }))
                        }
                        placeholder="Дополнительные пожелания"
                      />
                    </label>
                  </div>
                  <div className="actions">
                    <button
                      type="button"
                      className="button-primary"
                      onClick={() => void onCreateProjectOrder()}
                      disabled={busy || !canEditProjects}
                    >
                      Отправить на согласование
                    </button>
                  </div>
                  {projectOrders.length === 0 ? (
                    <div className="placeholder">Предварительные заказы еще не отправлялись.</div>
                  ) : (
                    <div className="usage-list">
                      {projectOrders.map((order) => (
                        <div key={order.id} className="usage-row">
                          <div>
                            <strong>
                              Заказ #{order.id} | {formatMoney(order.total_price, order.currency)}
                            </strong>
                            <div className="muted">
                              Версия v{order.generation_version}: {order.generation_name} | {order.total_chips} чипов |{" "}
                              {order.colors_used} цветов
                            </div>
                            <div className="muted">
                              {formatDateTime(order.created_at)} | {order.customer_name}
                              {order.customer_phone ? ` | ${order.customer_phone}` : ""}
                              {order.customer_email ? ` | ${order.customer_email}` : ""}
                            </div>
                            {order.comment && <div className="muted">Комментарий: {order.comment}</div>}
                            {order.status_comment && (
                              <div className="muted">Комментарий статуса: {order.status_comment}</div>
                            )}
                          </div>
                          <div className="row-actions order-actions">
                            <span className="status-pill">{orderStatusLabel(order.status)}</span>
                            {isAdmin && (
                              <>
                                <select
                                  value={orderStatusDraftByOrder[order.id] ?? order.status}
                                  onChange={(event) =>
                                    setOrderStatusDraftByOrder((current) => ({
                                      ...current,
                                      [order.id]: event.target.value as ProjectOrderStatus,
                                    }))
                                  }
                                >
                                  {ORDER_STATUS_OPTIONS.map((option) => (
                                    <option key={option.value} value={option.value}>
                                      {option.label}
                                    </option>
                                  ))}
                                </select>
                                <input
                                  type="text"
                                  value={orderStatusCommentByOrder[order.id] ?? ""}
                                  onChange={(event) =>
                                    setOrderStatusCommentByOrder((current) => ({
                                      ...current,
                                      [order.id]: event.target.value,
                                    }))
                                  }
                                  placeholder="Комментарий статуса"
                                />
                                <button
                                  type="button"
                                  className="button-secondary"
                                  onClick={() => void onUpdateOrderStatus(order.id)}
                                  disabled={busy}
                                >
                                  Обновить
                                </button>
                              </>
                            )}
                          </div>
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              </div>
            )}
          </article>
        </section>
      )}

      {activeTab === "palette" && (
        <section className="workspace">
          <article className="panel">
            <h2>Добавить цвет палитры</h2>
            <form onSubmit={onCreateColor} className="form-grid two-col">
              <label>
                <LabelTitle
                  text="Название"
                  hint="Человеко-понятное название цвета. Можно выбрать из подсказок."
                />
                <input
                  type="text"
                  list="ru-color-names"
                  value={newColorForm.name}
                  onChange={(event) => {
                    setNewColorTouched((current) => ({ ...current, name: true }));
                    setNewColorForm((current) => ({ ...current, name: event.target.value }));
                  }}
                  required
                />
              </label>
              <label>
                <LabelTitle
                  text="RAL"
                  hint="Код цвета по стандарту RAL. Можно выбрать из подсказок."
                />
                <input
                  type="text"
                  list="ral-codes"
                  value={newColorForm.ral_code}
                  onChange={(event) => {
                    setNewColorTouched((current) => ({ ...current, ral: true }));
                    setNewColorForm((current) => ({ ...current, ral_code: event.target.value }));
                  }}
                  required
                />
              </label>
              <label>
                <LabelTitle
                  text="HEX"
                  hint="Цвет для экрана в формате #RRGGBB. При выборе HEX название и RAL подставляются автоматически."
                />
                <div className="hex-input">
                  <input
                    type="text"
                    value={newColorForm.rgb_hex}
                    onChange={(event) => applyNewColorHex(event.target.value)}
                    onBlur={() => applyNewColorHex(newColorForm.rgb_hex)}
                    required
                  />
                  <input
                    type="color"
                    className="color-picker"
                    aria-label="Выбор HEX цвета"
                    value={pickerHexValue(newColorForm.rgb_hex)}
                    onChange={(event) => applyNewColorHex(event.target.value)}
                  />
                </div>
              </label>
              <div className="actions">
                <button type="submit" className="button-primary" disabled={busy}>
                  Добавить
                </button>
              </div>
            </form>
            <p className="muted">
              При выборе HEX название и RAL подставляются автоматически. Их можно изменить вручную.
            </p>

            <h3>Пакетный импорт</h3>
            <p className="muted">Формат: одна строка = название;RAL;#HEX</p>
            <textarea
              rows={5}
              value={bulkPaletteText}
              onChange={(event) => setBulkPaletteText(event.target.value)}
              placeholder="Синий;RAL 5015;#2B7FFF"
            />
            <button type="button" className="button-secondary" onClick={onBulkImportColors} disabled={busy}>
              Импортировать список
            </button>
          </article>

          <article className="panel">
            <h2>Список цветов</h2>
            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>Цвет</th>
                    <th>RAL</th>
                    <th>HEX</th>
                    <th>Статус</th>
                    <th>Действия</th>
                  </tr>
                </thead>
                <tbody>
                  {(bootstrap?.colors ?? []).map((color) => (
                    <tr key={color.id}>
                      <td>
                        <div className="row-color">
                          <span className="swatch" style={{ backgroundColor: color.rgb_hex }} />
                          {color.name}
                        </div>
                      </td>
                      <td>{color.ral_code}</td>
                      <td>{color.rgb_hex}</td>
                      <td>{color.is_active ? "Активен" : "Неактивен"}</td>
                      <td className="row-actions">
                        <button type="button" className="button-ghost" onClick={() => onStartEditColor(color)}>
                          Изменить
                        </button>
                        {color.is_active && (
                          <button
                            type="button"
                            className="button-ghost danger"
                            onClick={() => onDeactivateColor(color.id)}
                          >
                            Деактивировать
                          </button>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {editingColorId !== null && editingColorForm && (
              <div className="inline-editor">
                <h3>Редактирование цвета #{editingColorId}</h3>
                <div className="form-grid two-col">
                  <label>
                    Название
                    <input
                      type="text"
                      list="ru-color-names"
                      value={editingColorForm.name}
                      onChange={(event) => {
                        setEditingColorTouched((current) => ({ ...current, name: true }));
                        setEditingColorForm((current) =>
                          current ? { ...current, name: event.target.value } : current,
                        );
                      }}
                    />
                  </label>
                  <label>
                    RAL
                    <input
                      type="text"
                      list="ral-codes"
                      value={editingColorForm.ral_code}
                      onChange={(event) => {
                        setEditingColorTouched((current) => ({ ...current, ral: true }));
                        setEditingColorForm((current) =>
                          current ? { ...current, ral_code: event.target.value } : current,
                        );
                      }}
                    />
                  </label>
                  <label>
                    HEX
                    <div className="hex-input">
                      <input
                        type="text"
                        value={editingColorForm.rgb_hex}
                        onChange={(event) => applyEditingColorHex(event.target.value)}
                        onBlur={() => applyEditingColorHex(editingColorForm.rgb_hex)}
                      />
                      <input
                        type="color"
                        className="color-picker"
                        aria-label="Выбор HEX цвета"
                        value={pickerHexValue(editingColorForm.rgb_hex)}
                        onChange={(event) => applyEditingColorHex(event.target.value)}
                      />
                    </div>
                  </label>
                  <label className="checkbox">
                    <input
                      type="checkbox"
                      checked={Boolean(editingColorForm.is_active)}
                      onChange={(event) =>
                        setEditingColorForm((current) =>
                          current ? { ...current, is_active: event.target.checked } : current,
                        )
                      }
                    />
                    Активен
                  </label>
                </div>
                <div className="actions">
                  <button type="button" className="button-primary" onClick={onSaveColorEdit} disabled={busy}>
                    Сохранить
                  </button>
                  <button
                    type="button"
                    className="button-secondary"
                    onClick={() => {
                      setEditingColorId(null);
                      setEditingColorForm(null);
                      setEditingColorTouched({ name: false, ral: false });
                    }}
                  >
                    Отмена
                  </button>
                </div>
              </div>
            )}
          </article>
        </section>
      )}

      {activeTab === "settings" && (
        <section className="workspace">
          <article className="panel">
            <h2>Значения по умолчанию</h2>
            <form onSubmit={onSaveSettings} className="form-grid two-col">
              <label>
                <LabelTitle
                  text="Ширина поля по умолчанию (мм)"
                  hint="Это значение подставляется в форму генерации для новых сессий."
                />
                <input
                  type="number"
                  min={1}
                  value={settingsDraft.default_field_width_mm}
                  onChange={(event) =>
                    setSettingsDraft((current) => ({
                      ...current,
                      default_field_width_mm: Number(event.target.value),
                    }))
                  }
                />
              </label>
              <label>
                <LabelTitle
                  text="Высота поля по умолчанию (мм)"
                  hint="Это значение подставляется в форму генерации для новых сессий."
                />
                <input
                  type="number"
                  min={1}
                  value={settingsDraft.default_field_height_mm}
                  onChange={(event) =>
                    setSettingsDraft((current) => ({
                      ...current,
                      default_field_height_mm: Number(event.target.value),
                    }))
                  }
                />
              </label>
              <label>
                <LabelTitle
                  text="Размер ячейки по умолчанию (мм)"
                  hint="Базовый размер плитки для первой генерации."
                />
                <input
                  type="text"
                  inputMode="decimal"
                  min={1}
                  value={settingsDraft.default_cell_size_mm}
                  onChange={(event) => {
                    const parsed = parseNumberValue(event.target.value);
                    if (!Number.isFinite(parsed)) {
                      return;
                    }
                    setSettingsDraft((current) => ({
                      ...current,
                      default_cell_size_mm: parsed,
                    }));
                  }}
                />
              </label>
              <label>
                <LabelTitle
                  text="Расстояние по умолчанию (мм)"
                  hint="Базовая ширина шва между плитками."
                />
                <input
                  type="text"
                  inputMode="decimal"
                  min={0}
                  value={settingsDraft.default_gap_mm}
                  onChange={(event) => {
                    const parsed = parseNumberValue(event.target.value);
                    if (!Number.isFinite(parsed)) {
                      return;
                    }
                    setSettingsDraft((current) => ({
                      ...current,
                      default_gap_mm: parsed,
                    }));
                  }}
                />
              </label>
              <div className="actions">
                <button type="submit" className="button-primary" disabled={busy}>
                  Сохранить
                </button>
              </div>
            </form>
            <h3>База данных</h3>
            <p className="muted">
              Выберите провайдер и строку подключения. Кнопка ниже может создать таблицы и базовую структуру.
            </p>
            <form onSubmit={onSaveDatabaseConfig} className="form-grid">
              <label>
                <LabelTitle
                  text="Провайдер БД"
                  hint="Доступные варианты: sqlite, json, postgres, sqlserver, mysql."
                />
                <select
                  value={dbDraft.provider}
                  onChange={(event) => onDbProviderChange(event.target.value)}
                >
                  {supportedDbProviders.map((provider) => (
                    <option key={provider} value={provider}>
                      {provider}
                    </option>
                  ))}
                </select>
              </label>
              {dbDraft.provider === "sqlite" || dbDraft.provider === "json" ? (
                <label>
                  <LabelTitle
                    text="Файл базы данных"
                    hint="Путь к файлу данных на сервере. Пример: C:\\folder\\mozaika.storage.json или C:\\folder\\mozaika.db."
                  />
                  <input
                    type="text"
                    value={dbConnectionForm.dataSource}
                    onChange={(event) => setDbConnectionField("dataSource", event.target.value)}
                  />
                </label>
              ) : (
                <>
                  <label>
                    <LabelTitle text="Хост" hint="Домен или IP сервера БД." />
                    <input
                      type="text"
                      value={dbConnectionForm.host}
                      onChange={(event) => setDbConnectionField("host", event.target.value)}
                    />
                  </label>
                  <label>
                    <LabelTitle text="Порт" hint="Порт БД: PostgreSQL 5432, MySQL 3306, SQL Server 1433." />
                    <input
                      type="text"
                      value={dbConnectionForm.port}
                      onChange={(event) => setDbConnectionField("port", event.target.value)}
                    />
                  </label>
                  <label>
                    <LabelTitle text="Имя базы" hint="Название базы данных на сервере." />
                    <input
                      type="text"
                      value={dbConnectionForm.database}
                      onChange={(event) => setDbConnectionField("database", event.target.value)}
                    />
                  </label>
                  <label>
                    <LabelTitle text="Пользователь" hint="Логин пользователя БД." />
                    <input
                      type="text"
                      value={dbConnectionForm.username}
                      onChange={(event) => setDbConnectionField("username", event.target.value)}
                    />
                  </label>
                  <label>
                    <LabelTitle text="Пароль" hint="Пароль пользователя БД." />
                    <input
                      type="password"
                      value={dbConnectionForm.password}
                      onChange={(event) => setDbConnectionField("password", event.target.value)}
                    />
                  </label>
                  {(dbDraft.provider === "postgres" || dbDraft.provider === "mysql") && (
                    <label>
                      <LabelTitle text="Режим SSL" hint="Например: Require, Prefer или Disable (зависит от хостинга)." />
                      <input
                        type="text"
                        value={dbConnectionForm.sslMode}
                        onChange={(event) => setDbConnectionField("sslMode", event.target.value)}
                      />
                    </label>
                  )}
                  {(dbDraft.provider === "postgres" || dbDraft.provider === "sqlserver") && (
                    <label className="checkbox">
                      <input
                        type="checkbox"
                        checked={dbConnectionForm.trustServerCertificate}
                        onChange={(event) =>
                          setDbConnectionField("trustServerCertificate", event.target.checked)
                        }
                      />
                      Доверять сертификату сервера
                    </label>
                  )}
                  {dbDraft.provider === "sqlserver" && (
                    <label className="checkbox">
                      <input
                        type="checkbox"
                        checked={dbConnectionForm.encrypt}
                        onChange={(event) => setDbConnectionField("encrypt", event.target.checked)}
                      />
                      Шифровать соединение
                    </label>
                  )}
                </>
              )}
              <label>
                <LabelTitle
                  text="Доп. параметры"
                  hint="Дополнительные пары вида Key=Value;Key2=Value2. Добавятся в конец строки."
                />
                <textarea
                  rows={2}
                  value={dbConnectionForm.extra}
                  onChange={(event) => setDbConnectionField("extra", event.target.value)}
                />
              </label>
              <label>
                <LabelTitle
                  text="Строка подключения"
                  hint="Используется для подключения к выбранной базе данных."
                />
                <textarea
                  rows={3}
                  value={composedConnectionString}
                  readOnly
                />
              </label>
              <p className="muted">{providerConnectionHint(dbDraft.provider)}</p>
              <label className="checkbox">
                <input
                  type="checkbox"
                  checked={Boolean(dbDraft.echo)}
                  onChange={(event) =>
                    setDbDraft((current) => ({
                      ...current,
                      echo: event.target.checked,
                    }))
                  }
                />
                Включить SQL-лог (echo)
              </label>
              <label className="checkbox">
                <input
                  type="checkbox"
                  checked={Boolean(dbDraft.create_schema)}
                  onChange={(event) =>
                    setDbDraft((current) => ({
                      ...current,
                      create_schema: event.target.checked,
                    }))
                  }
                />
                Создать таблицы и структуру
              </label>
              <label className="checkbox">
                <input
                  type="checkbox"
                  checked={Boolean(dbDraft.seed_defaults)}
                  disabled={!dbDraft.create_schema}
                  onChange={(event) =>
                    setDbDraft((current) => ({
                      ...current,
                      seed_defaults: event.target.checked,
                    }))
                  }
                />
                Заполнить базовые данные (настройки и цвета шва)
              </label>
              <div className="actions">
                <button type="button" className="button-ghost" disabled={busy} onClick={onTestDatabaseConfig}>
                  Проверить подключение
                </button>
                <button type="submit" className="button-secondary" disabled={busy}>
                  Применить подключение БД
                </button>
              </div>
            </form>
          </article>

          <article className="panel">
            <h2>Цвета заполнения</h2>
            <form onSubmit={onCreateGroutColor} className="form-grid two-col">
              <label>
                <LabelTitle
                  text="Название"
                  hint="Название цвета шва. Можно выбрать из подсказок."
                />
                <input
                  type="text"
                  list="ru-color-names"
                  value={newGroutForm.name}
                  onChange={(event) => {
                    setNewGroutNameTouched(true);
                    setNewGroutForm((current) => ({ ...current, name: event.target.value }));
                  }}
                  required
                />
              </label>
              <label>
                <LabelTitle
                  text="HEX"
                  hint="Цвет шва в формате #RRGGBB. Название подставляется автоматически."
                />
                <div className="hex-input">
                  <input
                    type="text"
                    value={newGroutForm.rgb_hex}
                    onChange={(event) => applyNewGroutHex(event.target.value)}
                    onBlur={() => applyNewGroutHex(newGroutForm.rgb_hex)}
                    required
                  />
                  <input
                    type="color"
                    className="color-picker"
                    aria-label="Выбор HEX цвета заполнения"
                    value={pickerHexValue(newGroutForm.rgb_hex)}
                    onChange={(event) => applyNewGroutHex(event.target.value)}
                  />
                </div>
              </label>
              <div className="actions">
                <button type="submit" className="button-primary" disabled={busy}>
                  Добавить цвет
                </button>
              </div>
            </form>
            <p className="muted">Название цвета заполнения также подставляется автоматически по HEX.</p>

            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>Название</th>
                    <th>HEX</th>
                    <th>Статус</th>
                    <th>Действия</th>
                  </tr>
                </thead>
                <tbody>
                  {(bootstrap?.grout_colors ?? []).map((color) => (
                    <tr key={color.id}>
                      <td>{color.name}</td>
                      <td>
                        <div className="row-color">
                          <span className="swatch" style={{ backgroundColor: color.rgb_hex }} />
                          {color.rgb_hex}
                        </div>
                      </td>
                      <td>{color.is_active ? "Активен" : "Неактивен"}</td>
                      <td className="row-actions">
                        <button type="button" className="button-ghost" onClick={() => onStartEditGrout(color)}>
                          Изменить
                        </button>
                        {color.is_active && (
                          <button
                            type="button"
                            className="button-ghost danger"
                            onClick={() => onDeactivateGrout(color.id)}
                          >
                            Деактивировать
                          </button>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {editingGroutId !== null && editingGroutForm && (
              <div className="inline-editor">
                <h3>Редактирование цвета заполнения #{editingGroutId}</h3>
                <div className="form-grid two-col">
                  <label>
                    Название
                    <input
                      type="text"
                      list="ru-color-names"
                      value={editingGroutForm.name}
                      onChange={(event) => {
                        setEditingGroutNameTouched(true);
                        setEditingGroutForm((current) =>
                          current ? { ...current, name: event.target.value } : current,
                        );
                      }}
                    />
                  </label>
                  <label>
                    HEX
                    <div className="hex-input">
                      <input
                        type="text"
                        value={editingGroutForm.rgb_hex}
                        onChange={(event) => applyEditingGroutHex(event.target.value)}
                        onBlur={() => applyEditingGroutHex(editingGroutForm.rgb_hex)}
                      />
                      <input
                        type="color"
                        className="color-picker"
                        aria-label="Выбор HEX цвета заполнения"
                        value={pickerHexValue(editingGroutForm.rgb_hex)}
                        onChange={(event) => applyEditingGroutHex(event.target.value)}
                      />
                    </div>
                  </label>
                  <label className="checkbox">
                    <input
                      type="checkbox"
                      checked={Boolean(editingGroutForm.is_active)}
                      onChange={(event) =>
                        setEditingGroutForm((current) =>
                          current ? { ...current, is_active: event.target.checked } : current,
                        )
                      }
                    />
                    Активен
                  </label>
                </div>
                <div className="actions">
                  <button type="button" className="button-primary" onClick={onSaveEditGrout} disabled={busy}>
                    Сохранить
                  </button>
                  <button
                    type="button"
                    className="button-secondary"
                    onClick={() => {
                      setEditingGroutId(null);
                      setEditingGroutForm(null);
                      setEditingGroutNameTouched(false);
                    }}
                  >
                    Отмена
                  </button>
                </div>
              </div>
            )}
          </article>
        </section>
      )}
      <datalist id="ru-color-names">
        {COLOR_NAME_SUGGESTIONS_RU.map((item) => (
          <option key={item} value={item} />
        ))}
      </datalist>
      <datalist id="ral-codes">
        {RAL_SUGGESTIONS.map((item) => (
          <option key={item} value={item} />
        ))}
      </datalist>
    </div>
  );
}
