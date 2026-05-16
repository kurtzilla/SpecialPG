#!/usr/bin/env python3
"""Generate res://art/terrain/terrain_atlas.png (1x1, 2x2, 4x4 main patches per category)."""

from __future__ import annotations

import struct
import zlib
from pathlib import Path

VARIANTS = 4
CATEGORIES = 10
STRIP_1X1 = 32
STRIP_2X2 = 64
STRIP_4X4 = 128
STRIP_SIDE = 32
BAND_HEIGHT = STRIP_1X1 + STRIP_2X2 + STRIP_4X4 + STRIP_SIDE
WIDTH = VARIANTS * 128
HEIGHT = CATEGORIES * BAND_HEIGHT

ROW_COLORS: list[tuple[int, int, int]] = [
    (13, 46, 209),
    (46, 122, 252),
    (153, 158, 132),
    (82, 184, 97),
    (140, 128, 102),
    (51, 115, 61),
    (120, 150, 110),
    (90, 170, 100),
    (13, 46, 209),
    (128, 133, 140),
]

OUT = Path(__file__).resolve().parents[1] / "src" / "Godot" / "art" / "terrain" / "terrain_atlas.png"


def png_chunk(tag: bytes, data: bytes) -> bytes:
    crc = zlib.crc32(tag + data) & 0xFFFFFFFF
    return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", crc)


def write_png(path: Path, width: int, height: int, rgba: bytes) -> None:
    ihdr = struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0)
    raw = b"".join(b"\x00" + rgba[y * width * 4 : (y + 1) * width * 4] for y in range(height))
    idat = zlib.compress(raw, 9)
    payload = (
        b"\x89PNG\r\n\x1a\n"
        + png_chunk(b"IHDR", ihdr)
        + png_chunk(b"IDAT", idat)
        + png_chunk(b"IEND", b"")
    )
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(payload)


def fill_rect(pixels: bytearray, width: int, x: int, y: int, w: int, h: int, rgb: tuple[int, int, int], shade: float) -> None:
    r = min(255, int(rgb[0] * shade))
    g = min(255, int(rgb[1] * shade))
    b = min(255, int(rgb[2] * shade))
    for py in range(h):
        for px in range(w):
            ix = x + px
            iy = y + py
            if ix < 0 or iy < 0 or ix >= width or iy >= HEIGHT:
                continue
            i = (iy * width + ix) * 4
            pixels[i : i + 4] = bytes((r, g, b, 255))


def checker_subshade(px: int, py: int, base: float, cell: int) -> float:
    cell_x = px // cell
    cell_y = py // cell
    return base * (0.78 if (cell_x + cell_y) % 2 == 0 else 1.0)


def stroke_rect_border(
    pixels: bytearray, width: int, x: int, y: int, w: int, h: int, thickness: int = 2
) -> None:
    """White inset border so sprite bake is obvious vs flat color fill."""
    for t in range(thickness):
        fill_rect(pixels, width, x + t, y + t, w - 2 * t, 1, (255, 255, 255), 1.0)
        fill_rect(pixels, width, x + t, y + h - 1 - t, w - 2 * t, 1, (255, 255, 255), 1.0)
        fill_rect(pixels, width, x + t, y + t, 1, h - 2 * t, (255, 255, 255), 1.0)
        fill_rect(pixels, width, x + w - 1 - t, y + t, 1, h - 2 * t, (255, 255, 255), 1.0)


def variant_dot(pixels: bytearray, width: int, x: int, y: int, variant: int) -> None:
    """Small magenta marker per variant column (dev placeholder cue)."""
    ox = x + 4 + variant * 3
    oy = y + 4
    fill_rect(pixels, width, ox, oy, 2, 2, (255, 0, 220), 1.0)


def main() -> None:
    pixels = bytearray(WIDTH * HEIGHT * 4)
    for row in range(CATEGORIES):
        base = ROW_COLORS[row]
        band_y = row * BAND_HEIGHT
        is_water = row in (0, 1)
        for v in range(VARIANTS):
            shade = (0.72 + v * 0.09) if is_water else (1.0 - v * 0.06)
            x1, y1 = v * 32, band_y
            fill_rect(pixels, WIDTH, x1, y1, 32, 32, base, shade)
            stroke_rect_border(pixels, WIDTH, x1, y1, 32, 32, 2)
            variant_dot(pixels, WIDTH, x1, y1, v)
            x2 = v * 64
            y2 = band_y + STRIP_1X1
            for py in range(STRIP_2X2):
                for px in range(STRIP_2X2):
                    s = checker_subshade(px, py, shade, 16)
                    fill_rect(pixels, WIDTH, x2 + px, y2 + py, 1, 1, base, s)
            stroke_rect_border(pixels, WIDTH, x2, y2, STRIP_2X2, STRIP_2X2, 2)
            variant_dot(pixels, WIDTH, x2, y2, v)
            x4 = v * 128
            y4 = band_y + STRIP_1X1 + STRIP_2X2
            for py in range(STRIP_4X4):
                for px in range(STRIP_4X4):
                    s = checker_subshade(px, py, shade, 16)
                    fill_rect(pixels, WIDTH, x4 + px, y4 + py, 1, 1, base, s)
            stroke_rect_border(pixels, WIDTH, x4, y4, STRIP_4X4, STRIP_4X4, 3)
            variant_dot(pixels, WIDTH, x4, y4, v)

        y_side = band_y + STRIP_1X1 + STRIP_2X2 + STRIP_4X4
        water_rgb = ROW_COLORS[0]
        for facing in range(4):
            for v in range(VARIANTS):
                x_side = facing * VARIANTS * STRIP_1X1 + v * STRIP_1X1
                if row in (0, 1):
                    blend = (0.35, 0.55, 0.85)
                elif row in (2, 3, 4):
                    t = facing / 3.0
                    blend = (
                        int(base[0] * (1 - t) + water_rgb[0] * t * 0.4),
                        int(base[1] * (1 - t) + water_rgb[1] * t * 0.4),
                        int(base[2] * (1 - t) + water_rgb[2] * t * 0.4),
                    )
                else:
                    blend = base
                fill_rect(pixels, WIDTH, x_side, y_side, STRIP_1X1, STRIP_SIDE, blend, 0.9 - v * 0.05)
                stroke_rect_border(pixels, WIDTH, x_side, y_side, STRIP_1X1, STRIP_SIDE, 1)

    write_png(OUT, WIDTH, HEIGHT, bytes(pixels))
    print(f"Wrote {OUT} ({WIDTH}x{HEIGHT})")


if __name__ == "__main__":
    main()
