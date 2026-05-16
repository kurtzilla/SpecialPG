#!/usr/bin/env python3
"""Generate res://art/decor/decor_atlas.png (8× 32px decor variants)."""

from __future__ import annotations

import struct
import zlib
from pathlib import Path

VARIANTS = 8
TILE = 32
WIDTH = VARIANTS * TILE
HEIGHT = TILE
OUT = Path(__file__).resolve().parents[1] / "src" / "Godot" / "art" / "decor" / "decor_atlas.png"

COLORS = [
    (60, 140, 55),
    (45, 120, 48),
    (90, 150, 70),
    (70, 110, 45),
    (100, 130, 60),
    (55, 95, 40),
    (80, 125, 55),
    (50, 105, 50),
]


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


def main() -> None:
    pixels = bytearray(WIDTH * HEIGHT * 4)
    for v in range(VARIANTS):
        rgb = COLORS[v % len(COLORS)]
        x0 = v * TILE
        for py in range(TILE):
            for px in range(TILE):
                edge = px < 2 or py < 2 or px >= TILE - 2 or py >= TILE - 2
                r = min(255, rgb[0] + (30 if edge else 0))
                g = min(255, rgb[1] + (20 if edge else 0))
                b = min(255, rgb[2] + (10 if edge else 0))
                i = ((py * WIDTH) + (x0 + px)) * 4
                pixels[i : i + 4] = bytes((r, g, b, 255))
    write_png(OUT, WIDTH, HEIGHT, bytes(pixels))
    print(f"Wrote {OUT} ({WIDTH}x{HEIGHT})")


if __name__ == "__main__":
    main()
