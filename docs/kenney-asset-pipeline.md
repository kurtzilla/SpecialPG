# Kenney asset pipeline

How third-party **Kenney** art (CC0) enters SpecialPG: local source bundles, repo scripts, committed outputs, and Godot import settings.

**See also:** [terrain-art-import.md](terrain-art-import.md) (2D atlas layout), [architecture.md](architecture.md) (melange / terrain rendering).

---

## License and attribution

- Kenney assets are **[CC0](https://creativecommons.org/publicdomain/zero/1.0/)** (public domain). Attribution is appreciated but not required.
- Credit line for releases: **"Contains Kenney assets (CC0) from [kenney.nl](https://kenney.nl)"** plus pack names used.
- Each pack includes `License.txt` under your Kenney install, for example:
  - `...\Nature Kit (320 assets)\License.txt`
  - `...\Weapon Pack (100 assets)\License.txt`

**Do not commit** full Kenney distribution folders to git (multi‑GB). Commit only:

- Processed atlases under `src/Godot/art/terrain|decor|entities/`
- Curated 3D files under `src/Godot/art/3d/props/`
- [`scripts/kenney_manifest.json`](../scripts/kenney_manifest.json) (source mapping)
- [`src/Godot/art/3d/manifest.json`](../src/Godot/art/3d/manifest.json) (shipped 3D resource paths)

---

## Source vs shipped

| Role | Location |
|------|----------|
| **Source root** (machine-local) | `KENNEY_ASSETS_ROOT` env var, or `kenney_assets_root=` in [`config.ini`](../src/Godot/config.ini), default `D:\source\KenneyAssets` |
| **Manifest** (repo) | [`scripts/kenney_manifest.json`](../scripts/kenney_manifest.json) |
| **2D outputs** (repo) | `terrain_atlas.png`, `decor_atlas.png`, `entity_atlas.png` |
| **3D outputs** (repo) | `src/Godot/art/3d/props/<id>/*.glb` (+ Godot `.import` sidecars after editor import) |
| **3D runtime manifest** (repo) | `src/Godot/art/3d/manifest.json` |

Packs referenced in the current manifest:

| Pack | Source path (under Kenney root) | Use |
|------|----------------------------------|-----|
| Nature Kit (KGA2) | `Kenney Game Assets 2 version 22/3D assets/Nature Kit (320 assets)` | Top-down sprites, trees/rocks (3D) |
| Weapon Pack (KGA2) | `Kenney Game Assets 2 version 22/3D assets/Weapon Pack (100 assets)` | Weapon sprites + GLB props |
| KGA3 3D kits | `Kenney Game Assets 3 version 30/3D assets/...` | **Tranche B** (documented only; not imported yet) |

---

## Phase 1 — 2D atlases

### Flow

1. Extract `sprites_topdown.zip` from Nature Kit (cached under `.kenney_extract/` in repo, gitignored).
2. Run [`scripts/pack_kenney_2d_atlases.py`](../scripts/pack_kenney_2d_atlases.py):
   - Reads [`kenney_manifest.json`](../scripts/kenney_manifest.json)
   - Resizes sprites to **64×64** (Factorio-style 1×1 tile)
   - Patches **decor** atlas (8 variants) and **terrain** 1×1 slots per category; keeps procedural 2×2 / 4×4 / Side strips from [`gen_terrain_placeholder_atlas.py`](../scripts/gen_terrain_placeholder_atlas.py)
   - Packs **entity** atlas from Weapon Pack `Sprites/Render/`
3. Restart the game (or reload resources). Runtime: [`DecorFloorLayer`](../src/Godot/Terrain/DecorFloorLayer.cs), [`TerrainChunkRasterizer`](../src/Godot/Terrain/TerrainChunkRasterizer.cs) unchanged.

### Regenerate (Windows)

```bat
set KENNEY_ASSETS_ROOT=D:\source\KenneyAssets
pip install pillow
python scripts\pack_kenney_2d_atlases.py
```

### Godot import (2D)

Same as [terrain-art-import.md](terrain-art-import.md): **Nearest** filter, no mipmaps, lossless compression.

---

## Phase 2 — 3D props (hybrid melange)

### Flow

1. Curated entries in `kenney_manifest.json` → `props_3d` (small allowlist, not full Nature Kit).
2. Run [`scripts/import_kenney_3d_props.py`](../scripts/import_kenney_3d_props.py) to copy `.glb` / `.gltf` into `art/3d/props/<id>/`.
3. Open Godot once so imports generate `.import` sidecars.
4. Enable `decor_use_3d=true` in [`config.ini`](../src/Godot/config.ini).
5. [`Prop3DLayer`](../src/Godot/Terrain/Prop3DLayer.cs) (under `Main/Prop3D`) spawns meshes at decor scatter cells using the same grid→world mapping as [`InteractionRay3D`](../src/Godot/InteractionRay3D.cs) (2D cell center → XZ plane, Y up).

### Scale convention

- **1 grid cell** = `cell_size_px` (default **64**) on the 2D shell.
- **1 Godot unit** ≈ 1 cell width on the pick plane unless `scale` in `art/3d/manifest.json` overrides per prop.

### Regenerate 3D

```bat
set KENNEY_ASSETS_ROOT=D:\source\KenneyAssets
python scripts\import_kenney_3d_props.py
```

Then reimport in Godot editor if paths change.

---

## What we do not do (yet)

- Replace 2D terrain CPU bake with 3D tile meshes (melange variant C).
- Import entire KGA3 `3D assets` trees into the repo.
- Auto-generate Factorio-style **Side** transition art from Kenney tiles.

---

## KGA3 tranche B

After Nature Kit 3D props are stable, add rows to `kenney_manifest.json` for one KGA3 kit (e.g. **Retro Medieval Kit** or duplicate **Nature Kit**). Same glTF import rules and `import_kenney_3d_props.py`.

---

## Revision log

- **REV 59** — Kenney 2D atlases + pipeline doc + pack scripts.
- **REV 60** — Kenney 3D props + `Prop3DLayer` + `decor_use_3d`.
