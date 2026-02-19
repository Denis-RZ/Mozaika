from pathlib import Path

from sqlalchemy import select

from app.db.provider import create_database_provider
from app.models.color import Color


def test_sqlalchemy_provider_init_and_session(tmp_path: Path) -> None:
    db_file = tmp_path / "mozaika_test.db"
    provider = create_database_provider(f"sqlite:///{db_file}")
    provider.init_schema()

    with provider.session() as session:
        color = Color(
            name="White",
            ral_code="RAL 9016",
            rgb_hex="#FFFFFF",
            is_active=True,
        )
        session.add(color)

    with provider.session() as session:
        total = len(list(session.scalars(select(Color))))
        assert total == 1

    assert provider.healthcheck() is True
    provider.dispose()

