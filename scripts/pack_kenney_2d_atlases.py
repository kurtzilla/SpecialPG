#!/usr/bin/env python3
"""Pack Kenney 2D sprites into SpecialPG atlases per scripts/kenney_manifest.json."""

from __future__ import annotations

import json
import os
import shutil
import zipfile
from pathlib import Path

try:
    from PIL import Image
except ImportError as e:
    raise SystemExit("Pillow required: pip install pillow") from e

REPO = Path(__file__).resolve().parents[1]
MANIFEST_PATH = REPO / "scripts" / "kenney_manifest.json"
OUT_TERRAIN = REPO / "src" / "Godot" / "art" / "terrain" / "terrain_atlas.png"
OUT_DECOR = REPO / "src" / "Godot" / "art" / "decor" / "decor_atlas.png"
OUT_ENTITY = REPO / "src" / "Godot" / "art" / "entities" / "entity_atlas.png"
EXTRACT_CACHE = REPO / ".kenney_extract"

# Match TerrainAtlasCatalog / gen_terrain_placeholder_atlas.py
VARIANTS = 4
CATEGORIES = 10
TILE = 64
STRIP_1X1 = TILE
STRIP_2X2 = TILE * 2
STRIP_4X4 = TILE * 4
STRIP_SIDE = TILE
BAND_HEIGHT = STRIP_1X1 + STRIP_2X2 + STRIP_4X4 + STRIP_SIDE
TERRAIN_W = VARIANTS * STRIP_4X4
TERRAIN_H = CATEGORIES * BAND_HEIGHT

CATEGORY_ROW = {
    "DeepWater": 0,
    "ShallowWater": 1,
    "Coast": 2,
    "Land": 3,
    "Hill": 4,
    "Blocked": 5,
    "ForcedLandCoastBlend": 6,
    "ForcedLandOverride": 7,
    "ForcedWater": 8,
    "Empty": 9,
}


def kenney_root(manifest: dict) -> Path:
    env = os.environ.get("KENNEY_ASSETS_ROOT", "").strip()
    if env:
        return Path(env)
    ini_path = REPO / "src" / "Godot" / "config.ini"
    if ini_path.exists():
        for line in ini_path.read_text(encoding="utf-8", errors="replace").splitlines():
            line = line.strip()
            if line.startswith("kenney_assets_root="):
                val = line.split("=", 1)[1].strip().strip('"')
                if val:
                    return Path(val)
    return Path(manifest.get("kenney_assets_root_default", "D:/source/KenneyAssets"))


def ensure_nature_topdown(root: Path, manifest: dict) -> Path:
    pack = manifest["packs"]["nature_kit_kga2"]
    zip_rel = pack["sprites_topdown_zip"]
    zip_path = root / zip_rel.replace("/", os.sep)
    dest = EXTRACT_CACHE / "nature_topdown"
    if not dest.exists() or not any(dest.glob("*.png")):
        dest.mkdir(parents=True, exist_ok=True)
        if not zip_path.is_file():
            raise FileNotFoundError(f"Missing {zip_path}")
        with zipfile.ZipFile(zip_path, "r") as zf:
            zf.extractall(dest)
    return dest


def load_sprite(src: Path, tile_px: int) -> Image.Image:
    img = Image.open(src).convert("RGBA")
    if img.size != (tile_px, tile_px):
        img = img.resize((tile_px, tile_px), Image.Resampling.NEAREST)
    return img


def blit(dst: Image.Image, src: Image.Image, x: int, y: int) -> None:
    dst.paste(src, (x, y), src)


def build_terrain_atlas(root: Path, manifest: dict, nature_dir: Path) -> Image.Image:
    # Start from procedural placeholder for 2x2/4x4/side strips
    import subprocess

    subprocess.run(
        [os.environ.get("PYTHON", "python"), str(REPO / "scripts" / "gen_terrain_placeholder_atlas.py")],
        check=True,
        cwd=REPO,
    )
    atlas = Image.open(OUT_TERRAIN).convert("RGBA")
    cfg = manifest["terrain_atlas_main1x1"]
    tile_px = int(cfg["tile_px"])
    land_fallback = None
    for entry in cfg["categories"]:
        cat = entry["category"]
        variant = int(entry.get("variant", 0))
        row = CATEGORY_ROW[cat]
        png = nature_dir / entry["file"]
        if not png.is_file():
            raise FileNotFoundError(png)
        sprite = load_sprite(png, tile_px)
        if cat == "Land":
            land_fallback = sprite
        x = variant * tile_px
        y = row * BAND_HEIGHT
        blit(atlas, sprite, x, y)
    if cfg.get("fill_remaining_variants_from_category_land") and land_fallback is not None:
        for row in range(CATEGORIES):
            for v in range(1, VARIANTS):
                x = v * tile_px
                y = row * BAND_HEIGHT
                blit(atlas, land_fallback, x, y)
    return atlas


def build_decor_atlas(nature_dir: Path, manifest: dict) -> Image.Image:
    cfg = manifest["decor_atlas"]
    tile_px = int(cfg["tile_px"])
    n = len(cfg["variants"])
    atlas = Image.new("RGBA", (n * tile_px, tile_px), (0, 0, 0, 0))
    for entry in cfg["variants"]:
        idx = int(entry["index"])
        png = nature_dir / entry["file"]
        if not png.is_file():
            raise FileNotFoundError(png)
        blit(atlas, load_sprite(png, tile_px), idx * tile_px, 0)
    return atlas


def build_entity_atlas(root: Path, manifest: dict) -> Image.Image:
    cfg = manifest["entity_atlas"]
    tile_px = int(cfg["tile_px"])
    weapon_dir = root / manifest["packs"]["weapon_pack_kga2"]["sprites_render_dir"].replace("/", os.sep)
    atlas = Image.new("RGBA", (2 * tile_px, tile_px), (0, 0, 0, 0))
    for i, slot in enumerate(cfg["slots"]):
        png = weapon_dir / slot["file"]
        if not png.is_file():
            raise FileNotFoundError(png)
        blit(atlas, load_sprite(png, tile_px), i * tile_px, 0)
    return atlas


def main() -> None:
    manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
    root = kenney_root(manifest)
    if not root.is_dir():
        raise SystemExit(f"KENNEY_ASSETS_ROOT not found: {root}")

    nature_dir = ensure_nature_topdown(root, manifest)
    print(f"Kenney root: {root}")
    print(f"Nature topdown: {nature_dir}")

    terrain = build_terrain_atlas(root, manifest, nature_dir)
    OUT_TERRAIN.parent.mkdir(parents=True, exist_ok=True)
    terrain.save(OUT_TERRAIN)
    print(f"Wrote {OUT_TERRAIN} ({terrain.size[0]}x{terrain.size[1]})")

    decor = build_decor_atlas(nature_dir, manifest)
    decor.save(OUT_DECOR)
    print(f"Wrote {OUT_DECOR} ({decor.size[0]}x{decor.size[1]})")

    entity = build_entity_atlas(root, manifest)
    entity.save(OUT_ENTITY)
    print(f"Wrote {OUT_ENTITY} ({entity.size[0]}x{entity.size[1]})")


if __name__ == "__main__":
    main()
