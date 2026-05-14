# SpecialPG architecture

This document is the **source of truth** for coordinate conventions, floor slicing, and map connectivity. If game logic in `src/Core` changes any of these rules, update this file first (see `.cursor/.cursorrules.md`).

**Rendering direction:** Shell presentation follows the **melange view** (orthographic top-down + tall / three-quarter readability). See [melange-view-pattern.md](melange-view-pattern.md), [visual-direction-guide.md](visual-direction-guide.md), and [view-rendering-discussion.md](view-rendering-discussion.md).

## Core + Shell

- **`src/Core`**: Pure C# using the .NET Base Class Library only. No Godot types or assemblies.
- **`src/Godot`**: The **Shell**—the only place Godot APIs and generated game code live. The Shell reads Core data, renders, and forwards input; simulation rules stay in Core.

**World state:** Actor pose and grid intents (use vertical link, debug floor cycle) live in Core [`WorldState`](../src/Core/Maps/WorldState.cs). The Shell may drive **continuous** world position for the avatar; Core’s `(ActorX, ActorY, ActorZ)` is updated as the **sampled foot cell** via `SetActorCellFromShell` so tiles, links, and integrity stay grid-aligned. **Entities** (props, NPCs, machines—anything not authored as map tiles) live in [`EntityStore`](../src/Core/Maps/EntityStore.cs) on that `WorldState`, keyed by [`EntityId`](../src/Core/Maps/EntityId.cs) with chunk buckets `(cx, cy, z)` aligned to the active [`WorldMap`](../src/Core/Maps/WorldMap.cs); serialize separately from terrain via [`EntityStoreJson`](../src/Core/Maps/EntityStoreJson.cs).

**Shell tuning:** [`config.ini`](../src/Godot/config.ini) (loaded by [`ShellAppConfig`](../src/Godot/ShellAppConfig.cs)) supplies values such as `cell_size_px`, default map dimensions (used for bounded bootstrap / procedural defaults), **`wasd_steps_per_second`** (discrete WASD repeat rate), **`wasd_max_sub_steps_per_physics_frame`** (caps burst steps on long frames), zoom limits, **`randomize_startup_seed`**, and optional **`profile_shell_draw`**, without recompiling. Bounded procedural fills use [`ProceduralWorldMapGenerator`](../src/Core/Maps/ProceduralWorldMapGenerator.cs), which ends with [`ForceLandWalkMargin`](../src/Core/Maps/ForceLandWalkMargin.cs) so sub-tile walkability stays consistent one tile beyond `ForceLand` patches and the land-bridge path. The [`ShellPlayer`](../src/Godot/ShellPlayer.cs) node **lerps** its `Position` toward the authoritative sub-cell center each `_Process` so the camera scroll feels smoother than the Core step rate; Core sync and foot readouts use **`AuthoritativeFootWorld`**. **Physics cadence:** [`GameRoot`](../src/Godot/GameRoot.cs) drives WASD in **`_PhysicsProcess`**; Godot’s default **`physics_ticks_per_second`** is **60** unless you change it under **Project → Project Settings → Physics → Common** (or add `[physics]` keys in `project.godot`). Mismatches between physics rate and display refresh can exaggerate stepping; tune `wasd_steps_per_second` first, then physics FPS if needed. Window mode stays in `project.godot`. The checked-in [`sample_twofloor.json`](../src/Godot/maps/sample_twofloor.json) uses its own `width`/`height` from the file (kept smaller than those defaults so the asset stays tractable in git); bump dimensions in `scripts/gen_sample_twofloor.py` when you need a full-size sample on disk.

**Visibility:** The shell renders the entire active floor unconditionally — there is no fog-of-war, no reveal mask, and no shader overlay. Terrain tints come from [`TerrainVisualColor`](../src/Core/Maps/TerrainVisualColor.cs) sampled at world coordinates per cell.

