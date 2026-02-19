import type {
  AdminSettingsRead,
  BootstrapResponse,
  ColorCreate,
  ColorRead,
  DatabaseConfigRead,
  DatabaseConfigUpdate,
  GroutColorCreate,
  GroutColorRead,
  MosaicGenerateResponse,
  StudioFormState,
} from "./types";

const API_BASE = import.meta.env.VITE_API_BASE_URL ?? "";

async function handleJsonResponse<T>(response: Response): Promise<T> {
  if (!response.ok) {
    let detail = response.statusText;
    try {
      const payload = (await response.json()) as { detail?: string };
      detail = payload.detail ?? detail;
    } catch {
      // Keep default message when no json body.
    }
    throw new Error(detail);
  }
  return (await response.json()) as T;
}

export async function fetchBootstrap(): Promise<BootstrapResponse> {
  const response = await fetch(`${API_BASE}/api/bootstrap`);
  return handleJsonResponse<BootstrapResponse>(response);
}

export async function createColor(payload: ColorCreate): Promise<ColorRead> {
  const response = await fetch(`${API_BASE}/api/colors`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload),
  });
  return handleJsonResponse<ColorRead>(response);
}

export async function createColorsBulk(payload: { colors: ColorCreate[] }): Promise<ColorRead[]> {
  const response = await fetch(`${API_BASE}/api/colors/bulk`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload),
  });
  return handleJsonResponse<ColorRead[]>(response);
}

export async function patchColor(
  colorId: number,
  payload: Partial<ColorCreate>,
): Promise<ColorRead> {
  const response = await fetch(`${API_BASE}/api/colors/${colorId}`, {
    method: "PATCH",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload),
  });
  return handleJsonResponse<ColorRead>(response);
}

export async function deactivateColor(colorId: number): Promise<ColorRead> {
  const response = await fetch(`${API_BASE}/api/colors/${colorId}`, {
    method: "DELETE",
  });
  return handleJsonResponse<ColorRead>(response);
}

export async function updateSettings(
  payload: Partial<AdminSettingsRead>,
): Promise<AdminSettingsRead> {
  const response = await fetch(`${API_BASE}/api/admin/settings`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload),
  });
  return handleJsonResponse<AdminSettingsRead>(response);
}

export async function fetchDatabaseConfig(): Promise<DatabaseConfigRead> {
  const response = await fetch(`${API_BASE}/api/admin/database`);
  return handleJsonResponse<DatabaseConfigRead>(response);
}

export async function configureDatabase(
  payload: DatabaseConfigUpdate,
): Promise<DatabaseConfigRead> {
  const response = await fetch(`${API_BASE}/api/admin/database`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload),
  });
  return handleJsonResponse<DatabaseConfigRead>(response);
}

export async function createGroutColor(payload: GroutColorCreate): Promise<GroutColorRead> {
  const response = await fetch(`${API_BASE}/api/admin/grout-colors`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload),
  });
  return handleJsonResponse<GroutColorRead>(response);
}

export async function patchGroutColor(
  groutColorId: number,
  payload: Partial<GroutColorCreate>,
): Promise<GroutColorRead> {
  const response = await fetch(`${API_BASE}/api/admin/grout-colors/${groutColorId}`, {
    method: "PATCH",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload),
  });
  return handleJsonResponse<GroutColorRead>(response);
}

export async function deactivateGroutColor(groutColorId: number): Promise<GroutColorRead> {
  const response = await fetch(`${API_BASE}/api/admin/grout-colors/${groutColorId}`, {
    method: "DELETE",
  });
  return handleJsonResponse<GroutColorRead>(response);
}

export async function generateMosaic(
  image: File,
  form: StudioFormState,
  includeIds: number[],
  excludeIds: number[],
): Promise<MosaicGenerateResponse> {
  const data = new FormData();
  data.set("image", image);
  data.set("field_width_mm", String(form.fieldWidthMm));
  data.set("field_height_mm", String(form.fieldHeightMm));
  data.set("cell_size_mm", String(form.cellSizeMm));
  data.set("gap_mm", String(form.gapMm));
  data.set("max_colors", String(form.maxColors));
  data.set("offset_x_mm", String(form.offsetXmm));
  data.set("offset_y_mm", String(form.offsetYmm));
  if (form.groutColorId !== null) {
    data.set("grout_color_id", String(form.groutColorId));
  }
  if (includeIds.length > 0) {
    data.set("include_color_ids", includeIds.join(","));
  }
  if (excludeIds.length > 0) {
    data.set("exclude_color_ids", excludeIds.join(","));
  }

  const response = await fetch(`${API_BASE}/api/mosaic/generate`, {
    method: "POST",
    body: data,
  });
  return handleJsonResponse<MosaicGenerateResponse>(response);
}
