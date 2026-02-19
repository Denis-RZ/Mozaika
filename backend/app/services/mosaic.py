from __future__ import annotations

import base64
import io
from dataclasses import dataclass
from math import floor
from typing import Sequence

import numpy as np
from PIL import Image

from app.models.color import Color
from app.schemas.mosaic import MosaicColorUsage, MosaicGenerateResponse
from app.services.color_math import hex_to_rgb, normalize_hex, rgb_array_to_lab


class MosaicGenerationError(ValueError):
    pass


@dataclass(frozen=True)
class PaletteColor:
    id: int
    name: str
    ral_code: str
    rgb_hex: str
    rgb: tuple[int, int, int]


def _compute_grid(
    *,
    field_width_mm: float,
    field_height_mm: float,
    cell_size_mm: float,
    gap_mm: float,
) -> tuple[int, int, float, float]:
    if field_width_mm <= 0 or field_height_mm <= 0:
        raise MosaicGenerationError("Ширина и высота поля должны быть больше нуля.")
    if cell_size_mm <= 0:
        raise MosaicGenerationError("Размер ячейки должен быть больше нуля.")
    if gap_mm < 0:
        raise MosaicGenerationError("Расстояние между ячейками не может быть отрицательным.")

    pitch = cell_size_mm + gap_mm
    columns = floor((field_width_mm + gap_mm) / pitch)
    rows = floor((field_height_mm + gap_mm) / pitch)
    if columns <= 0 or rows <= 0:
        raise MosaicGenerationError("С текущими параметрами на поле не помещается ни одной ячейки.")

    mosaic_width_mm = (columns * cell_size_mm) + ((columns - 1) * gap_mm)
    mosaic_height_mm = (rows * cell_size_mm) + ((rows - 1) * gap_mm)
    return rows, columns, mosaic_width_mm, mosaic_height_mm


def _prepare_source_pixels(image_bytes: bytes, rows: int, columns: int) -> np.ndarray:
    if not image_bytes:
        raise MosaicGenerationError("Файл изображения пустой.")

    try:
        source = Image.open(io.BytesIO(image_bytes)).convert("RGB")
    except Exception as exc:  # pragma: no cover - pillow throws various exceptions
        raise MosaicGenerationError("Не удалось прочитать изображение.") from exc

    target_aspect = columns / rows
    source_aspect = source.width / source.height

    if source_aspect > target_aspect:
        new_width = max(1, int(source.height * target_aspect))
        left = (source.width - new_width) // 2
        source = source.crop((left, 0, left + new_width, source.height))
    elif source_aspect < target_aspect:
        new_height = max(1, int(source.width / target_aspect))
        top = (source.height - new_height) // 2
        source = source.crop((0, top, source.width, top + new_height))

    reduced = source.resize((columns, rows), Image.Resampling.BOX)
    return np.asarray(reduced, dtype=np.float32)


def _as_palette(colors: Sequence[Color]) -> list[PaletteColor]:
    return [
        PaletteColor(
            id=color.id,
            name=color.name,
            ral_code=color.ral_code,
            rgb_hex=normalize_hex(color.rgb_hex),
            rgb=hex_to_rgb(color.rgb_hex),
        )
        for color in colors
        if color.is_active
    ]


def _nearest_palette_indices(pixel_rgb: np.ndarray, palette_rgb: np.ndarray) -> np.ndarray:
    rows, columns, _ = pixel_rgb.shape
    flat_pixels = pixel_rgb.reshape(-1, 3)
    pixel_lab = rgb_array_to_lab(flat_pixels)
    palette_lab = rgb_array_to_lab(palette_rgb)

    distances = np.sum((pixel_lab[:, None, :] - palette_lab[None, :, :]) ** 2, axis=2)
    nearest = np.argmin(distances, axis=1)
    return nearest.reshape(rows, columns)


def _pick_final_palette(
    *,
    palette: list[PaletteColor],
    initial_grid: np.ndarray,
    max_colors: int,
    include_color_ids: set[int],
) -> list[int]:
    forced_indices = [index for index, color in enumerate(palette) if color.id in include_color_ids]
    if max_colors < len(forced_indices):
        raise MosaicGenerationError(
            "Число max_colors меньше количества принудительно включенных цветов."
        )

    if max_colors >= len(palette):
        return list(range(len(palette)))

    counts = np.bincount(initial_grid.ravel(), minlength=len(palette))
    ranked = sorted(range(len(palette)), key=lambda idx: counts[idx], reverse=True)

    selected: list[int] = []
    for idx in forced_indices:
        if idx not in selected:
            selected.append(idx)
    for idx in ranked:
        if idx in selected:
            continue
        selected.append(idx)
        if len(selected) == max_colors:
            break

    if len(selected) < max_colors:
        for idx in range(len(palette)):
            if idx not in selected:
                selected.append(idx)
            if len(selected) == max_colors:
                break

    return selected


