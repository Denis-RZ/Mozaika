from functools import lru_cache

from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    app_name: str = "Mozaika API"
    api_prefix: str = "/api"
    database_url: str = "sqlite:///./mozaika.db"
    database_echo: bool = False
    cors_origins: list[str] = ["*"]
    max_upload_mb: int = 20

    model_config = SettingsConfigDict(
        env_prefix="MOZAIKA_",
        env_file=".env",
        extra="ignore",
    )


@lru_cache
def get_settings() -> Settings:
    return Settings()
