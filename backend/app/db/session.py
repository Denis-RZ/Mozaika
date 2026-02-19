from collections.abc import Generator

from fastapi import Depends, Request
from sqlalchemy.orm import Session

from app.db.provider import DatabaseProvider


def get_db_provider(request: Request) -> DatabaseProvider:
    return request.app.state.db_provider


def get_db_session(
    provider: DatabaseProvider = Depends(get_db_provider),
) -> Generator[Session, None, None]:
    with provider.session() as session:
        yield session

