# Agent pitfalls (SpecialPG)

Regression-oriented notes for AI agents and contributors. **Not** a substitute for [architecture.md](architecture.md) (contracts) or [shell-feature-revision-log.md](shell-feature-revision-log.md) (full human changelog).

**Maintenance:** Any bug fixed twice → add a bullet here **or** a Core test in `tests/SpecialPG.Core.Tests`. Link to architecture when the rule is a contract.

**Session start:** Copy [agent-session-handoff.md](agent-session-handoff.md) into your prompt for focused work.

---

## Chunk seams

| Symptom | Likely cause |
|---------|----------------|
| Visible line at 32×32 chunk boundary after map edit | Only the local chunk was rebuilt; neighbor chunks still hold stale margin pixels |
| Mismatched main patches across chunk border | Patch anchors not globally aligned (`gx % 4 == 0` for 4×4, `gx % 2 == 0` for 2×2) |
| Transition wrong at edge | Planning rect missing **1-cell margin** (`TransitionMarginCells` in `TerrainChunkRasterizer`) |

**Fix / code:** Tile and editor edits must go through [`MarkMapCellDirty`](../src/Godot/GameRoot.cs) (marks terrain **and** decor in a **3×3 chunk** neighborhood). Do not call single-chunk dirty helpers without that fan-out.

**Verify (manual — Shell only):** Edit a cell on a chunk edge (global coordinate where `gx % 32 == 0` or `31`). Pan camera across the seam; no color or sprite discontinuity.

