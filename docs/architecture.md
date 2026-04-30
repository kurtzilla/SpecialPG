# SpecialPG architecture

This document is the **source of truth** for coordinate conventions, floor slicing, and map connectivity. If game logic in `src/Core` changes any of these rules, update this file first (see `.cursor/.cursorrules.md`).

**Rendering direction:** Shell presentation follows the **melange view** (orthographic top-down + tall / three-quarter readability). See [melange-view-pattern.md](melange-view-pattern.md), [visual-direction-guide.md](visual-direction-guide.md), and [view-rendering-discussion.md](view-rendering-discussion.md).

## Core + Shell

- **`src/Core`**: Pure C# using the .NET Base Class Library only. No Godot types or assemblies.
- **`src/Godot`**: The **Shell**—the only place Godot APIs and generated game code live. The Shell reads Core data, renders, and forwards input; simulation rules stay in Core.

**World state:** Actor pose and grid intents (use vertical link, debug floor cycle) live in Core [`WorldState`](../src/Core/Maps/WorldState.cs). The Shell may drive **continuous** world position for the avatar; Core’s `(ActorX, ActorY, ActorZ)` is updated as the **sampled foot cell** via `SetActorCellFromShell` so tiles, links, and integrity stay grid-aligned. Session fog-of-war revealed cells live in [`FogOfWarState`](../src/Core/Maps/FogOfWarState.cs) on the same `WorldState` instance.

**Shell tuning:** [`config.ini`](../src/Godot/config.ini) (loaded by [`ShellAppConfig`](../src/Godot/ShellAppConfig.cs)) supplies values such as `cell_size_px`, default map dimensions (used when the shell builds a map with no JSON), move speed, zoom limits, and fog reveal half-extents (`fog_reveal_half_width_cells` / `fog_reveal_half_height_cells`) without recompiling. Window mode stays in `project.godot`. The checked-in [`sample_twofloor.json`](../src/Godot/maps/sample_twofloor.json) uses its own `width`/`height` from the file (kept smaller than those defaults so the asset stays tractable in git); bump dimensions in `scripts/gen_sample_twofloor.py` when you need a full-size sample on disk.

**Fog reveal + edge tuning (GPU mask mode):** Core reveal writes stay authoritative and cell-based in [`FogOfWarState`](../src/Core/Maps/FogOfWarState.cs). Visual fog is decoupled in the shell: [`FogOverlayRenderer`](../src/Godot/FogOverlayRenderer.cs) keeps a floor-scoped world-space mask texture, stamps reveal circles on actor cell changes, and renders one board-sized quad with [`FogMaskOverlay.gdshader`](../src/Godot/FogMaskOverlay.gdshader). Tune via:
- `fog_gpu_enabled`: default GPU mask path (`true`) vs legacy CPU tile overlay (`false`).
- `fog_mask_pixels_per_cell`: fog mask texel density in world space (higher = smoother edges, higher texture cost).
- `fog_edge_opacity`: fog alpha in shader output (0..1).
- `fog_edge_width_cells`: shader blur/sample radius in tile units (default `1.0`).
- `fog_edge_softness`: shader falloff exponent; higher values darken transition faster.
- `fog_edge_samples`: shader blur taps (quality/perf tradeoff).

Runtime A/B toggle: press **F6** in shell to switch between legacy CPU tile fog and GPU mask overlay while profiling.

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

**Tile identity**: A floor cell is addressed by `(X, Y)` **on** floor `Z`. Tile *payload* (e.g. `TileData`) does not repeat `(X, Y, Z)` if the map storage already keys cells by position.

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

## Procedural maps and map sources

**Authoritative data** for a run is always an in-memory [`WorldMap`](../src/Core/Maps/WorldMap.cs). JSON ([`WorldMapJson`](../src/Core/Maps/WorldMapJson.cs)) is one loader; procedural output should also materialize a `WorldMap` (and optional `WorldState`) the same way.

**Shell bootstrap:** The Godot shell composes [`IWorldMapSource`](../src/Core/Maps/IWorldMapSource.cs) implementations (e.g. JSON file, then built-in fallback) via [`ChainedWorldMapSource`](../src/Core/Maps/ChainedWorldMapSource.cs). Add a new implementation (e.g. `ProceduralWorldMapSource`) and insert it in the chain instead of branching all logic through [`GameRoot`](../src/Godot/GameRoot.cs).

**Seeds and determinism:** Pass a session/world **seed** into procedural builders; keep generation **pure** (inputs → map) where possible so tests can replay. Prefer injectable random (or a fixed `Random` from seed) over ad hoc `Random.Shared` in Core hot paths.

**Bounds:** `WorldMap` today uses a **fixed** `Width`/`Height` and optional `MinX`/`MinY`. Bounded proc patches fit as-is. **Growing** or “infinite” worlds imply either rebuilding a larger map, extending APIs later, or introducing **world-chunk** addressing separate from tile `MinX`/`MinY`—document the chosen policy when you add streaming.

**[`MapIntegrity`](../src/Core/Maps/MapIntegrity.cs) timing:** Full validation assumes a **complete** link graph. Partial/streamed generation may temporarily violate rules; consider **authoring-time** vs **runtime** validation (e.g. validate only **committed** regions, or a final pass after generation).

**Vertical connectivity:** Any floor with defined tiles must still satisfy the integrity rule (vertical exit). Generators should place **stairs/links in the same pass** as tiles, or run a **repair pass** after layout.

**Fog / memory:** [`FogOfWarState`](../src/Core/Maps/FogOfWarState.cs) reveal sets grow with explored area; huge proc worlds may need caps, eviction, or hierarchical fog—document limits when you scale up.

**Persistence:** Saves can continue to use `WorldMapJson` or a future binary/chunked format aligned with [`FloorSlice`](../src/Core/Maps/FloorSlice.cs) chunk storage.

## Interaction (summary)

Per project rules: use **3D raycasting** for interaction and hit logic, not grid-snapped collision as the primary model.

**Current slice:** [`InteractionRay3D`](../src/Godot/InteractionRay3D.cs) raycasts against a pick volume aligned to the map, then fills [`GridPickResult`](../src/Core/Interaction/GridPickResult.cs) (`HasCell`, `X`, `Y`, `Z`). The Shell shows the pick in the HUD; Core rules can later branch on this struct for targeting, doors, etc.

**Main scene:** [`Main.tscn`](../src/Godot/Main.tscn) root `Main` (`Node`) has `ShellUi` (HUD + pause menu + debug toggles under `LeftHudColumn`), `GridMap` (2D shell + `GameRoot` + `FogOverlayRenderer` + `DebugGridOverlay`), and `Interaction3D` (pick probe). `PickFloor` uses a `BoxShape3D` placeholder until `InteractionRay3D` resizes it at runtime. Update this section when hit filtering, layers, or entity ids are added.
