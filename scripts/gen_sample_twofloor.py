"""Regenerate res://maps/sample_twofloor.json.

Default W×H is a repo-sized demo (larger than the old 256×128); shell/config defaults
for empty fallback maps are 2048×1024 — override W,H here if you need a full-size JSON.
"""
import json
from pathlib import Path

# Committed sample: big enough to stress-load, small enough for git; bump W,H locally for full maps.
W, H = 512, 384
OUT = Path(__file__).resolve().parent.parent / "src" / "Godot" / "maps" / "sample_twofloor.json"


def floor_cells(z: int) -> list[dict]:
    cells = []
    for y in range(H):
        for x in range(W):
            kind = ((x + y + z) % 2) + 1
            cells.append({"tileKind": kind, "flags": 0, "variant": 0})
    return cells


def main() -> None:
    # One blocked cell for walkability tests. Do not use map center: shell spawns at (W/2,H/2),
    # so a center block forces a nearby spawn and often leaves the blocked cell immediately
    # east/south of the player (looks like a traversal bug).
    bx, by = W - 1, H - 1
    z0 = floor_cells(0)
    i = by * W + bx
    z0[i] = {"tileKind": z0[i]["tileKind"], "flags": 1, "variant": 0}  # TileFlags.Blocked = 1
    z1 = floor_cells(1)
    dto = {
        "width": W,
        "height": H,
        "floors": [
            {"z": 0, "cells": z0},
            {"z": 1, "cells": z1},
        ],
        "verticalLinks": [
            {
                "fromX": 0,
                "fromY": 0,
                "fromZ": 0,
                "toX": 0,
                "toY": 0,
                "toZ": 1,
                "kind": "stairs",
                "oneWay": False,
            }
        ],
    }
    text = json.dumps(dto, separators=(",", ":"))
    OUT.write_text(text, encoding="utf-8")
    print(f"Wrote {OUT} ({OUT.stat().st_size // 1024} KiB) size {W}x{H}")


if __name__ == "__main__":
    main()
