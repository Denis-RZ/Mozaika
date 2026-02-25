export type TabKey = "studio" | "projects" | "palette" | "settings";
export type UserRole = "admin" | "customer" | "viewer";

export interface AuthUserRead {
  id: number;
  username: string;
  display_name: string;
  role: UserRole;
}

export interface AuthSessionRead {
  is_authenticated: boolean;
  expires_at: string | null;
  user: AuthUserRead | null;
}

export interface AuthLoginRequest {
  username: string;
  password: string;
}

export interface AuthLoginResponse {
  token: string;
  expires_at: string;
  user: AuthUserRead;
}

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

export interface DatabaseConfigRead {
  provider: string;
  connection_string: string;
  echo: boolean;
  supported_providers: string[];
}

export interface DatabaseConfigUpdate {
  provider: string;
  connection_string: string;
  echo?: boolean;
  create_schema?: boolean;
  seed_defaults?: boolean;
}

export interface DatabaseConfigTestResponse {
  success: boolean;
  message: string;
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

export interface MosaicPriceBreakdown {
  currency: string;
  total_chips: number;
  area_sq_m: number;
  setup_price: number;
  chips_price: number;
  colors_price: number;
  complexity_price: number;
  grout_price: number;
  subtotal_price: number;
  min_order_price: number;
  min_order_applied: boolean;
  total_price: number;
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
  total_chips: number;
  price: MosaicPriceBreakdown;
  used_colors: MosaicColorUsage[];
  grid_color_ids: number[][];
  preview_png_base64: string;
}

export interface MosaicReplaceColorRequest {
  grid_color_ids: number[][];
  from_color_id: number;
  to_color_id: number;
  field_width_mm: number;
  field_height_mm: number;
  cell_size_mm: number;
  gap_mm: number;
  offset_x_mm: number;
  offset_y_mm: number;
  grout_color_hex: string;
  requested_max_colors?: number;
}

export interface MosaicExportRequest {
  grid_color_ids: number[][];
  field_width_mm: number;
  field_height_mm: number;
  cell_size_mm: number;
  gap_mm: number;
  offset_x_mm: number;
  offset_y_mm: number;
  grout_color_hex: string;
  dpi: number;
  mirror_horizontal: boolean;
  include_legend: boolean;
  module_chip_columns: number;
  module_chip_rows: number;
  module_start_number: number;
  include_color_numbers: boolean;
  file_name: string;
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

export interface ProjectSnapshotPayload {
  mosaic: MosaicGenerateResponse;
  include_color_ids: number[];
  exclude_color_ids: number[];
  grout_color_id: number | null;
  preview_zoom: number;
  preview_pan_x: number;
  preview_pan_y: number;
}

export interface ProjectSaveGenerationRequest {
  name: string;
  note: string;
  snapshot: ProjectSnapshotPayload;
}

export interface ProjectCreateRequest {
  name: string;
  description: string;
  source_image_mime_type: string;
  source_image_base64: string;
  initial_generation: ProjectSaveGenerationRequest;
}

export interface ProjectUpdateRequest {
  name?: string;
  description?: string;
}

export interface ProjectGenerationSummary {
  id: number;
  version: number;
  name: string;
  note: string;
  created_at: string;
}

export interface ProjectGenerationRead {
  id: number;
  version: number;
  name: string;
  note: string;
  created_at: string;
  snapshot: ProjectSnapshotPayload;
}

export interface ProjectListItem {
  id: number;
  name: string;
  description: string;
  owner_username: string | null;
  created_at: string;
  updated_at: string;
  generations_count: number;
  active_generation_id: number | null;
  last_generation_at: string | null;
}

export interface ProjectListPage {
  items: ProjectListItem[];
  total: number;
  page: number;
  limit: number;
}

export interface ProjectRead {
  id: number;
  name: string;
  description: string;
  owner_username: string | null;
  source_image_mime_type: string;
  source_image_base64: string;
  created_at: string;
  updated_at: string;
  generations_count: number;
  active_generation_id: number | null;
  last_generation_at: string | null;
  active_generation: ProjectGenerationRead | null;
  generations: ProjectGenerationSummary[];
}

export interface ProjectShareCreateRequest {
  generation_id: number | null;
  expires_in_days: number | null;
}

export interface ProjectShareRead {
  id: number;
  project_id: number;
  generation_id: number;
  generation_version: number;
  generation_name: string;
  token: string;
  created_at: string;
  expires_at: string | null;
  is_revoked: boolean;
  created_by: string;
}

export interface ProjectShareResolveResponse {
  project_id: number;
  project_name: string;
  project_description: string;
  source_image_mime_type: string;
  source_image_base64: string;
  generation: ProjectGenerationRead;
  share: ProjectShareRead;
}

export type ProjectOrderStatus = "submitted" | "in_review" | "approved" | "rejected" | "cancelled";

export interface ProjectOrderCreateRequest {
  generation_id: number | null;
  customer_name: string;
  customer_email: string;
  customer_phone: string;
  comment: string;
}

export interface ProjectOrderStatusUpdateRequest {
  status: ProjectOrderStatus;
  status_comment: string;
}

export interface ProjectOrderRead {
  id: number;
  project_id: number;
  generation_id: number;
  generation_version: number;
  generation_name: string;
  created_at: string;
  updated_at: string;
  status: ProjectOrderStatus;
  status_comment: string;
  customer_name: string;
  customer_email: string;
  customer_phone: string;
  comment: string;
  submitted_by: string;
  total_price: number;
  currency: string;
  total_chips: number;
  colors_used: number;
}
