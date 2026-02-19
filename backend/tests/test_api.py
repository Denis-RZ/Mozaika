from __future__ import annotations

import io
from pathlib import Path

from fastapi.testclient import TestClient
from PIL import Image

from app.config import get_settings
from app.main import create_app


def _make_client(tmp_path: Path, monkeypatch) -> TestClient:
    database_url = f"sqlite:///{tmp_path / 'api_test.db'}"
    monkeypatch.setenv("MOZAIKA_DATABASE_URL", database_url)
    monkeypatch.setenv("MOZAIKA_DATABASE_ECHO", "false")
    get_settings.cache_clear()
    app = create_app()
    return TestClient(app)


def _png_bytes(color: tuple[int, int, int]) -> bytes:
    image = Image.new("RGB", (64, 64), color)
    buffer = io.BytesIO()
    image.save(buffer, format="PNG")
    return buffer.getvalue()


def test_bootstrap_and_admin_endpoints(tmp_path: Path, monkeypatch) -> None:
    with _make_client(tmp_path, monkeypatch) as client:
        bootstrap_response = client.get("/api/bootstrap")
        assert bootstrap_response.status_code == 200
        bootstrap = bootstrap_response.json()
        assert bootstrap["settings"]["default_cell_size_mm"] == 10
        assert len(bootstrap["grout_colors"]) >= 1

        settings_update = client.put(
            "/api/admin/settings",
            json={"default_cell_size_mm": 12, "default_gap_mm": 3},
        )
        assert settings_update.status_code == 200
        assert settings_update.json()["default_cell_size_mm"] == 12

        grout_create = client.post(
            "/api/admin/grout-colors",
            json={"name": "Ivory", "rgb_hex": "#EAE2CF", "is_active": True},
        )
        assert grout_create.status_code == 201
        grout_id = grout_create.json()["id"]

        grout_patch = client.patch(
            f"/api/admin/grout-colors/{grout_id}",
            json={"is_active": False},
        )
        assert grout_patch.status_code == 200
        assert grout_patch.json()["is_active"] is False


def test_mosaic_generation_full_flow(tmp_path: Path, monkeypatch) -> None:
    with _make_client(tmp_path, monkeypatch) as client:
        colors_response = client.post(
            "/api/colors/bulk",
            json={
                "colors": [
                    {"name": "White", "ral_code": "RAL 9016", "rgb_hex": "#F5F5F5"},
                    {"name": "Graphite", "ral_code": "RAL 9005", "rgb_hex": "#1E1E1E"},
                    {"name": "Red", "ral_code": "RAL 3020", "rgb_hex": "#C1121F"},
                ]
            },
        )
        assert colors_response.status_code == 201
        colors = colors_response.json()
        red_id = next(item["id"] for item in colors if item["name"] == "Red")

        grout_colors_response = client.get("/api/admin/grout-colors")
        assert grout_colors_response.status_code == 200
        grout_id = grout_colors_response.json()[0]["id"]

        files = {"image": ("sample.png", _png_bytes((190, 30, 30)), "image/png")}
        data = {
            "field_width_mm": "600",
            "field_height_mm": "400",
            "max_colors": "2",
            "grout_color_id": str(grout_id),
            "include_color_ids": str(red_id),
            "offset_x_mm": "0",
            "offset_y_mm": "0",
        }
        generate_response = client.post("/api/mosaic/generate", files=files, data=data)
        assert generate_response.status_code == 200
        result = generate_response.json()
        assert result["actual_colors_used"] <= 2
        assert len(result["preview_png_base64"]) > 100

