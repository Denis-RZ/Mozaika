from __future__ import annotations

import re

import numpy as np

HEX_COLOR_PATTERN = re.compile(r"^[0-9A-Fa-f]{6}$")


def normalize_hex(value: str) -> str:
    raw = value.strip().lstrip("#")
    if len(raw) == 3:
        raw = "".join(char * 2 for char in raw)
    if not HEX_COLOR_PATTERN.fullmatch(raw):
        raise ValueError("HEX-цвет должен быть в формате #RRGGBB.")
    return f"#{raw.upper()}"


def hex_to_rgb(value: str) -> tuple[int, int, int]:
    normalized = normalize_hex(value)
    return (
        int(normalized[1:3], 16),
        int(normalized[3:5], 16),
        int(normalized[5:7], 16),
    )


def rgb_array_to_lab(rgb: np.ndarray) -> np.ndarray:
    """
    Convert RGB [0..255] to CIE LAB (D65).
    Supports arrays shaped (..., 3).
    """
    rgb = np.asarray(rgb, dtype=np.float32)
    rgb = np.clip(rgb / 255.0, 0.0, 1.0)

    linear = np.where(
        rgb > 0.04045,
        ((rgb + 0.055) / 1.055) ** 2.4,
        rgb / 12.92,
    )

    transform = np.array(
        [
            [0.4124564, 0.3575761, 0.1804375],
            [0.2126729, 0.7151522, 0.0721750],
            [0.0193339, 0.1191920, 0.9503041],
        ],
        dtype=np.float32,
    )
    xyz = linear @ transform.T

    reference_white = np.array([0.95047, 1.0, 1.08883], dtype=np.float32)
    xyz = xyz / reference_white

    delta = 6 / 29
    threshold = delta**3
    factor = np.where(
        xyz > threshold,
        np.cbrt(xyz),
        (xyz / (3 * delta**2)) + (4 / 29),
    )

    l_channel = (116 * factor[..., 1]) - 16
    a_channel = 500 * (factor[..., 0] - factor[..., 1])
    b_channel = 200 * (factor[..., 1] - factor[..., 2])
    return np.stack([l_channel, a_channel, b_channel], axis=-1)
