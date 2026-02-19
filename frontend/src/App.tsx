import { useCallback, useEffect, useMemo, useState } from "react";

import {
  createColor,
  createColorsBulk,
  createGroutColor,
  deactivateColor,
  deactivateGroutColor,
  fetchBootstrap,
  generateMosaic,
  patchColor,
  patchGroutColor,
  updateSettings,
} from "./api";
import type {
  BootstrapResponse,
  ColorCreate,
  ColorRead,
  GroutColorCreate,
  GroutColorRead,
  MosaicGenerateResponse,
  StudioFormState,
  TabKey,
} from "./types";

const DEFAULT_MAX_COLORS = 3;

const tabs: Array<{ key: TabKey; label: string }> = [
  { key: "studio", label: "Генерация" },
  { key: "palette", label: "Палитра" },
  { key: "settings", label: "Настройки" },
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
    Белый: "RAL 9016",
    Черный: "RAL 9005",
    Серый: "RAL 7040",
    Красный: "RAL 3020",
    Оранжевый: "RAL 2004",
    Желтый: "RAL 1023",
    Зеленый: "RAL 6018",
    Бирюзовый: "RAL 5018",
    Синий: "RAL 5015",
    Фиолетовый: "RAL 4008",
    Розовый: "RAL 4010",
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
  const columns = Math.floor((fieldWidth + gap) / pitch);
  const rows = Math.floor((fieldHeight + gap) / pitch);
  if (rows <= 0 || columns <= 0) {
    return { rows: 0, columns: 0, mosaicWidth: 0, mosaicHeight: 0 };
  }
  const mosaicWidth = columns * cellSize + (columns - 1) * gap;
  const mosaicHeight = rows * cellSize + (rows - 1) * gap;
  return { rows, columns, mosaicWidth, mosaicHeight };
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

export default function App() {
  const [activeTab, setActiveTab] = useState<TabKey>("studio");
  const [bootstrap, setBootstrap] = useState<BootstrapResponse | null>(null);
  const [loadingBootstrap, setLoadingBootstrap] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const [imageFile, setImageFile] = useState<File | null>(null);
  const [imagePreviewUrl, setImagePreviewUrl] = useState<string | null>(null);
  const [mosaicResult, setMosaicResult] = useState<MosaicGenerateResponse | null>(null);

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

  const maxOffsetX = Math.max(0, studioForm.fieldWidthMm - gridEstimate.mosaicWidth);
  const maxOffsetY = Math.max(0, studioForm.fieldHeightMm - gridEstimate.mosaicHeight);

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

      const firstActiveGrout = payload.grout_colors.find((color) => color.is_active) ?? null;
      const activeColorCount = Math.max(
        1,
        payload.colors.filter((color) => color.is_active).length,
      );
      const defaultMax = Math.min(DEFAULT_MAX_COLORS, activeColorCount);
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
  }, []);

  useEffect(() => {
    void refreshBootstrap();
  }, [refreshBootstrap]);

  useEffect(() => {
    if (activePalette.length === 0) {
      return;
    }
    const activeIds = new Set(activePalette.map((color) => color.id));
    setIncludeColorIds((items) => items.filter((id) => activeIds.has(id)));
    setExcludeColorIds((items) => items.filter((id) => activeIds.has(id)));
    const defaultMax = Math.min(DEFAULT_MAX_COLORS, activePalette.length);
    setStudioForm((current) => ({
      ...current,
      maxColors:
        current.maxColors > 0
          ? Math.max(1, Math.min(current.maxColors, activePalette.length))
          : defaultMax,
    }));
  }, [activePalette]);

  useEffect(() => {
    setStudioForm((current) => ({
      ...current,
      offsetXmm: Math.min(current.offsetXmm, maxOffsetX),
      offsetYmm: Math.min(current.offsetYmm, maxOffsetY),
    }));
  }, [maxOffsetX, maxOffsetY]);

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

  const setStudioField = <K extends keyof StudioFormState>(field: K, value: StudioFormState[K]) => {
    setStudioForm((current) => ({ ...current, [field]: value }));
  };

  const onTabClick = (tab: TabKey) => {
    if (tab === activeTab && tab === "studio") {
      setError(null);
      setNotice(
        imageFile
          ? 'Вы уже на вкладке "Генерация". Нажмите "Сгенерировать мозаику", чтобы обновить превью.'
          : 'Вы уже на вкладке "Генерация". Сначала загрузите изображение, затем нажмите "Сгенерировать мозаику".',
      );
      return;
    }
    setActiveTab(tab);
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
      return;
    }
    if (mode === "include") {
      setIncludeColorIds((items) => items.filter((id) => id !== colorId));
      setExcludeColorIds((items) => [...items, colorId]);
      return;
    }
    setExcludeColorIds((items) => items.filter((id) => id !== colorId));
  };

  const onImageChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0] ?? null;
    setImageFile(file);
    if (imagePreviewUrl) {
      URL.revokeObjectURL(imagePreviewUrl);
      setImagePreviewUrl(null);
    }
    if (file) {
      setImagePreviewUrl(URL.createObjectURL(file));
    }
  };

  const onGenerateMosaic = async () => {
    if (!imageFile) {
      setError("Перед генерацией загрузите изображение.");
      return;
    }
    setBusy(true);
    setError(null);
    setNotice(null);
    try {
      const response = await generateMosaic(
        imageFile,
        studioForm,
        includeColorIds,
        excludeColorIds,
      );
      setMosaicResult(response);
      setNotice("Мозаика успешно сгенерирована.");
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : "Ошибка генерации.");
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
        </div>
      </header>

      <nav className="tabs">
        {tabs.map((tab) => (
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

      {error && <section className="alert alert-error">{error}</section>}
      {notice && <section className="alert alert-success">{notice}</section>}

      {activeTab === "studio" && (
        <section className="workspace">
          <article className="panel">
            <h2>Параметры генерации</h2>
            <div className="form-grid two-col">
              <label>
                <LabelTitle
                  text="Ширина поля (мм)"
                  hint="Физическая ширина рабочей зоны, на которую раскладывается мозаика."
                />
                <input
                  type="number"
                  min={1}
                  value={studioForm.fieldWidthMm}
                  onChange={(event) => setStudioField("fieldWidthMm", Number(event.target.value))}
                />
              </label>
              <label>
                <LabelTitle
                  text="Высота поля (мм)"
                  hint="Физическая высота рабочей зоны, на которую раскладывается мозаика."
                />
                <input
                  type="number"
                  min={1}
                  value={studioForm.fieldHeightMm}
                  onChange={(event) => setStudioField("fieldHeightMm", Number(event.target.value))}
                />
              </label>
              <label>
                <LabelTitle
                  text="Размер ячейки (мм)"
                  hint="Размер одной квадратной плитки. Чем меньше размер, тем больше детализация."
                />
                <input
                  type="number"
                  min={1}
                  value={studioForm.cellSizeMm}
                  onChange={(event) => setStudioField("cellSizeMm", Number(event.target.value))}
                />
              </label>
              <label>
                <LabelTitle
                  text="Расстояние между ячейками (мм)"
                  hint="Ширина шва между плитками."
                />
                <input
                  type="number"
                  min={0}
                  value={studioForm.gapMm}
                  onChange={(event) => setStudioField("gapMm", Number(event.target.value))}
                />
              </label>
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
                  text="Макс. цветов"
                  hint="Верхний предел количества цветов плиток в мозаике. Это значение «до N», а не строго N."
                />
                <input
                  type="number"
                  min={1}
                  max={Math.max(1, activePalette.length)}
                  value={studioForm.maxColors}
                  onChange={(event) => setStudioField("maxColors", Number(event.target.value))}
                />
              </label>
              <label>
                <LabelTitle
                  text="Цвет заполнения"
                  hint="Цвет швов (пространства между ячейками), не цвет самих плиток."
                />
                <select
                  value={studioForm.groutColorId ?? ""}
                  onChange={(event) =>
                    setStudioField(
                      "groutColorId",
                      event.target.value ? Number(event.target.value) : null,
                    )
                  }
                >
                  <option value="">Выберите цвет</option>
                  {activeGroutColors.map((color) => (
                    <option key={color.id} value={color.id}>
                      {color.name} ({color.rgb_hex})
                    </option>
                  ))}
                </select>
              </label>
              <label>
                <LabelTitle
                  text="Смещение X (мм)"
                  hint="Сдвиг мозаики вправо внутри рабочего поля."
                />
                <input
                  type="number"
                  min={0}
                  max={maxOffsetX}
                  value={studioForm.offsetXmm}
                  onChange={(event) => setStudioField("offsetXmm", Number(event.target.value))}
                />
              </label>
              <label>
                <LabelTitle
                  text="Смещение Y (мм)"
                  hint="Сдвиг мозаики вниз внутри рабочего поля."
                />
                <input
                  type="number"
                  min={0}
                  max={maxOffsetY}
                  value={studioForm.offsetYmm}
                  onChange={(event) => setStudioField("offsetYmm", Number(event.target.value))}
                />
              </label>
            </div>
            <div className="actions">
              <button type="button" className="button-secondary" onClick={applyGridDraft} disabled={busy}>
                Применить оценку сетки
              </button>
            </div>
            <p className="muted">
              Для нормальной детализации ставьте больше 1 цвета. Сейчас: {studioForm.maxColors}.
            </p>

            <label className="file-upload">
              <input type="file" accept="image/*" onChange={onImageChange} />
              <span>{imageFile ? imageFile.name : "Нажмите, чтобы выбрать изображение"}</span>
            </label>

            <h3>
              Стратегия палитры <FieldHint text="Нажатие по цвету переключает режим: нейтрально -> включить -> исключить." />
            </h3>
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
            <p className="muted">
              Кнопка ниже запускает генерацию и обновляет превью с учетом текущих параметров.
            </p>

            <button type="button" className="button-primary" onClick={onGenerateMosaic} disabled={busy}>
              {busy ? "Генерация..." : "Сгенерировать мозаику"}
            </button>
          </article>

          <article className="panel preview-panel">
            <h2>Превью</h2>
            {!mosaicResult && imagePreviewUrl && (
              <img className="preview-image" src={imagePreviewUrl} alt="Превью исходного изображения" />
            )}
            {mosaicResult && (
              <>
                <img
                  className="preview-image"
                  src={`data:image/png;base64,${mosaicResult.preview_png_base64}`}
                  alt="Превью сгенерированной мозаики"
                />
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
              </>
            )}
            {!mosaicResult && !imagePreviewUrl && (
              <div className="placeholder">Загрузите изображение и запустите генерацию.</div>
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
                  type="number"
                  min={1}
                  value={settingsDraft.default_cell_size_mm}
                  onChange={(event) =>
                    setSettingsDraft((current) => ({
                      ...current,
                      default_cell_size_mm: Number(event.target.value),
                    }))
                  }
                />
              </label>
              <label>
                <LabelTitle
                  text="Расстояние по умолчанию (мм)"
                  hint="Базовая ширина шва между плитками."
                />
                <input
                  type="number"
                  min={0}
                  value={settingsDraft.default_gap_mm}
                  onChange={(event) =>
                    setSettingsDraft((current) => ({
                      ...current,
                      default_gap_mm: Number(event.target.value),
                    }))
                  }
                />
              </label>
              <div className="actions">
                <button type="submit" className="button-primary" disabled={busy}>
                  Сохранить
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
