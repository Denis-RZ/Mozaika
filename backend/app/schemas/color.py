from __future__ import annotations

from datetime import datetime

from pydantic import BaseModel, ConfigDict, Field, field_validator, model_validator

from app.services.color_math import normalize_hex


class ColorBase(BaseModel):
    name: str = Field(min_length=1, max_length=120)
    ral_code: str = Field(min_length=1, max_length=32)
    rgb_hex: str
    is_active: bool = True

    @field_validator("name", "ral_code")
    @classmethod
    def trim_text(cls, value: str) -> str:
        return value.strip()

    @field_validator("rgb_hex")
    @classmethod
    def validate_hex(cls, value: str) -> str:
        return normalize_hex(value)


class ColorCreate(ColorBase):
    pass


class ColorUpdate(BaseModel):
    name: str | None = Field(default=None, min_length=1, max_length=120)
    ral_code: str | None = Field(default=None, min_length=1, max_length=32)
    rgb_hex: str | None = None
    is_active: bool | None = None

    @field_validator("name", "ral_code")
    @classmethod
    def trim_optional_text(cls, value: str | None) -> str | None:
        if value is None:
            return None
        return value.strip()

    @field_validator("rgb_hex")
    @classmethod
    def validate_optional_hex(cls, value: str | None) -> str | None:
        if value is None:
            return None
        return normalize_hex(value)

    @model_validator(mode="after")
    def validate_has_updates(self) -> "ColorUpdate":
        if self.name is None and self.ral_code is None and self.rgb_hex is None and self.is_active is None:
            raise ValueError("At least one field must be provided.")
        return self


class ColorRead(ColorBase):
    id: int
    created_at: datetime

    model_config = ConfigDict(from_attributes=True)


class ColorBulkCreate(BaseModel):
    colors: list[ColorCreate] = Field(min_length=1, max_length=500)
