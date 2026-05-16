# Terrain and surface art import

Godot atlases used by the shell live under:

| Path | Purpose |
|------|---------|
| `res://art/terrain/terrain_atlas.png` | Main terrain patches (1×1, 2×2, 4×4 per category) |
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

Water rows in the terrain atlas use four variant columns as animation frames when `terrain_water_animate=true` in [`config.ini`](../src/Godot/config.ini).

## Hand-authored art

When replacing placeholders, keep tile sizes aligned with [`TerrainAtlasCatalog`](../src/Godot/Terrain/TerrainAtlasCatalog.cs) (32px 1×1, 64px 2×2, 128px 4×4 per category band). A future JSON rect catalog is out of scope for the current rollout.
