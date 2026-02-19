from __future__ import annotations

from sqlalchemy import select
from sqlalchemy.orm import Session

from app.models.app_setting import AppSetting
from app.schemas.settings import AdminSettingsUpdate


def get_or_create_settings(session: Session) -> AppSetting:
    statement = select(AppSetting).where(AppSetting.id == 1)
    entity = session.scalar(statement)
    if entity is not None:
        return entity

    entity = AppSetting(id=1)
    session.add(entity)
    session.flush()
    session.refresh(entity)
    return entity


def update_settings(session: Session, settings: AppSetting, payload: AdminSettingsUpdate) -> AppSetting:
    updates = payload.model_dump(exclude_none=True)
    for field, value in updates.items():
        setattr(settings, field, value)
    session.add(settings)
    session.flush()
    session.refresh(settings)
    return settings