**Shell HUD / pause:** [`ShellHudLayer`](../src/Godot/ShellHudLayer.cs) lives on `Main/ShellUi/ShellHudRoot` (a `CanvasLayer` sibling of `GridMap`, not under the pannable `Node2D`). `ShellUi` must keep **`follow_viewport_enabled = false`** so HUD stays **window-anchored**; if it is `true`, Godot ties the layer to the active `Camera2D` scroll and the UI drifts with the map. **ESC** is handled in [`GameRoot._UnhandledInput`](../src/Godot/GameRoot.cs) and toggles the centered pause menu on that layer (**Quit** at the top, **Resume** below); only **Quit** exits the app. [`GameRoot`](../src/Godot/GameRoot.cs) pushes boot line, feature RichText, and world XY labels through `ShellHudLayer`. Debug builds may still show the external-debug autoload overlay first; **ESC** there skips the wait (see addon README) before the pause menu behaves normally.

**Shell zoom (mouse wheel):** Wheel steps are handled in [`GameRoot`](../src/Godot/GameRoot.cs) via `_UnhandledInput` so controls that consume `_Input` still leave zoom to the shell after UI handling (see Godot unhandled-input order).

**Tiles:** [`TileData.Flags`](../src/Core/Maps/TileData.cs) uses bitmask constants in [`TileFlags`](../src/Core/Maps/TileFlags.cs) (e.g. **Blocked**). [`TileTraversal`](../src/Core/Maps/TileTraversal.cs) and [`VerticalLinkTraversal`](../src/Core/Maps/VerticalLinkTraversal.cs) gate movement and link use.

**Debug overlays:** Walkability / link / ray-pick / path **visualizations** are Shell-only (F5 + [`DebugChannelPanel`](../src/Godot/DebugChannelPanel.cs) toggles in `ShellHudRoot/LeftHudColumn`, same window layer as the feature HUD); they do not change Core simulation.

Performance: prefer **`struct`** types for dense, hot data (tiles, stats) and minimize GC allocations.

## Coordinate system (X, Y, Z)

The world is a **3D integer grid**. All logical positions use the same `(X, Y, Z)` triple in Core and Shell.

| Axis | Meaning |
|------|--------|
| **X** | Horizontal index along one edge of the floor plane (e.g. increasing **east**). |
| **Y** | Horizontal index along the other edge of the floor plane (e.g. increasing **north**). |
| **Z** | **Floor index**—discrete vertical layer. Higher `Z` is “upper” floors unless a specific map overrides elevation flavor. Not fractional height. |

**Tile identity**: A floor cell is addressed by `(X, Y)` **on** floor `Z`. Tile *payload* ([`TileCell`](../src/Core/Maps/TileCell.cs)) does not repeat `(X, Y, Z)` if the map storage already keys cells by position.

**Sub-tile (horizontal):** [`WorldState`](../src/Core/Maps/WorldState.cs) also tracks [`ActorSubX`](../src/Core/Maps/WorldState.cs) / [`ActorSubY`](../src/Core/Maps/WorldState.cs) within the current cell, indices `0 .. SubTileGrid.Resolution-1` (see [`SubTileGrid`](../src/Core/Maps/SubTileGrid.cs), default 16 per axis), increasing **east** / **north**. [`SubTileTraversal`](../src/Core/Maps/SubTileTraversal.cs) applies the same block / override rules as [`TileTraversal`](../src/Core/Maps/TileTraversal.cs) and uses [`ITerrainEvaluator`](../src/Core/Maps/ITerrainEvaluator.cs) at fractional world coordinates for water vs land. [`GameRoot`](../src/Godot/GameRoot.cs) drives **discrete** **WASD** steps via [`TryStepSubTile`](../src/Core/Maps/WorldState.cs) (with OS key-repeat) and keeps the Shell player node aligned to the resulting sub-cell center; [`SetActorCellFromShell`](../src/Core/Maps/WorldState.cs) remains the sync path after position snaps.

**Screen space**: Camera, projection, sprites/meshes, and Core→screen mapping live in the Shell only (melange view—see linked docs above). Core stays in grid/world space.

### Shell mapping (2D melange prototype)

The Godot Shell ([`GameRoot`](../src/Godot/GameRoot.cs) on `Main/GridMap`) draws the active floor as an **axis-aligned square grid** (orthographic top-down, variant A in [melange-view-pattern.md](melange-view-pattern.md)):

