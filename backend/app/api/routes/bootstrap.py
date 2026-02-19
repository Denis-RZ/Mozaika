from __future__ import annotations

from fastapi import APIRouter, Depends
from sqlalchemy.orm import Session

from app.db.session import get_db_session
from app.repositories import color_repository, grout_color_repository, settings_repository
from app.schemas.bootstrap import BootstrapResponse

router = APIRouter(prefix="/bootstrap", tags=["bootstrap"])


@router.get("", response_model=BootstrapResponse)
def get_bootstrap(session: Session = Depends(get_db_session)) -> BootstrapResponse:
    settings = settings_repository.get_or_create_settings(session)
    colors = color_repository.list_colors(session, include_inactive=True)
    grout_colors = grout_color_repository.list_grout_colors(session, include_inactive=True)
    return BootstrapResponse(settings=settings, colors=colors, grout_colors=grout_colors)

