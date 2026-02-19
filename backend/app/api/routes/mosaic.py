from __future__ import annotations

from fastapi import APIRouter, Depends, File, Form, HTTPException, UploadFile, status
from sqlalchemy.orm import Session

from app.config import get_settings
from app.db.session import get_db_session
from app.repositories import color_repository, grout_color_repository, settings_repository
from app.schemas.mosaic import MosaicGenerateResponse
from app.services.color_math import normalize_hex
from app.services.mosaic import MosaicGenerationError, generate_mosaic

router = APIRouter(prefix="/mosaic", tags=["mosaic"])


def _parse_ids(raw: str | None) -> set[int]:
    if raw is None or raw.strip() == "":
        return set()
    try:
        return {int(item.strip()) for item in raw.split(",") if item.strip()}
    except ValueError as exc:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="Поля include_color_ids/exclude_color_ids должны содержать целые id через запятую.",
        ) from exc


def _resolve_grout_color_hex(
    *,
    session: Session,
    grout_color_id: int | None,
    grout_color_hex: str | None,
) -> str:
    if grout_color_id is not None:
        grout_color = grout_color_repository.get_grout_color_by_id(session, grout_color_id)
        if grout_color is None or not grout_color.is_active:
            raise HTTPException(
                status_code=status.HTTP_400_BAD_REQUEST,
                detail="Выбранный цвет заполнения недоступен.",
            )
        return grout_color.rgb_hex

    if grout_color_hex is not None and grout_color_hex.strip() != "":
        try:
            return normalize_hex(grout_color_hex)
        except ValueError as exc:
            raise HTTPException(
                status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
                detail=str(exc),
            ) from exc

    active_grout_colors = grout_color_repository.list_grout_colors(session, include_inactive=False)
    if active_grout_colors:
        return active_grout_colors[0].rgb_hex
    return "#FFFFFF"


@router.post("/generate", response_model=MosaicGenerateResponse)
async def generate_mosaic_endpoint(
    image: UploadFile = File(...),
    field_width_mm: float | None = Form(default=None, gt=0),
    field_height_mm: float | None = Form(default=None, gt=0),
    cell_size_mm: float | None = Form(default=None, gt=0),
    gap_mm: float | None = Form(default=None, ge=0),
    grout_color_id: int | None = Form(default=None, ge=1),
    grout_color_hex: str | None = Form(default=None),
    max_colors: int | None = Form(default=None, ge=1),
    include_color_ids: str | None = Form(default=None),
    exclude_color_ids: str | None = Form(default=None),
    offset_x_mm: float = Form(default=0, ge=0),
    offset_y_mm: float = Form(default=0, ge=0),
    session: Session = Depends(get_db_session),
) -> MosaicGenerateResponse:
    if image.content_type and not image.content_type.startswith("image/"):
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="Загруженный файл должен быть изображением.",
        )

    source_bytes = await image.read()
    if not source_bytes:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="Загруженное изображение пустое.",
        )
    app_cfg = get_settings()
    max_upload_bytes = app_cfg.max_upload_mb * 1024 * 1024
    if len(source_bytes) > max_upload_bytes:
        raise HTTPException(
            status_code=status.HTTP_413_REQUEST_ENTITY_TOO_LARGE,
            detail=f"Размер изображения превышает лимит {app_cfg.max_upload_mb} МБ.",
        )

    app_settings = settings_repository.get_or_create_settings(session)
    resolved_field_width = field_width_mm or app_settings.default_field_width_mm
    resolved_field_height = field_height_mm or app_settings.default_field_height_mm
    resolved_cell_size = cell_size_mm or app_settings.default_cell_size_mm
    resolved_gap = gap_mm if gap_mm is not None else app_settings.default_gap_mm
    resolved_grout_hex = _resolve_grout_color_hex(
        session=session,
        grout_color_id=grout_color_id,
        grout_color_hex=grout_color_hex,
    )

    available_colors = color_repository.list_colors(session, include_inactive=False)
    if not available_colors:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="Палитра пуста. Добавьте хотя бы один активный цвет.",
        )

    try:
        return generate_mosaic(
            image_bytes=source_bytes,
            available_colors=available_colors,
            field_width_mm=resolved_field_width,
            field_height_mm=resolved_field_height,
            cell_size_mm=resolved_cell_size,
            gap_mm=resolved_gap,
            grout_color_hex=resolved_grout_hex,
            max_colors=max_colors,
            include_color_ids=_parse_ids(include_color_ids),
            exclude_color_ids=_parse_ids(exclude_color_ids),
            offset_x_mm=offset_x_mm,
            offset_y_mm=offset_y_mm,
        )
    except MosaicGenerationError as exc:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail=str(exc),
        ) from exc
