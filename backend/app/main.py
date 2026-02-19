from __future__ import annotations

from contextlib import asynccontextmanager

from fastapi import FastAPI, Request
from fastapi.middleware.cors import CORSMiddleware

from app.api.routes import admin, bootstrap, colors, mosaic
from app.config import get_settings
from app.db.bootstrap import seed_default_data
from app.db.provider import create_database_provider


def create_app() -> FastAPI:
    settings = get_settings()
    db_provider = create_database_provider(
        database_url=settings.database_url,
        echo=settings.database_echo,
    )

    @asynccontextmanager
    async def lifespan(app: FastAPI):
        db_provider.init_schema()
        seed_default_data(db_provider)
        try:
            yield
        finally:
            db_provider.dispose()

    app = FastAPI(title=settings.app_name, lifespan=lifespan)
    app.state.db_provider = db_provider
    app.add_middleware(
        CORSMiddleware,
        allow_origins=settings.cors_origins,
        allow_credentials=True,
        allow_methods=["*"],
        allow_headers=["*"],
    )
    app.include_router(colors.router, prefix=settings.api_prefix)
    app.include_router(mosaic.router, prefix=settings.api_prefix)
    app.include_router(admin.router, prefix=settings.api_prefix)
    app.include_router(bootstrap.router, prefix=settings.api_prefix)

    @app.get("/health")
    def health(request: Request) -> dict[str, str]:
        is_ok = request.app.state.db_provider.healthcheck()
        return {"status": "ok" if is_ok else "degraded"}

    return app


app = create_app()
