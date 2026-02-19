from __future__ import annotations

from datetime import datetime

from pydantic import BaseModel, ConfigDict, Field, model_validator


class AdminSettingsRead(BaseModel):
    id: int
    default_field_width_mm: float
    default_field_height_mm: float
    default_cell_size_mm: float
    default_gap_mm: float
    created_at: datetime
    updated_at: datetime

    model_config = ConfigDict(from_attributes=True)


class AdminSettingsUpdate(BaseModel):
    default_field_width_mm: float | None = Field(default=None, gt=0)
    default_field_height_mm: float | None = Field(default=None, gt=0)
    default_cell_size_mm: float | None = Field(default=None, gt=0)
    default_gap_mm: float | None = Field(default=None, ge=0)

    @model_validator(mode="after")
    def validate_has_updates(self) -> "AdminSettingsUpdate":
        if (
            self.default_field_width_mm is None
            and self.default_field_height_mm is None
            and self.default_cell_size_mm is None
            and self.default_gap_mm is None
        ):
            raise ValueError("At least one field must be provided.")
        return self

