from __future__ import annotations

from collections.abc import Sequence

from sqlalchemy import or_, select
from sqlalchemy.orm import Session

from app.models.color import Color
from app.schemas.color import ColorCreate, ColorUpdate


def list_colors(session: Session, include_inactive: bool = False) -> list[Color]:
    statement = select(Color).order_by(Color.id.asc())
    if not include_inactive:
        statement = statement.where(Color.is_active.is_(True))
    return list(session.scalars(statement))


def get_colors_by_ids(session: Session, color_ids: set[int]) -> list[Color]:
    if not color_ids:
        return []
    statement = select(Color).where(Color.id.in_(color_ids)).order_by(Color.id.asc())
    return list(session.scalars(statement))


def get_color_by_id(session: Session, color_id: int) -> Color | None:
    statement = select(Color).where(Color.id == color_id)
    return session.scalar(statement)


def find_duplicates(
    session: Session,
    *,
    name: str,
    ral_code: str,
    rgb_hex: str,
    exclude_id: int | None = None,
) -> list[Color]:
    statement = select(Color).where(
        or_(
            Color.name == name,
            Color.ral_code == ral_code,
            Color.rgb_hex == rgb_hex,
        )
    )
    if exclude_id is not None:
        statement = statement.where(Color.id != exclude_id)
    return list(session.scalars(statement))


def create_color(session: Session, payload: ColorCreate) -> Color:
    color = Color(
        name=payload.name,
        ral_code=payload.ral_code,
        rgb_hex=payload.rgb_hex,
        is_active=payload.is_active,
    )
    session.add(color)
    session.flush()
    session.refresh(color)
    return color


def create_many_colors(session: Session, payloads: Sequence[ColorCreate]) -> list[Color]:
    colors = [
        Color(
            name=payload.name,
            ral_code=payload.ral_code,
            rgb_hex=payload.rgb_hex,
            is_active=payload.is_active,
        )
        for payload in payloads
    ]
    session.add_all(colors)
    session.flush()
    for color in colors:
        session.refresh(color)
    return colors


def update_color(session: Session, color: Color, payload: ColorUpdate) -> Color:
    updates = payload.model_dump(exclude_none=True)
    for field, value in updates.items():
        setattr(color, field, value)
    session.add(color)
    session.flush()
    session.refresh(color)
    return color


def deactivate_color(session: Session, color: Color) -> Color:
    color.is_active = False
    session.add(color)
    session.flush()
    session.refresh(color)
    return color
