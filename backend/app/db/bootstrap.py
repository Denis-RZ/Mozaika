from __future__ import annotations

from app.db.provider import DatabaseProvider
from app.repositories import grout_color_repository, settings_repository
from app.schemas.grout_color import GroutColorCreate

DEFAULT_GROUT_COLORS = [
    GroutColorCreate(name="Warm White", rgb_hex="#F2EFEA", is_active=True),
    GroutColorCreate(name="Graphite", rgb_hex="#353535", is_active=True),
    GroutColorCreate(name="Sand", rgb_hex="#CDBB9C", is_active=True),
]


def seed_default_data(provider: DatabaseProvider) -> None:
    with provider.session() as session:
        settings_repository.get_or_create_settings(session)
        existing = grout_color_repository.list_grout_colors(session, include_inactive=True)
        if existing:
            return

        for item in DEFAULT_GROUT_COLORS:
            grout_color_repository.create_grout_color(session, item)