- **East → right:** screen `X` increases with Core `X`. Cell width is a constant pixel size `CellSize`.
- **Board in `GridMap` space:** The `Width × Height` cell rectangle (in pixel units) is **centered on `(0, 0)`** so the camera can show tiles in negative as well as positive screen directions from the view center.
- **North → up:** Core `Y` increases toward **north** on the floor plane; on screen, **north is toward the top of the viewport** (Godot’s `Y` grows downward, so the Shell uses local row `ly = y - MinY` with a vertical flip). With grid pixel origin `(originX, originY)` from [`GetGridOrigin`](../src/Godot/GameRoot.cs), the top-left of global cell `(x, y)` is `(originX + (x - MinX) * CellSize, originY + (Height - 1 - ly) * CellSize)`.

**3D / `Vector3` (prototype pick slice):** For the thin Godot **3D ray pick** (`InteractionRay3D` + orthographic `Camera3D` + invisible `StaticBody3D` floor volume), the Shell maps the logical floor plane to world space as:

- **Core X** → Godot **+X** (same edge as 2D east→right).
- **Core Y** → Godot **+Z** (north along the floor plane; the pick box spans XZ at **world Y = 0**).
- **Core Z** (floor index) is **not** encoded in the pick hit position; the Shell attaches the **active floor** index when building [`GridPickResult`](../src/Core/Interaction/GridPickResult.cs) (same `Z` as the actor / primary slice until multi-floor picking is defined).

Core gameplay may later consume `GridPickResult` from Shell-driven intents; rules stay in Core.

## Active Floor rendering rule

**`ActiveFloor`**: The floor index `Z` used for the primary view—typically the player’s current floor, or an editor/camera override.

**Default slice (v1)**:

- Draw **floor tiles and floor-bound entities** with **`Z == ActiveFloor`** for the main pass.
- Optional later passes (transparent upper floor, pits, effects) are **not** the default contract; document them here if added.

**Ordering within a slice**: At fixed `Z`, sort deterministically for stable overlaps (e.g. **Y** ascending then **X**, or diagonal painter order). Shell must match whatever rule is documented in the Godot layer; Core does not own draw order.

## Map Integrity rule

Every **floor `Z` that appears in map data** (has at least one defined tile or gameplay volume) must have **at least one** designed **vertical connection** to a **different** floor index, such as stairs, ladder, elevator, portal, or an intentional one-way drop **with** a documented return path elsewhere unless the design is explicitly one-way.

- **In/out**: By default the connection graph should allow leaving that floor and **eventually returning** without soft-locking, except where a feature is deliberately one-way.
- **Validation**: Map load or authoring tools in Core (or editor checks) should **reject or flag** maps that violate this rule.

### Vertical links (`VerticalLink`) — single hop, arbitrary floors

Core models each [`VerticalLink`](../src/Core/Maps/VerticalLink.cs) as **one hop**: a directed edge from cell `(FromX, FromY, FromZ)` to `(ToX, ToY, ToZ)`. **`FromZ` and `ToZ` may differ by any amount** (e.g. 1→6 in one jump). **Visitable floor indices do not need to be consecutive** in the map data (e.g. only slices for Z=0 and Z=5 may exist).

- **Two-way (default):** `OneWay == false` means the player may also traverse **from `To` back to `From`** using the **same** link record (reverse of the same endpoint pair). That is the default “same stairway path up and down” for that hop.
- **One-way:** `OneWay == true` means there is **no** reverse traversal on that link; designers should provide another route back if soft-locks must be avoided.

### Multi-hop stair paths (not one `VerticalLink` row today)

If a design needs a **sequence** of landings—e.g. **Stair A** through Z **1 → 4 → 5 → 6**, or **Stair B** through **1 → 2 → 3 → 18**—that is **not** expressed by a single `VerticalLink` unless you only care about a **direct** jump from the bottom to top endpoint. A single link `(x,y,1) → (x,y,6)` does **not** imply the actor “visits” Z=4 and Z=5 as part of the same feature unless you add a separate model (e.g. ordered waypoint list / stair-run id, or a **chain** of `VerticalLink`s at the same shaft). Future work should pick one representation and document it here.

## Maps: Factorio-style convention (living document)

This project treats **Factorio’s map model** as the **design reference** for worlds and saves. Details will evolve with implementation; revise this section when behavior changes.

### What “the Factorio way” means here

