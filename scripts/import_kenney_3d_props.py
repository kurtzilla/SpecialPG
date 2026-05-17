#!/usr/bin/env python3
"""Copy curated Kenney glTF/GLB props into res://art/3d/props/ and write art/3d/manifest.json."""

from __future__ import annotations

import json
import os
import shutil
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
MANIFEST_PATH = REPO / "scripts" / "kenney_manifest.json"
OUT_MANIFEST = REPO / "src" / "Godot" / "art" / "3d" / "manifest.json"
OUT_PROPS = REPO / "src" / "Godot" / "art" / "3d" / "props"


def kenney_root(manifest: dict) -> Path:
    env = os.environ.get("KENNEY_ASSETS_ROOT", "").strip()
    if env:
        return Path(env)
    return Path(manifest.get("kenney_assets_root_default", "D:/source/KenneyAssets"))


def main() -> None:
    manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
    root = kenney_root(manifest)
    packs = manifest["packs"]
    shipped = []
    for prop in manifest.get("props_3d", []):
        pack_id = prop["pack"]
        pack = packs[pack_id]
        gltf_dir = root / pack["gltf_dir"].replace("/", os.sep)
        src_name = prop["gltf"]
        src = gltf_dir / src_name
        if not src.is_file():
            raise FileNotFoundError(src)
        dest_dir = OUT_PROPS / prop["id"]
        dest_dir.mkdir(parents=True, exist_ok=True)
        dest = dest_dir / src_name
        shutil.copy2(src, dest)
        if src.suffix.lower() == ".gltf":
            bin_src = src.with_suffix(".bin")
            if bin_src.is_file():
                shutil.copy2(bin_src, dest_dir / bin_src.name)
        rel = f"res://art/3d/props/{prop['id']}/{src_name}"
        shipped.append({**prop, "resource_path": rel})
        print(f"Copied {src} -> {dest}")

    OUT_MANIFEST.parent.mkdir(parents=True, exist_ok=True)
    OUT_MANIFEST.write_text(
        json.dumps({"schema": 1, "props": shipped}, indent=2) + "\n",
        encoding="utf-8",
    )
    print(f"Wrote {OUT_MANIFEST}")


if __name__ == "__main__":
    main()
