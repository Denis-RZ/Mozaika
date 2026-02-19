from __future__ import annotations

from pydantic import BaseModel

from app.schemas.color import ColorRead
from app.schemas.grout_color import GroutColorRead
from app.schemas.settings import AdminSettingsRead


class BootstrapResponse(BaseModel):
    settings: AdminSettingsRead
    colors: list[ColorRead]
    grout_colors: list[GroutColorRead]

