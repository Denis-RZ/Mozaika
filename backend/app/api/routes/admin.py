from __future__ import annotations

from fastapi import APIRouter, Depends, HTTPException, Path, Query, status
from sqlalchemy.orm import Session

from app.db.session import get_db_session
from app.repositories import grout_color_repository, settings_repository
from app.schemas.grout_color import GroutColorCreate, GroutColorRead, GroutColorUpdate
from app.schemas.settings import AdminSettingsRead, AdminSettingsUpdate

router = APIRouter(prefix="/admin", tags=["admin"])


@router.get("/settings", response_model=AdminSettingsRead)
def get_admin_settings(session: Session = Depends(get_db_session)) -> AdminSettingsRead:
    return settings_repository.get_or_create_settings(session)


@router.put("/settings", response_model=AdminSettingsRead)
def update_admin_settings(
    payload: AdminSettingsUpdate,
    session: Session = Depends(get_db_session),
) -> AdminSettingsRead:
    settings = settings_repository.get_or_create_settings(session)
    return settings_repository.update_settings(session, settings, payload)


@router.get("/grout-colors", response_model=list[GroutColorRead])
def list_grout_colors(
    include_inactive: bool = Query(default=False),
    session: Session = Depends(get_db_session),
) -> list[GroutColorRead]:
    return grout_color_repository.list_grout_colors(session, include_inactive=include_inactive)


@router.post("/grout-colors", response_model=GroutColorRead, status_code=status.HTTP_201_CREATED)
def create_grout_color(
    payload: GroutColorCreate,
    session: Session = Depends(get_db_session),
) -> GroutColorRead:
    duplicates = grout_color_repository.find_duplicates(
        session,
        name=payload.name,
        rgb_hex=payload.rgb_hex,
    )
    if duplicates:
        raise HTTPException(
            status_code=status.HTTP_409_CONFLICT,
            detail="Цвет заполнения с таким названием или HEX уже существует.",
        )
    return grout_color_repository.create_grout_color(session, payload)


@router.patch("/grout-colors/{grout_color_id}", response_model=GroutColorRead)
def update_grout_color(
    grout_color_id: int = Path(..., ge=1),
    payload: GroutColorUpdate = ...,
    session: Session = Depends(get_db_session),
) -> GroutColorRead:
    entity = grout_color_repository.get_grout_color_by_id(session, grout_color_id)
    if entity is None:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="Цвет заполнения не найден.")

    name = payload.name if payload.name is not None else entity.name
    rgb_hex = payload.rgb_hex if payload.rgb_hex is not None else entity.rgb_hex
    duplicates = grout_color_repository.find_duplicates(
        session,
        name=name,
        rgb_hex=rgb_hex,
        exclude_id=entity.id,
    )
    if duplicates:
        raise HTTPException(
            status_code=status.HTTP_409_CONFLICT,
            detail="Цвет заполнения с таким названием или HEX уже существует.",
        )

    return grout_color_repository.update_grout_color(session, entity, payload)


@router.delete("/grout-colors/{grout_color_id}", response_model=GroutColorRead)
def deactivate_grout_color(
    grout_color_id: int = Path(..., ge=1),
    session: Session = Depends(get_db_session),
) -> GroutColorRead:
    entity = grout_color_repository.get_grout_color_by_id(session, grout_color_id)
    if entity is None:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="Цвет заполнения не найден.")
    return grout_color_repository.deactivate_grout_color(session, entity)
