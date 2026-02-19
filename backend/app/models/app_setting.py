from __future__ import annotations

from datetime import datetime

from sqlalchemy import DateTime, Float, Integer, func
from sqlalchemy.orm import Mapped, mapped_column

from app.db.base import Base


class AppSetting(Base):
    __tablename__ = "app_settings"

    id: Mapped[int] = mapped_column(Integer, primary_key=True, default=1)
    default_field_width_mm: Mapped[float] = mapped_column(Float, nullable=False, default=1200.0)
    default_field_height_mm: Mapped[float] = mapped_column(Float, nullable=False, default=800.0)
    default_cell_size_mm: Mapped[float] = mapped_column(Float, nullable=False, default=10.0)
    default_gap_mm: Mapped[float] = mapped_column(Float, nullable=False, default=2.0)
    created_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True),
        nullable=False,
        server_default=func.now(),
    )
    updated_at: Mapped[datetime] = mapped_column(
        DateTime(timezone=True),
        nullable=False,
        server_default=func.now(),
        onupdate=func.now(),
    )

