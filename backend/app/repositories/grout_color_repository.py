from __future__ import annotations

from sqlalchemy import or_, select
from sqlalchemy.orm import Session

from app.models.grout_color import GroutColor
from app.schemas.grout_color import GroutColorCreate, GroutColorUpdate


def list_grout_colors(session: Session, include_inactive: bool = False) -> list[GroutColor]:
    statement = select(GroutColor).order_by(GroutColor.id.asc())
    if not include_inactive:
        statement = statement.where(GroutColor.is_active.is_(True))
    return list(session.scalars(statement))


def get_grout_color_by_id(session: Session, grout_color_id: int) -> GroutColor | None:
    statement = select(GroutColor).where(GroutColor.id == grout_color_id)
    return session.scalar(statement)


def find_duplicates(
    session: Session,
    *,
    name: str,
    rgb_hex: str,
    exclude_id: int | None = None,
) -> list[GroutColor]:
    statement = select(GroutColor).where(
        or_(
            GroutColor.name == name,
            GroutColor.rgb_hex == rgb_hex,
        )
    )
    if exclude_id is not None:
        statement = statement.where(GroutColor.id != exclude_id)
    return list(session.scalars(statement))


def create_grout_color(session: Session, payload: GroutColorCreate) -> GroutColor:
    entity = GroutColor(
        name=payload.name,
        rgb_hex=payload.rgb_hex,
        is_active=payload.is_active,
    )
    session.add(entity)
    session.flush()
    session.refresh(entity)
    return entity


def update_grout_color(
    session: Session,
    entity: GroutColor,
    payload: GroutColorUpdate,
) -> GroutColor:
    updates = payload.model_dump(exclude_none=True)
    for field, value in updates.items():
        setattr(entity, field, value)
    session.add(entity)
    session.flush()
    session.refresh(entity)
    return entity


def deactivate_grout_color(session: Session, entity: GroutColor) -> GroutColor:
    entity.is_active = False
    session.add(entity)
    session.flush()
    session.refresh(entity)
    return entity

