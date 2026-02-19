from __future__ import annotations

from pydantic import BaseModel, Field


class MosaicColorUsage(BaseModel):
    id: int
    name: str
    ral_code: str
    rgb_hex: str
    cells: int = Field(ge=0)
    ratio: float = Field(ge=0.0, le=1.0)


class MosaicGenerateResponse(BaseModel):
    rows: int
    columns: int
    field_width_mm: float
    field_height_mm: float
    mosaic_width_mm: float
    mosaic_height_mm: float
    cell_size_mm: float
    gap_mm: float
    offset_x_mm: float
    offset_y_mm: float
    grout_color_hex: str
    requested_max_colors: int
    actual_colors_used: int
    used_colors: list[MosaicColorUsage]
    grid_color_ids: list[list[int]]
    preview_png_base64: str