- **Global map seed** (plus generation settings you define later) **deterministically** defines what would exist at any chunk coordinate. Same seed + same chunk index + same floor ⇒ same generated content (before player/building changes).
- **Chunks** are the natural unit of generation and storage. Factorio uses **32×32** tiles per surface chunk; SpecialPG uses the same default via [`MapChunkDimensions`](../src/Core/Maps/MapChunkDimensions.cs) (`DefaultWidth` / `DefaultHeight` = 32). Chunk dimensions may stay configurable, but the **Factorio default** is the baseline.
- **Realized territory** is not assumed to be fully precomputed for an infinite plane. Generation is conceptually **on demand** as the simulation or camera needs a chunk (Factorio generates chunks as the map is explored). Storage stays **sparse**: [`FloorSlice`](../src/Core/Maps/FloorSlice.cs) holds tile data per **chunk key** `(Cx, Cy)` only where chunks have content.
- **Chunk lifecycle:** [`FloorSlice`](../src/Core/Maps/FloorSlice.cs) records **modified** chunk keys after gameplay `Set` calls (`ModifiedChunkCount`, `CopyModifiedChunkCoordinates`, `ClearChunkModificationTracking`). **Noise-only** loaded chunks (no tracked `Set`) may be **evicted** with `TryEvictUnmodifiedChunk` to free memory; modified chunks are retained. Initial world build and JSON hydrate use `SuppressChunkModificationTracking` so procedural / load does not mark every chunk dirty ([`ProceduralWorldMapGenerator`](../src/Core/Maps/ProceduralWorldMapGenerator.cs), [`WorldMapJson`](../src/Core/Maps/WorldMapJson.cs)). [`WorldMap.ClearChunkModificationTrackingOnAllFloors`](../src/Core/Maps/WorldMap.cs) resets markers after a successful save.
- **Persistence:** A **new game** stores (or implies) **seed + settings** and persists **what changed** as the player affects the world. **Loading** restores stored state for visited/modified areas and uses the **same generator math** for space that was never materialized or saved in full. Exact save encoding (`WorldMapJson`, binary, per-chunk blobs) is an implementation choice; the **factorio-like intent** is seed + deltas, not one giant static asset for the whole universe.

### SpecialPG mapping (today vs direction)

- **Authoritative runtime data** remains an in-memory [`WorldMap`](../src/Core/Maps/WorldMap.cs) (floors as [`FloorSlice`](../src/Core/Maps/FloorSlice.cs), [`VerticalLink`](../src/Core/Maps/VerticalLink.cs) list). JSON ([`WorldMapJson`](../src/Core/Maps/WorldMapJson.cs)) is one way to **hydrate** that graph for development or hand-authored maps.
- **`WorldMap` bounds:** The type currently uses a **finite** `Width`/`Height` and optional `MinX`/`MinY`. That is enough for a **bounded** prototype (generate every chunk inside the box). **Streaming / growth** (true on-demand chunks beyond a fixed rectangle) means either extending APIs or introducing explicit **world-chunk** streaming separate from fixed map bounds—do that when you add exploration at scale; until then, treat the bounded box as a **development stepping stone**, but keep **generation logic per chunk** `(seed, Z, chunkX, chunkY)` so you do not rewrite core math when streaming arrives.
- **Core procedural bootstrap:** [`ProceduralWorldMapGenerator`](../src/Core/Maps/ProceduralWorldMapGenerator.cs) fills Z=0 and Z=1 from [`MapGenerationParameters`](../src/Core/Maps/MapGenerationParameters.cs) (seed + **land % / water %**) with one deterministic RNG per chunk; water uses [`TerrainTileKinds.Water`](../src/Core/Maps/TerrainTileKinds.cs) + blocked walk. [`ProceduralWorldMapSource`](../src/Core/Maps/ProceduralWorldMapSource.cs) implements [`IWorldMapSource`](../src/Core/Maps/IWorldMapSource.cs) and runs [`MapIntegrity`](../src/Core/Maps/MapIntegrity.cs) before returning.
- **Map workbench (Shell):** ESC pause menu → **Map generator** (preview 128×128, apply full-size world from config dimensions) or **Map editor** (after a workbench-committed proc map only). [`MapWorkbenchPanel`](../src/Godot/MapWorkbenchPanel.cs) + [`MapPreviewRasterizer`](../src/Godot/MapPreviewRasterizer.cs). Session tracks [`SessionMapOrigin`](../src/Godot/SessionMapOrigin.cs) / committed [`MapGenerationParameters`](../src/Core/Maps/MapGenerationParameters.cs) on [`GameRoot`](../src/Godot/GameRoot.cs).
- **Save envelope:** [`MapSaveEnvelope`](../src/Core/Maps/MapSaveEnvelope.cs) + [`MapSaveEnvelopeJson`](../src/Core/Maps/MapSaveEnvelopeJson.cs) bundle generation DTOs, a [`WorldMapJson`](../src/Core/Maps/WorldMapJson.cs) string for tiles/links, and a separate [`EntitiesJson`](../src/Core/Maps/MapSaveEnvelope.cs) string from [`EntityStoreJson`](../src/Core/Maps/EntityStoreJson.cs). Use [`MapSaveEnvelope.FromBoundedWorld`](../src/Core/Maps/MapSaveEnvelope.cs) to pack and [`MapSaveEnvelope.TryCreateWorldState`](../src/Core/Maps/MapSaveEnvelope.cs) to hydrate (Shell still chooses file paths and when to call these).