def _render_preview(
    *,
    grid_indices: np.ndarray,
    palette_rgb: np.ndarray,
    field_width_mm: float,
    field_height_mm: float,
    cell_size_mm: float,
    gap_mm: float,
    offset_x_mm: float,
    offset_y_mm: float,
    grout_color_hex: str,
) -> str:
    max_preview_side_px = 1400
    scale = max_preview_side_px / max(field_width_mm, field_height_mm)
    scale = min(max(scale, 0.4), 8.0)

    field_width_px = max(1, int(round(field_width_mm * scale)))
    field_height_px = max(1, int(round(field_height_mm * scale)))
    cell_px = max(1, int(round(cell_size_mm * scale)))
    gap_px = max(0, int(round(gap_mm * scale)))
    step_px = cell_px + gap_px

    grout_rgb = np.array(hex_to_rgb(grout_color_hex), dtype=np.uint8)
    canvas = np.zeros((field_height_px, field_width_px, 3), dtype=np.uint8)
    canvas[:, :] = grout_rgb

    origin_x_px = int(round(offset_x_mm * scale))
    origin_y_px = int(round(offset_y_mm * scale))

    rows, columns = grid_indices.shape
    for row in range(rows):
        y0 = origin_y_px + row * step_px
        y1 = min(y0 + cell_px, field_height_px)
        if y0 >= field_height_px:
            break
        if y1 <= 0:
            continue

        for column in range(columns):
            x0 = origin_x_px + column * step_px
            x1 = min(x0 + cell_px, field_width_px)
            if x0 >= field_width_px:
                break
            if x1 <= 0:
                continue

            color = palette_rgb[grid_indices[row, column]]
            canvas[max(y0, 0) : y1, max(x0, 0) : x1] = color

    image = Image.fromarray(canvas)
    buffer = io.BytesIO()
    image.save(buffer, format="PNG", optimize=True)
    return base64.b64encode(buffer.getvalue()).decode("ascii")


def _enforce_included_colors_presence(
    *,
    final_grid: np.ndarray,
    source_pixels: np.ndarray,
    final_palette: list[PaletteColor],
    include_color_ids: set[int],
) -> np.ndarray:
    if not include_color_ids:
        return final_grid

    palette_index_by_id = {color.id: index for index, color in enumerate(final_palette)}
    include_indices = [palette_index_by_id[color_id] for color_id in include_color_ids if color_id in palette_index_by_id]
    if not include_indices:
        return final_grid

    flat_grid = final_grid.ravel().copy()
    if len(include_indices) > flat_grid.size:
        raise MosaicGenerationError("Слишком много принудительно включенных цветов для текущей сетки.")

    counts = np.bincount(flat_grid, minlength=len(final_palette))
    missing_indices = [index for index in include_indices if counts[index] == 0]
    if not missing_indices:
        return final_grid

    flat_pixels = source_pixels.reshape(-1, 3)
    pixel_lab = rgb_array_to_lab(flat_pixels)
    palette_rgb = np.asarray([color.rgb for color in final_palette], dtype=np.float32)
    palette_lab = rgb_array_to_lab(palette_rgb)

    reserved_pixel_indices: set[int] = set()
    for palette_index in missing_indices:
        distances = np.sum((pixel_lab - palette_lab[palette_index]) ** 2, axis=1)
        if reserved_pixel_indices:
            distances[list(reserved_pixel_indices)] = np.inf
        selected_pixel_index = int(np.argmin(distances))
        reserved_pixel_indices.add(selected_pixel_index)
        flat_grid[selected_pixel_index] = palette_index

    return flat_grid.reshape(final_grid.shape)


