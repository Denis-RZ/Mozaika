import io

from PIL import Image

from app.models.color import Color
from app.services.mosaic import generate_mosaic


def _image_bytes(color: tuple[int, int, int], width: int = 64, height: int = 64) -> bytes:
    image = Image.new("RGB", (width, height), color=color)
    buffer = io.BytesIO()
    image.save(buffer, format="PNG")
    return buffer.getvalue()


def test_generate_mosaic_with_limited_colors() -> None:
    colors = [
        Color(id=1, name="White", ral_code="RAL 9016", rgb_hex="#FFFFFF", is_active=True),
        Color(id=2, name="Black", ral_code="RAL 9005", rgb_hex="#000000", is_active=True),
        Color(id=3, name="Red", ral_code="RAL 3020", rgb_hex="#C1121F", is_active=True),
    ]

    result = generate_mosaic(
        image_bytes=_image_bytes((190, 20, 30)),
        available_colors=colors,
        field_width_mm=120,
        field_height_mm=120,
        cell_size_mm=10,
        gap_mm=2,
        grout_color_hex="#DDDDDD",
        max_colors=2,
        include_color_ids={3},
        exclude_color_ids=set(),
        offset_x_mm=0,
        offset_y_mm=0,
    )

    assert result.rows > 0
    assert result.columns > 0
    assert result.actual_colors_used <= 2
    assert result.preview_png_base64


def test_generate_mosaic_forces_include_color_presence() -> None:
    colors = [
        Color(id=1, name="White", ral_code="RAL 9016", rgb_hex="#FFFFFF", is_active=True),
        Color(id=2, name="Black", ral_code="RAL 9005", rgb_hex="#000000", is_active=True),
        Color(id=3, name="Red", ral_code="RAL 3020", rgb_hex="#C1121F", is_active=True),
        Color(id=4, name="Blue", ral_code="RAL 5015", rgb_hex="#2B7FFF", is_active=True),
    ]

    result = generate_mosaic(
        image_bytes=_image_bytes((35, 110, 210)),
        available_colors=colors,
        field_width_mm=120,
        field_height_mm=120,
        cell_size_mm=10,
        gap_mm=2,
        grout_color_hex="#DDDDDD",
        max_colors=2,
        include_color_ids={3},
        exclude_color_ids=set(),
        offset_x_mm=0,
        offset_y_mm=0,
    )

    used_ids = {item.id for item in result.used_colors}
    assert 3 in used_ids
