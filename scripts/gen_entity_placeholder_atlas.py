#!/usr/bin/env python3
"""Generate res://art/entities/entity_atlas.png (placeholder entity kinds)."""

from __future__ import annotations

import struct
import zlib
from pathlib import Path

TILE = 32
# Prop, Actor (debug prop only; player is ShellPlayer)
WIDTH = 2 * TILE
HEIGHT = TILE
OUT = Path(__file__).resolve().parents[1] / "src" / "Godot" / "art" / "entities" / "entity_atlas.png"

KINDS = [
    ((220, 80, 80), "Actor"),
    ((200, 160, 60), "Prop"),
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


def fill_tile(pixels: bytearray, width: int, x0: int, rgb: tuple[int, int, int]) -> None:
    for py in range(TILE):
        for px in range(TILE):
            cx, cy = px - TILE // 2, py - TILE // 2
            inside = abs(cx) + abs(cy) < TILE // 2 - 2
            r, g, b = rgb
            if inside:
                r, g, b = min(255, r + 40), min(255, g + 40), min(255, b + 40)
            i = (py * width + x0 + px) * 4
            pixels[i : i + 4] = bytes((r, g, b, 255 if inside or px % 8 == 0 or py % 8 == 0 else 200))


def main() -> None:
    pixels = bytearray(WIDTH * HEIGHT * 4)
    for i, (rgb, _) in enumerate(KINDS):
        fill_tile(pixels, WIDTH, i * TILE, rgb)
    write_png(OUT, WIDTH, HEIGHT, bytes(pixels))
    print(f"Wrote {OUT} ({WIDTH}x{HEIGHT})")


if __name__ == "__main__":
    main()
