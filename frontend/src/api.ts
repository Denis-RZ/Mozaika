import type {
  AdminSettingsRead,
  AuthLoginRequest,
  AuthLoginResponse,
  AuthSessionRead,
  BootstrapResponse,
  ColorCreate,
  ColorRead,
  DatabaseConfigRead,
  DatabaseConfigTestResponse,
  DatabaseConfigUpdate,
  GroutColorCreate,
  GroutColorRead,
  MosaicExportRequest,
  MosaicGenerateResponse,
  MosaicReplaceColorRequest,
  ProjectCreateRequest,
  ProjectGenerationRead,
  ProjectListItem,
  ProjectListPage,
  ProjectOrderCreateRequest,
  ProjectOrderRead,
  ProjectOrderStatusUpdateRequest,
  ProjectRead,
  ProjectSaveGenerationRequest,
  ProjectShareCreateRequest,
  ProjectShareRead,
  ProjectShareResolveResponse,
  ProjectUpdateRequest,
  StudioFormState,
} from "./types";

const API_BASE = import.meta.env.VITE_API_BASE_URL ?? "";
let authToken: string | null = null;

export function setApiAuthToken(token: string | null): void {
  authToken = token && token.trim().length > 0 ? token.trim() : null;
}

async function apiFetch(url: string, init?: RequestInit): Promise<Response> {
  const headers = new Headers(init?.headers ?? {});
  if (authToken) {
    headers.set("Authorization", `Bearer ${authToken}`);
  }

  return fetch(url, {
    ...init,
    headers,
  });
}

async function readErrorDetail(response: Response): Promise<string> {
  let detail = response.statusText;
  try {
    const payload = (await response.json()) as { detail?: string };
    detail = payload.detail ?? detail;
    return detail;
  } catch {
    // Try plain text fallback.
  }

  try {
    const raw = await response.text();
    if (raw.trim().length > 0) {
      detail = raw.trim();
    }
  } catch {
    // Keep default fallback.
  }

  return detail;
}

async function handleJsonResponse<T>(response: Response): Promise<T> {
  if (!response.ok) {
    const detail = await readErrorDetail(response);
    throw new Error(detail);
  }
  return (await response.json()) as T;
}