**See:** [architecture.md § Terrain rendering](architecture.md#terrain-rendering-shell), REV 55 in revision log.

---

## Coast / shoreline (removed)

**Tile transitions** (`TileTransitionPlanner` Side sprites) and **per-pixel shoreline** (`TerrainShorelinePainter` / mask) were removed from the shell bake path. Chunk terrain is **main patches + color or sprite bake only**. Low-land coast tint still comes from [`TerrainVisualColor`](../src/Core/Maps/TerrainVisualColor.cs) in color mode — that is not a separate config flag.

`TileTransitionPlanner` remains in Core for tests/future use but is **not called** from [`TerrainChunkRasterizer`](../src/Godot/Terrain/TerrainChunkRasterizer.cs).

---

## config.ini and ShellAppConfig

- Most terrain/visual flags are read at **boot** — **restart Godot** after editing [`config.ini`](../src/Godot/config.ini).
- **Exception:** WASD sliders on the right preset stack persist at runtime via `PersistWasdMovementSettings`.
- New flags in [`ShellAppConfig`](../src/Godot/ShellAppConfig.cs): document restart requirement here and add an inline comment in `config.ini`.

---

## Atlas / Play crash

- `terrain_use_sprites=true` requires atlas layout per [`TerrainAtlasCatalog`](../src/Godot/Terrain/TerrainAtlasCatalog.cs) (see [terrain-art-import.md](terrain-art-import.md)).
- Wrong/missing atlas → safe fallback to color bake; native blit crash if size/layout mismatch on some builds.
- Regenerate: `python scripts/gen_terrain_placeholder_atlas.py` from repo root.

**Bisect:** `terrain_use_sprites=false` → Play should succeed.

---

## Terrain raster pass order

Per chunk bake:

1. **Main** patches — `TileMainPatchPlanner` (with 1-cell planning margin at chunk edges)
2. **Paint** — atlas sprites when `terrain_use_sprites=true`, else low-res color fill scaled by `cell_size_px`

---

## Perf and flicker

- Do not call terrain `MarkAllDirty` from HUD or per WASD sub-step (REV 38).
- `terrain_water_animate=true`: only **water-bearing** visible chunks dirty on the ~200 ms tick (REV 57).
- Grid/terrain redraw: cull window uses **expansion-only** compare vs last draw (REV 44) — avoid `QueueRedraw` on ±1 cell camera jitter.
- Profile: `profile_shell_draw=true` in config or `SPECIALPG_PROFILE_SHELL_DRAW=1`; read `terrain_chunk_rebuild` / `surface_sync` / `grid_draw` buckets in [debugging.md](debugging.md).

---

## Camera and movement

- Core foot / camera follow: **`AuthoritativeFootWorld`**, not lerped `ShellPlayer.Position` (REV 61 — avoids turn pop).
- WASD: discrete steps in **`_PhysicsProcess`**; `ShellPlayer` lerps visuals in `_Process` only.
- Water surface (elevation below threshold) is **never** walkable (`TileTraversal`).

---

## Coordinates

- Core **Y** increases **north** on the floor plane.
- Godot screen **Y** grows downward — Shell flips via `GetGridOrigin` / `ChunkPatchOriginY` (north = top of viewport).
- Do not assume Godot node `Position.Y` equals Core `Y` without the documented mapping.

---

## Core vs Shell

| Core (`src/Core`) | Shell (`src/Godot`) |
|-------------------|---------------------|
| Planners, masks, traversal, integrity | Chunk views, rasterize, atlas, HUD |
| .NET BCL only — **no Godot types** | Godot APIs only here |

New rendering rules belong in Core planners when possible; add tests before Shell wiring.

---

## Map and seeds

- Procedural generation must stay **deterministic** from seed — use [`NoiseSeedUtility`](../src/Core/Maps/Noise/NoiseSeedUtility.cs), not `HashCode` for seed mixing.
- [`MapIntegrity`](../src/Core/Maps/MapIntegrity.cs) validates full maps on load; local edits use `ValidateModification` / `ValidateVerticalLink` where applicable.

## Global origin (0,0) and spawn

- Bounded procedural maps use a **centered** min corner (`minX = minY = -(size/2)`) so **global tile (0,0)** is the board center, not a corner.
- [`ProceduralWorldMapGenerator`](../src/Core/Maps/ProceduralWorldMapGenerator.cs) **pans noise** via [`LandmassNoiseAlignment`](../src/Core/Maps/LandmassNoiseAlignment.cs) so the largest landmass centroid sits near (0,0); `WorldMap.ProceduralLandmassAligned` is set when that path runs.
- [`OriginWalkabilityPatch`](../src/Core/Maps/OriginWalkabilityPatch.cs) at (0,0) is a **small safety margin** (`startup_origin_patch_chebyshev_radius`, default 1–2), not the main land guarantee. [`LandmassBridgeToLargestComponent`](../src/Core/Maps/LandmassBridgeToLargestComponent.cs) runs only if origin is still off the LCC after alignment + margin.
- **Shell spawn:** when `ProceduralLandmassAligned`, [`BootstrapWorldFromMap`](../src/Godot/GameRoot.cs) keeps the actor at **global (0,0)** and only nudges **sub-tile** position inside that cell — do not fall back to map-center LCC search unless alignment failed.
- **Sub-tile:** `ForceLand` at a tile does not guarantee sub-tile walkability; [`ForceLandWalkMargin`](../src/Core/Maps/ForceLandWalkMargin.cs) remains after the origin patch.

---

## Cold start load time

- Raising **`default_map_width_cells` / `default_map_height_cells`** dominates F5 time (full procedural fill is synchronous).
- **DebugWait** adds up to **2 s** on F5 with debugger attached — not map generation. See [debugging.md § Cold start](debugging.md#cold-start--f5-load-time-15-seconds).
- Terrain chunk sync runs from **`GameRoot._Draw`** on cull/zoom change; first pan/zoom after load may hitch while visible chunks bake.

---

## Scope creep (game design)

- Do **not** implement Palladium combat, stat blocks, spells, or O.C.C. math while working on Shell/terrain unless the handoff names **Phase 5+** — see [game/scope-and-phases.md](game/scope-and-phases.md).
- Faction names, region labels, and tone in data/docs are fine; **rules text** from PDFs stays out of the repo — use [game/rifts-source-index.md](game/rifts-source-index.md) to find which book to open locally.
- **Architecture wins** on coordinates and saves; **game vision wins** on whether a feature belongs in the current phase.

---

## Related docs

- [architecture.md](architecture.md) — contracts
- [game/vision.md](game/vision.md) — product direction (Rifts hybrid)
- [game/scope-and-phases.md](game/scope-and-phases.md) — phased goals
- [debugging.md](debugging.md) — F5, coast A/B, profiling
- [terrain-art-import.md](terrain-art-import.md) — atlas bands, 64px tile
- [agent-session-handoff.md](agent-session-handoff.md) — per-session template
- `.cursor/rules/*.mdc` — Cursor agent rules