def generate_mosaic(
    *,
    image_bytes: bytes,
    available_colors: Sequence[Color],
    field_width_mm: float,
    field_height_mm: float,
    cell_size_mm: float,
    gap_mm: float,
    grout_color_hex: str,
    max_colors: int | None,
    include_color_ids: set[int],
    exclude_color_ids: set[int],
    offset_x_mm: float,
    offset_y_mm: float,
) -> MosaicGenerateResponse:
    if include_color_ids.intersection(exclude_color_ids):
        raise MosaicGenerationError("Один цвет нельзя одновременно включить и исключить.")

    rows, columns, mosaic_width_mm, mosaic_height_mm = _compute_grid(
        field_width_mm=field_width_mm,
        field_height_mm=field_height_mm,
        cell_size_mm=cell_size_mm,
        gap_mm=gap_mm,
    )

    if offset_x_mm < 0 or offset_y_mm < 0:
        raise MosaicGenerationError("Смещение мозаики не может быть отрицательным.")
    if offset_x_mm + mosaic_width_mm > field_width_mm + 1e-6:
        raise MosaicGenerationError("Смещение X выводит мозаику за границы поля.")
    if offset_y_mm + mosaic_height_mm > field_height_mm + 1e-6:
        raise MosaicGenerationError("Смещение Y выводит мозаику за границы поля.")

    grout_color_hex = normalize_hex(grout_color_hex)
    source_pixels = _prepare_source_pixels(image_bytes, rows, columns)

    active_palette = _as_palette(available_colors)
    active_palette = [color for color in active_palette if color.id not in exclude_color_ids]

    palette_ids = {color.id for color in active_palette}
    missing_includes = include_color_ids.difference(palette_ids)
    if missing_includes:
        raise MosaicGenerationError(f"Цвета с id {sorted(missing_includes)} недоступны для включения.")
    if not active_palette:
        raise MosaicGenerationError("Нет активных цветов для генерации.")

    requested_max_colors = max_colors or len(active_palette)
    if requested_max_colors <= 0:
        raise MosaicGenerationError("Параметр max_colors должен быть больше нуля.")
    requested_max_colors = min(requested_max_colors, len(active_palette))

    palette_rgb = np.asarray([color.rgb for color in active_palette], dtype=np.float32)
    initial_grid = _nearest_palette_indices(source_pixels, palette_rgb)

    selected_indices = _pick_final_palette(
        palette=active_palette,
        initial_grid=initial_grid,
        max_colors=requested_max_colors,
        include_color_ids=include_color_ids,
    )
    final_palette = [active_palette[index] for index in selected_indices]
    final_palette_rgb = np.asarray([color.rgb for color in final_palette], dtype=np.float32)
    final_grid = _nearest_palette_indices(source_pixels, final_palette_rgb)
    final_grid = _enforce_included_colors_presence(
        final_grid=final_grid,
        source_pixels=source_pixels,
        final_palette=final_palette,
        include_color_ids=include_color_ids,
    )

    palette_id_lookup = np.asarray([color.id for color in final_palette], dtype=np.int32)
    grid_color_ids = palette_id_lookup[final_grid]

    counts = np.bincount(final_grid.ravel(), minlength=len(final_palette))
    total_cells = int(rows * columns)
    used_colors: list[MosaicColorUsage] = []
    for index, color in enumerate(final_palette):
        cell_count = int(counts[index])
        if cell_count == 0:
            continue
        used_colors.append(
            MosaicColorUsage(
                id=color.id,
                name=color.name,
                ral_code=color.ral_code,
                rgb_hex=color.rgb_hex,
                cells=cell_count,
                ratio=cell_count / total_cells,
            )
        )

    preview_png_base64 = _render_preview(
        grid_indices=final_grid,
        palette_rgb=np.asarray([color.rgb for color in final_palette], dtype=np.uint8),
        field_width_mm=field_width_mm,
        field_height_mm=field_height_mm,
        cell_size_mm=cell_size_mm,
        gap_mm=gap_mm,
        offset_x_mm=offset_x_mm,
        offset_y_mm=offset_y_mm,
        grout_color_hex=grout_color_hex,
    )

    return MosaicGenerateResponse(
        rows=rows,
        columns=columns,
        field_width_mm=field_width_mm,
        field_height_mm=field_height_mm,
        mosaic_width_mm=mosaic_width_mm,
        mosaic_height_mm=mosaic_height_mm,
        cell_size_mm=cell_size_mm,
        gap_mm=gap_mm,
        offset_x_mm=offset_x_mm,
        offset_y_mm=offset_y_mm,
        grout_color_hex=grout_color_hex,
        requested_max_colors=requested_max_colors,
        actual_colors_used=len(used_colors),
        used_colors=used_colors,
        grid_color_ids=grid_color_ids.tolist(),
        preview_png_base64=preview_png_base64,
    )
