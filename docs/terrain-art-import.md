# Terrain and surface art import

**Kenney CC0 packs:** see [kenney-asset-pipeline.md](kenney-asset-pipeline.md) for source paths, rebuild commands, and what is committed vs kept local.

Godot atlases used by the shell live under:

| Path | Purpose |
|------|---------|
| `res://art/terrain/terrain_atlas.png` | Main terrain patches (1×1, 2×2, 4×4 per category) + Side transition strip |
| `res://art/decor/decor_atlas.png` | Procedural decor scatter |
| `res://art/entities/entity_atlas.png` | Entity / prop sprites |

## Import preset (pixel art)

In the Godot editor, select each PNG and set:

- **Compress → Mode:** Lossless (or Uncompressed for debugging)
- **Mipmaps:** Off
- **Filter:** Nearest
- **Repeat:** Disabled

Chunk and sprite nodes already use **Nearest** filtering at runtime ([`TerrainChunkView`](../src/Godot/Terrain/TerrainChunkView.cs), decor/entity sprites).

## Regenerating placeholders

| Script | Output |
|--------|--------|
| [`scripts/gen_terrain_placeholder_atlas.py`](../scripts/gen_terrain_placeholder_atlas.py) | `terrain_atlas.png` |
| [`scripts/gen_decor_placeholder_atlas.py`](../scripts/gen_decor_placeholder_atlas.py) | `decor_atlas.png` |
| [`scripts/gen_entity_placeholder_atlas.py`](../scripts/gen_entity_placeholder_atlas.py) | `entity_atlas.png` |

Run from repo root, e.g. `python scripts/gen_terrain_placeholder_atlas.py`. Restart the game or reload resources after replacing PNGs.

**Required terrain atlas size:** must match [`TerrainAtlasCatalog`](../src/Godot/Terrain/TerrainAtlasCatalog.cs) (`CategoryBandHeight` × category row count × **1024** width). Wrong dimensions fall back to color bake. See [debugging.md](debugging.md) if Play fails with `terrain_use_sprites=true`.

Water rows in the terrain atlas use four variant columns as animation frames when `terrain_water_animate=true` in [`config.ini`](../src/Godot/config.ini).

## Hand-authored art

When replacing placeholders, keep tile sizes aligned with [`TerrainAtlasCatalog`](../src/Godot/Terrain/TerrainAtlasCatalog.cs) (Factorio-style **64px** 1×1, **128px** 2×2, **256px** 4×4, **64px** Side per category band; `cell_size_px=64` in config). The shell bake path uses **main patches only** (Side strips remain in the atlas for future use). A future JSON rect catalog is out of scope for the current rollout.
