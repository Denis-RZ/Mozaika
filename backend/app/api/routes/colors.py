from __future__ import annotations

from fastapi import APIRouter, Depends, HTTPException, Path, Query, status
from sqlalchemy.orm import Session

from app.db.session import get_db_session
from app.repositories import color_repository
from app.schemas.color import ColorBulkCreate, ColorCreate, ColorRead, ColorUpdate

router = APIRouter(prefix="/colors", tags=["colors"])


@router.get("", response_model=list[ColorRead])
def list_colors(
    include_inactive: bool = Query(default=False),
    session: Session = Depends(get_db_session),
) -> list[ColorRead]:
    return color_repository.list_colors(session, include_inactive=include_inactive)


@router.post("", response_model=ColorRead, status_code=status.HTTP_201_CREATED)
def create_color(
    payload: ColorCreate,
    session: Session = Depends(get_db_session),
) -> ColorRead:
    duplicates = color_repository.find_duplicates(
        session,
        name=payload.name,
        ral_code=payload.ral_code,
        rgb_hex=payload.rgb_hex,
    )
    if duplicates:
        raise HTTPException(
            status_code=status.HTTP_409_CONFLICT,
            detail="Цвет с таким названием, RAL или HEX уже существует.",
        )
    return color_repository.create_color(session, payload)


@router.post("/bulk", response_model=list[ColorRead], status_code=status.HTTP_201_CREATED)
def create_colors_bulk(
    payload: ColorBulkCreate,
    session: Session = Depends(get_db_session),
) -> list[ColorRead]:
    existing = color_repository.list_colors(session, include_inactive=True)
    existing_names = {color.name for color in existing}
    existing_ral = {color.ral_code for color in existing}
    existing_hex = {color.rgb_hex for color in existing}

    for item in payload.colors:
        if item.name in existing_names or item.ral_code in existing_ral or item.rgb_hex in existing_hex:
            raise HTTPException(
                status_code=status.HTTP_409_CONFLICT,
                detail=f"Найден дубликат для цвета '{item.name}'.",
            )
        existing_names.add(item.name)
        existing_ral.add(item.ral_code)
        existing_hex.add(item.rgb_hex)

    return color_repository.create_many_colors(session, payload.colors)


@router.patch("/{color_id}", response_model=ColorRead)
def update_color(
    color_id: int = Path(..., ge=1),
    payload: ColorUpdate = ...,
    session: Session = Depends(get_db_session),
) -> ColorRead:
    color = color_repository.get_color_by_id(session, color_id)
    if color is None:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="Цвет не найден.")

    new_name = payload.name if payload.name is not None else color.name
    new_ral = payload.ral_code if payload.ral_code is not None else color.ral_code
    new_hex = payload.rgb_hex if payload.rgb_hex is not None else color.rgb_hex
    duplicates = color_repository.find_duplicates(
        session,
        name=new_name,
        ral_code=new_ral,
        rgb_hex=new_hex,
        exclude_id=color.id,
    )
    if duplicates:
        raise HTTPException(
            status_code=status.HTTP_409_CONFLICT,
            detail="Цвет с таким названием, RAL или HEX уже существует.",
        )

    return color_repository.update_color(session, color, payload)


@router.delete("/{color_id}", response_model=ColorRead)
def deactivate_color(
    color_id: int = Path(..., ge=1),
    session: Session = Depends(get_db_session),
) -> ColorRead:
    color = color_repository.get_color_by_id(session, color_id)
    if color is None:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="Цвет не найден.")
    return color_repository.deactivate_color(session, color)
