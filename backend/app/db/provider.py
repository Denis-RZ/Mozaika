from __future__ import annotations

from abc import ABC, abstractmethod
from contextlib import contextmanager
from typing import Callable, Iterator

from sqlalchemy import create_engine, text
from sqlalchemy.engine import Engine
from sqlalchemy.engine.url import make_url
from sqlalchemy.orm import Session, sessionmaker

from app.db.base import Base


class DatabaseProvider(ABC):
    @contextmanager
    @abstractmethod
    def session(self) -> Iterator[Session]:
        """Return managed SQLAlchemy session."""

    @abstractmethod
    def init_schema(self) -> None:
        """Create schema objects if not present."""

    @abstractmethod
    def healthcheck(self) -> bool:
        """Check provider connectivity."""

    @abstractmethod
    def dispose(self) -> None:
        """Release underlying resources."""


class SQLAlchemyProvider(DatabaseProvider):
    def __init__(self, database_url: str, echo: bool = False) -> None:
        connect_args = {}
        if database_url.startswith("sqlite"):
            connect_args = {"check_same_thread": False}

        self._engine: Engine = create_engine(
            database_url,
            echo=echo,
            pool_pre_ping=True,
            connect_args=connect_args,
        )
        self._session_factory = sessionmaker(
            bind=self._engine,
            autoflush=False,
            autocommit=False,
            expire_on_commit=False,
        )

    @contextmanager
    def session(self) -> Iterator[Session]:
        db = self._session_factory()
        try:
            yield db
            db.commit()
        except Exception:
            db.rollback()
            raise
        finally:
            db.close()

    def init_schema(self) -> None:
        import app.models  # noqa: F401

        Base.metadata.create_all(bind=self._engine)

    def healthcheck(self) -> bool:
        try:
            with self._engine.connect() as connection:
                connection.execute(text("SELECT 1"))
            return True
        except Exception:
            return False

    def dispose(self) -> None:
        self._engine.dispose()


ProviderBuilder = Callable[[str, bool], DatabaseProvider]
_REGISTRY: dict[str, ProviderBuilder] = {}


def register_database_provider(scheme: str, builder: ProviderBuilder) -> None:
    _REGISTRY[scheme] = builder


def list_registered_provider_schemes() -> list[str]:
    return sorted(_REGISTRY.keys())


def _register_defaults() -> None:
    if _REGISTRY:
        return
    for scheme in ("sqlite", "postgresql", "mysql", "mssql", "oracle"):
        register_database_provider(scheme, lambda url, echo: SQLAlchemyProvider(url, echo=echo))


def create_database_provider(database_url: str, echo: bool = False) -> DatabaseProvider:
    _register_defaults()
    backend_name = make_url(database_url).get_backend_name()
    builder = _REGISTRY.get(backend_name)
    if builder is None:
        available = ", ".join(list_registered_provider_schemes())
        raise ValueError(
            f"Unsupported database backend '{backend_name}'. Registered backends: {available}"
        )
    return builder(database_url, echo)
