export type TabKey = "studio" | "palette" | "settings";

export interface ColorRead {
  id: number;
  name: string;
  ral_code: string;
  rgb_hex: string;
  is_active: boolean;
  created_at: string;
}

export interface ColorCreate {
  name: string;
  ral_code: string;
  rgb_hex: string;
  is_active?: boolean;
}

export interface GroutColorRead {
  id: number;
  name: string;
  rgb_hex: string;
  is_active: boolean;
  created_at: string;
}

export interface GroutColorCreate {
  name: string;
  rgb_hex: string;
  is_active?: boolean;
}

export interface AdminSettingsRead {
  id: number;
  default_field_width_mm: number;
  default_field_height_mm: number;
  default_cell_size_mm: number;
  default_gap_mm: number;
  created_at: string;
  updated_at: string;
}

export interface BootstrapResponse {
  settings: AdminSettingsRead;
  colors: ColorRead[];
  grout_colors: GroutColorRead[];
}

export interface MosaicColorUsage {
  id: number;
  name: string;
  ral_code: string;
  rgb_hex: string;
  cells: number;
  ratio: number;
}

export interface MosaicGenerateResponse {
  rows: number;
  columns: number;
  field_width_mm: number;
  field_height_mm: number;
  mosaic_width_mm: number;
  mosaic_height_mm: number;
  cell_size_mm: number;
  gap_mm: number;
  offset_x_mm: number;
  offset_y_mm: number;
  grout_color_hex: string;
  requested_max_colors: number;
  actual_colors_used: number;
  used_colors: MosaicColorUsage[];
  grid_color_ids: number[][];
  preview_png_base64: string;
}

export interface StudioFormState {
  fieldWidthMm: number;
  fieldHeightMm: number;
  cellSizeMm: number;
  gapMm: number;
  maxColors: number;
  groutColorId: number | null;
  offsetXmm: number;
  offsetYmm: number;
}