export async function login(payload: AuthLoginRequest): Promise<AuthLoginResponse> {
  const response = await apiFetch(`${API_BASE}/api/auth/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload),
  });
  return handleJsonResponse<AuthLoginResponse>(response);
}

export async function logout(): Promise<void> {
  const response = await apiFetch(`${API_BASE}/api/auth/logout`, {
    method: "POST",
  });
  if (!response.ok) {
    const detail = await readErrorDetail(response);
    throw new Error(detail);
  }
}

export async function fetchAuthSession(): Promise<AuthSessionRead> {
  const response = await apiFetch(`${API_BASE}/api/auth/me`);
  return handleJsonResponse<AuthSessionRead>(response);
}

export async function fetchBootstrap(): Promise<BootstrapResponse> {
  const response = await apiFetch(`${API_BASE}/api/bootstrap`);
  return handleJsonResponse<BootstrapResponse>(response);
}

export async function createColor(payload: ColorCreate): Promise<ColorRead> {
  const response = await apiFetch(`${API_BASE}/api/colors`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload),
  });
  return handleJsonResponse<ColorRead>(response);
}

export async function createColorsBulk(payload: { colors: ColorCreate[] }): Promise<ColorRead[]> {
  const response = await apiFetch(`${API_BASE}/api/colors/bulk`, {
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
  const response = await apiFetch(`${API_BASE}/api/colors/${colorId}`, {
    method: "PATCH",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload),
  });
  return handleJsonResponse<ColorRead>(response);
}

export async function deactivateColor(colorId: number): Promise<ColorRead> {
  const response = await apiFetch(`${API_BASE}/api/colors/${colorId}`, {
    method: "DELETE",
  });
  return handleJsonResponse<ColorRead>(response);
}

export async function updateSettings(
  payload: Partial<AdminSettingsRead>,
): Promise<AdminSettingsRead> {
  const response = await apiFetch(`${API_BASE}/api/admin/settings`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload),
  });
  return handleJsonResponse<AdminSettingsRead>(response);
}

export async function fetchDatabaseConfig(): Promise<DatabaseConfigRead> {
  const response = await apiFetch(`${API_BASE}/api/admin/database`);
  return handleJsonResponse<DatabaseConfigRead>(response);
}

export async function configureDatabase(
  payload: DatabaseConfigUpdate,
): Promise<DatabaseConfigRead> {
  const response = await apiFetch(`${API_BASE}/api/admin/database`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload),
  });
  return handleJsonResponse<DatabaseConfigRead>(response);
}

export async function testDatabaseConnection(
  payload: DatabaseConfigUpdate,
): Promise<DatabaseConfigTestResponse> {
  const response = await apiFetch(`${API_BASE}/api/admin/database/test`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload),
  });
  return handleJsonResponse<DatabaseConfigTestResponse>(response);
}

export async function createGroutColor(payload: GroutColorCreate): Promise<GroutColorRead> {
  const response = await apiFetch(`${API_BASE}/api/admin/grout-colors`, {
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
  const response = await apiFetch(`${API_BASE}/api/admin/grout-colors/${groutColorId}`, {
    method: "PATCH",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload),
  });
  return handleJsonResponse<GroutColorRead>(response);
}

export async function deactivateGroutColor(groutColorId: number): Promise<GroutColorRead> {
  const response = await apiFetch(`${API_BASE}/api/admin/grout-colors/${groutColorId}`, {
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

  const response = await apiFetch(`${API_BASE}/api/mosaic/generate`, {
    method: "POST",
    body: data,
  });
  return handleJsonResponse<MosaicGenerateResponse>(response);
}

export async function replaceMosaicColor(
  payload: MosaicReplaceColorRequest,
): Promise<MosaicGenerateResponse> {
  const response = await apiFetch(`${API_BASE}/api/mosaic/replace-color`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload),
  });
  return handleJsonResponse<MosaicGenerateResponse>(response);
}

function parseDownloadFileName(contentDisposition: string | null, fallback: string): string {
  if (!contentDisposition) {
    return fallback;
  }

  const utfMatch = /filename\*=UTF-8''([^;]+)/i.exec(contentDisposition);
  if (utfMatch && utfMatch[1]) {
    try {
      return decodeURIComponent(utfMatch[1]);
    } catch {
      // Continue to other patterns.
    }
  }

  const quotedMatch = /filename=\"([^\"]+)\"/i.exec(contentDisposition);
  if (quotedMatch && quotedMatch[1]) {
    return quotedMatch[1];
  }

  const simpleMatch = /filename=([^;]+)/i.exec(contentDisposition);
  if (simpleMatch && simpleMatch[1]) {
    return simpleMatch[1].trim();
  }

  return fallback;
}

export async function exportMosaic(
  format:
    | "png"
    | "jpeg"
    | "svg"
    | "pdf"
    | "materials-csv"
    | "grid-csv"
    | "modules-csv"
    | "assembly-kit-pdf",
  payload: MosaicExportRequest,
): Promise<{ blob: Blob; fileName: string }> {
  const response = await apiFetch(`${API_BASE}/api/mosaic/export/${format}`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload),
  });

  if (!response.ok) {
    const detail = await readErrorDetail(response);
    throw new Error(detail);
  }

  const extension =
    format === "jpeg"
      ? "jpg"
      : format === "materials-csv" || format === "grid-csv" || format === "modules-csv"
        ? "csv"
        : format === "assembly-kit-pdf"
          ? "pdf"
          : format;
  const fallback = `mozaika.${extension}`;
  return {
    blob: await response.blob(),
    fileName: parseDownloadFileName(response.headers.get("content-disposition"), fallback),
  };
}

export async function fetchProjects(
  page = 1,
  limit = 20,
): Promise<ProjectListPage> {
  const params = new URLSearchParams({ page: String(page), limit: String(limit) });
  const response = await apiFetch(`${API_BASE}/api/projects?${params.toString()}`);
  const raw = await handleJsonResponse<ProjectListPage>(response);
  const items: ProjectListItem[] = raw.items;
  return { ...raw, items };
}

export async function deleteProject(projectId: number): Promise<void> {
  const response = await apiFetch(`${API_BASE}/api/projects/${projectId}`, {
    method: "DELETE",
  });
  if (!response.ok && response.status !== 204) {
    const detail = await readErrorDetail(response);
    throw new Error(detail);
  }
}

export async function createProject(payload: ProjectCreateRequest): Promise<ProjectRead> {
  const response = await apiFetch(`${API_BASE}/api/projects`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload),
  });
  return handleJsonResponse<ProjectRead>(response);
}

export async function fetchProject(projectId: number): Promise<ProjectRead> {
  const response = await apiFetch(`${API_BASE}/api/projects/${projectId}`);
  return handleJsonResponse<ProjectRead>(response);
}

export async function updateProject(
  projectId: number,
  payload: ProjectUpdateRequest,
): Promise<ProjectRead> {
  const response = await apiFetch(`${API_BASE}/api/projects/${projectId}`, {
    method: "PATCH",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload),
  });
  return handleJsonResponse<ProjectRead>(response);
}

export async function saveProjectGeneration(
  projectId: number,
  payload: ProjectSaveGenerationRequest,
): Promise<ProjectGenerationRead> {
  const response = await apiFetch(`${API_BASE}/api/projects/${projectId}/generations`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload),
  });
  return handleJsonResponse<ProjectGenerationRead>(response);
}

export async function fetchProjectGeneration(
  projectId: number,
  generationId: number,
): Promise<ProjectGenerationRead> {
  const response = await apiFetch(`${API_BASE}/api/projects/${projectId}/generations/${generationId}`);
  return handleJsonResponse<ProjectGenerationRead>(response);
}

export async function activateProjectGeneration(
  projectId: number,
  generationId: number,
): Promise<ProjectRead> {
  const response = await apiFetch(
    `${API_BASE}/api/projects/${projectId}/generations/${generationId}/activate`,
    {
      method: "POST",
    },
  );
  return handleJsonResponse<ProjectRead>(response);
}

export async function listProjectShares(projectId: number): Promise<ProjectShareRead[]> {
  const response = await apiFetch(`${API_BASE}/api/projects/${projectId}/shares`);
  return handleJsonResponse<ProjectShareRead[]>(response);
}

export async function createProjectShare(
  projectId: number,
  payload: ProjectShareCreateRequest,
): Promise<ProjectShareRead> {
  const response = await apiFetch(`${API_BASE}/api/projects/${projectId}/shares`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload),
  });
  return handleJsonResponse<ProjectShareRead>(response);
}

export async function revokeProjectShare(projectId: number, shareId: number): Promise<ProjectShareRead> {
  const response = await apiFetch(`${API_BASE}/api/projects/${projectId}/shares/${shareId}/revoke`, {
    method: "POST",
  });
  return handleJsonResponse<ProjectShareRead>(response);
}

export async function resolveProjectShare(token: string): Promise<ProjectShareResolveResponse> {
  const response = await apiFetch(`${API_BASE}/api/project-shares/${encodeURIComponent(token)}`);
  return handleJsonResponse<ProjectShareResolveResponse>(response);
}

export async function listProjectOrders(projectId: number): Promise<ProjectOrderRead[]> {
  const response = await apiFetch(`${API_BASE}/api/projects/${projectId}/orders`);
  return handleJsonResponse<ProjectOrderRead[]>(response);
}

export async function createProjectOrder(
  projectId: number,
  payload: ProjectOrderCreateRequest,
): Promise<ProjectOrderRead> {
  const response = await apiFetch(`${API_BASE}/api/projects/${projectId}/orders`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload),
  });
  return handleJsonResponse<ProjectOrderRead>(response);
}

export async function updateProjectOrderStatus(
  projectId: number,
  orderId: number,
  payload: ProjectOrderStatusUpdateRequest,
): Promise<ProjectOrderRead> {
  const response = await apiFetch(`${API_BASE}/api/projects/${projectId}/orders/${orderId}/status`, {
    method: "PATCH",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload),
  });
  return handleJsonResponse<ProjectOrderRead>(response);
}