### Map sources and shell bootstrap

The Shell composes [`IWorldMapSource`](../src/Core/Maps/IWorldMapSource.cs) implementations via [`ChainedWorldMapSource`](../src/Core/Maps/ChainedWorldMapSource.cs) (see [`GameRoot`](../src/Godot/GameRoot.cs)). JSON-from-disk is one source; **procedural** sources should produce the same `WorldMap` contract. Prefer **clear failure** (log + user-visible error, or menu exit) over silently dropping to a placeholder map—players should not land on an unlabeled “emergency” layout. Hand-authored JSON and procedural generation can coexist (e.g. JSON for fixed scenarios, procedural for new-game worlds); configuration chooses precedence.

### Seeds and determinism

Pass a session/world **seed** into procedural builders; keep generation **pure** (inputs → tiles/links) where possible so tests and replays match. Prefer a dedicated RNG derived from the seed (or a small deterministic PRNG) over `Random.Shared` in Core hot paths.

### Integrity vs streaming

[`MapIntegrity`](../src/Core/Maps/MapIntegrity.cs) today assumes a **complete** map graph for validation. **Partial/streamed** generation may temporarily violate rules until a region is **committed**; split **authoring-time** checks from **runtime** checks (validate after a chunk batch, or run a **repair pass** for vertical connectivity). Any floor with defined tiles must eventually satisfy the [Map Integrity rule](#map-integrity-rule); generators should place **stairs/links** in the same pass as tiles when feasible.

### Visibility and memory at scale

The shell renders the full active floor every frame (clipped to the camera cull rect). There is no fog-of-war reveal set, so visibility never accumulates per-session memory; if a future feature reintroduces selective visibility, document storage and eviction here.

### Persistence (summary)

Saves should align with **chunk-oriented** [`FloorSlice`](../src/Core/Maps/FloorSlice.cs) storage: seed, settings, and **per-chunk or aggregated deltas** for territory that matters—not necessarily a monolithic JSON of the entire infinite plane.

## Interaction (summary)

Per project rules: use **3D raycasting** for interaction and hit logic, not grid-snapped collision as the primary model.

**Current slice:** [`InteractionRay3D`](../src/Godot/InteractionRay3D.cs) raycasts against a pick volume aligned to the map, then fills [`GridPickResult`](../src/Core/Interaction/GridPickResult.cs) (`HasCell`, `X`, `Y`, `Z`). The Shell shows the pick in the HUD; Core rules can later branch on this struct for targeting, doors, etc.

**Main scene:** [`Main.tscn`](../src/Godot/Main.tscn) root `Main` (`Node`) has `ShellUi` (HUD + pause menu + debug toggles under `LeftHudColumn`), `GridMap` (2D shell + `GameRoot` + `DebugGridOverlay`), and `Interaction3D` (pick probe). `PickFloor` uses a `BoxShape3D` placeholder until `InteractionRay3D` resizes it at runtime. Update this section when hit filtering, layers, or entity ids are added.
