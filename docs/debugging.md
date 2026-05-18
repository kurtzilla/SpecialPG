# Debugging Godot + C# (SpecialPG)

This page describes **how to run and debug** the Shell ([`src/Godot`](../src/Godot)) from Cursor / VS Code. Game architecture and coordinates stay in [`architecture.md`](architecture.md).

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- Godot **4.x .NET** build (not the non-C# template build)
- The **C#** extension (or C# Dev Kit) for `coreclr` debugging in the editor

## F5 workflow (no-picker auto attach)

Default **F5** uses **Godot: Run + Auto Attach (No Picker)**:

1. Build solution
2. Start Godot in a detached task
3. Resolve the correct Godot game PID for this workspace (`--path .../src/Godot`)
4. Attach `coreclr` debugger to that PID automatically

This avoids intermittent `coreclr` attach-handshake failures (for example `configurationDone` errors) and avoids manual process selection on each run.

If auto-attach cannot resolve a PID, use fallback profile **.NET Attach (Godot)**.

**Run Task → SpecialPG: Ensure Godot editor** uses the configured gdvm/godot path to start editor only when needed.

## GDScript LSP with non-headless editor TCP

This workspace keeps **`godotTools.lsp.headless = false`** in [`SpecialPG.code-workspace`](../SpecialPG.code-workspace), so Godot Tools connects to the editor TCP endpoint (**127.0.0.1:6005**).

If Godot editor is not running yet, Cursor can show a startup warning like **\"Couldn't connect to the GDScript language server at 127.0.0.1:6005\"**. This is expected in non-headless mode and clears once editor/LSP is up.

Startup is manual-only to avoid duplicate editor launches; there is no folder-open autorun task in the final workflow.

**`godotTools.editorPath.godot4`** points to the installed Godot .NET executable so editor launch and tooling resolve a stable binary.

After changing these settings, use **Developer: Reload Window** once so Godot Tools picks them up.

If LSP does not connect after editor startup, confirm the selected Godot binary is the **.NET** Godot 4 build that matches [`src/Godot/project.godot`](../src/Godot/project.godot) (`config/features` C# / version line).

## Too many Godot processes

### Read the command line

In the attach picker or Task Manager, check the arguments:

- **`--path … --editor`** means that process is the **Godot editor** for your project. Each time something starts the editor with that project, you can get **another** window/process. That is **not** the same as “three headless LSP copies” in the UI sense—though **Godot Tools (headless)** still **spawns** its own Godot child for the language server; that child may use different flags than a normal editor session.

### How to avoid stacking editors

1. **Close extra editor windows** from the taskbar (or end duplicate `Godot_*_mono_win64.exe` rows in Task Manager) so you keep **one** editor if you still use the editor at all.
2. **Do not repeatedly use “Open workspace with Godot Editor”** (Godot Tools) or equivalent shortcuts unless you mean to open another instance.
3. **Prefer one Cursor window** on this repo; extra windows can each activate Godot Tools and contribute extra work on disk/CPU (and sometimes extra confusion in the process list).
4. After changing **headless** or **`editorPath`**, run **Developer: Reload Window** once. If old helper processes linger, close them in Task Manager, then reload again.
5. If you use **headless LSP** so you “don’t need the editor,” try **not** also leaving **multiple** editors open from other habits (gdvm test, Project Manager, etc.)—pick **either** mostly headless + Cursor **or** one long-lived editor, unless you intentionally want both.

### Normal extra processes during debug

**F5 launch** or **Play** starts a **game** process; an **editor** can still be open. You may legitimately see **more than one** Godot row while debugging—that is not always a bug. Use the **full command line** in the picker to choose the process you mean (game vs editor).

## Attach profiles

| | **No-picker attach** (`Godot: Run + Auto Attach (No Picker)`) | **Manual attach** (`.NET Attach (Godot)`) |
|---|-------------------------------------|-------------------------------------|
| Who starts Godot? | F5 prelaunch tasks build + run detached + resolve PID automatically. | You start Godot/editor, then pick process manually. |
| Picker prompt | No | Yes |
| Good for | Daily one-key debug flow | Troubleshooting or unusual process states |

## Why attach is often recommended for editor work

Many teams spend most of their time in the **Godot editor**: edit scenes, press **Play**, inspect the remote scene tree, stop, repeat. **Attach** from Cursor connects the debugger to **that** process after Play. **Launch** from Cursor starts a **separate** game process without the full editor UI.

Pick the workflow that matches where you are working.

## Hot reload / C# assembly reload

**Attach vs launch does not enable or disable “hot reload.”** It only changes **which process** you debug.

Godot **.NET** can reload or rebuild game assemblies in the editor in ways that are **easy to get wrong** (domain reload, tooling quirks). If something behaves oddly after a C# change, a full **stop Play → rebuild → Play** (or restarting the editor) is still a reliable reset.

The **External Debug Attach** addon under [`src/Godot/addons/external_debug_attach`](../src/Godot/addons/external_debug_attach) keeps its **editor plugin** in **GDScript** partly to reduce C# assembly reload friction in the **plugin** itself. See the addon’s README for that workflow; it complements manual attach from Cursor.

## Godot cache folder (`.godot`)

Godot stores import cache and editor metadata under **`src/Godot/.godot/`** (not the repo root). The folder is **gitignored** and appears after you open [`src/Godot/project.godot`](../src/Godot/project.godot) in the Godot editor at least once. If you do not see it, open that project file from Godot 4.x .NET, then look again under `src/Godot/`.

## “Waiting for debugger…” overlay on Play

The **external_debug_attach** autoload (`DebugWait`) can show a black overlay with a countdown. That is **not** map generation — the game is paused visually until the timer ends or you skip.

- **No debugger attached** (normal Godot Play): the wait is **skipped** automatically on current builds.
- **Debugger attached** (F5 / attach from Cursor): short wait (default **2 s**) so breakpoints in `_Ready` can hit; press **ESC** in the game window to skip immediately.
- To disable entirely: remove or comment out the `DebugWait=…` line under **`[autoload]`** in [`project.godot`](../src/Godot/project.godot), or set `max_wait_seconds` to **0** on the autoload in the Godot editor.

If you previously saw **10.0s**, the autoload export was likely raised in the editor — reset it or rely on ESC.

Set process environment **`SPECIALPG_SKIP_DEBUG_WAIT=1`** (or `true`) before F5 to skip the wait entirely.

## Cold start / F5 load time (15+ seconds)

Cold start cost is **not** WASD — it is procedural map fill plus the first **visible-chunk terrain bake** in [`GameRoot._Draw`](../src/Godot/GameRoot.cs).

| Stage | Typical cause |
|-------|----------------|
| DebugWait | Up to **2 s** when a debugger is attached (ESC to skip; or `SPECIALPG_SKIP_DEBUG_WAIT=1`) |
| Procedural fill | [`ProceduralWorldMapGenerator.BuildBoundedWorld`](../src/Core/Maps/ProceduralWorldMapGenerator.cs) — scales with `default_map_width_cells` × `default_map_height_cells` |
| Bootstrap | [`GameRoot.BootstrapWorldFromMap`](../src/Godot/GameRoot.cs) — placeholders, spawn, integrity |
| First draw | `SyncTerrainChunks` / `SyncSurfaceLayers` for the camera cull window |

**Spawn at global (0,0):** procedural cold start pans noise so the main landmass sits on tile (0,0) (`WorldMap.ProceduralLandmassAligned`). The player should spawn on that cell, not on a fallback search near map center. If you see **`Procedural landmass alignment failed at global (0,0)`**, check `startup_origin_patch_chebyshev_radius` (safety margin only) and console lines for sub-tile viability. Core regression: `ProceduralWorldMapGeneratorTests.Centered_map_origin_is_walkable_on_largest_landmass`. See [agent-pitfalls.md § Global origin](agent-pitfalls.md#global-origin-00-and-spawn).

Enable **`profile_shell_draw=true`** for `terrain_chunk_rebuild` / `surface_sync` / `grid_draw` averages.

**Faster dev defaults** in [`config.ini`](../src/Godot/config.ini):

- `default_map_width_cells=128` and `default_map_height_cells=128`
- `#decor_enabled=false` — skips decor surface sync on boot
- `terrain_use_sprites=false` — color bake (no atlas blit)

## Slow first paint and “can’t move” after Play

Chunk terrain and decor are rebuilt on the **main thread** during [`GameRoot._Draw`](../src/Godot/GameRoot.cs) when the visible cell window or zoom changes. The first frame after load can hitch while all visible chunks bake.

If the map appears but **WASD does nothing**:

1. **Click the Godot game window** (not the VS Code / Cursor IDE). F5 attach only delivers keyboard input to the process that has focus.
2. Close the **pause menu** or **map workbench** — modal HUD disables WASD.
3. Check the console for **`[GameRoot] WASD keys held but no step applied`** or **`WASD blocked`** (throttled).
4. Enable **`profile_shell_draw=true`** in [`config.ini`](../src/Godot/config.ini) and watch the HUD **`Draw … ms (terr …)`** line — if **`terr`** spikes for many seconds, chunks are still baking; movement should still work while baking completes.

## Still slow after the map looks complete?

Use this checklist after a full **stop Play → rebuild → Play** (C# changes require a fresh run).

1. **`profile_shell_draw=true`** — when the camera is **still**, **`grid_draw`** should stay near **0 ms** (grid only redraws when the cull window or zoom changes). If **`grid_draw`** is high every frame, report it — that indicates a redraw loop bug.
2. **`terrain_chunk_rebuild`** should spike only while **panning/zooming** or during the first few seconds of cold start, not forever at idle.
3. **Perf A/B in `config.ini`** (restart after each change):
   - `#decor_enabled=false` — if this fixes slowness, decor bake/sync is the bottleneck.
   - `terrain_use_sprites=false` — color mode uses a 32×32 texture per chunk scaled by the sprite (no 2048×2048 CPU resize).
4. **Chunk sync is viewport-scoped** — only on-screen chunks are kept active; panning no longer syncs the entire historical cull rectangle every tick.
5. **`grid_draw_mode=chunk_only`** — forces chunk borders only when zoomed out (fewer `DrawLine` calls).

## Coast appearance (current)

The shell no longer runs **tile transitions** or **per-pixel shoreline**. In color mode, coast-like shading is only from [`TerrainVisualColor`](../src/Core/Maps/TerrainVisualColor.cs) (elevation / low-land tint). For hard tile edges, use `terrain_use_sprites=false` without expecting grid-aligned blend sprites.

See [agent-pitfalls.md § Coast / shoreline (removed)](agent-pitfalls.md#coast--shoreline-removed).

## Profiling shell terrain redraw (movement jitter)

[`GameRoot`](../src/Godot/GameRoot.cs) repaints visible terrain + grid when the **visible global cell window**, **active floor**, or **zoom** changes; sub-tile camera motion alone does not force a full terrain rebake (see shell changelog REV 37). **REV 44:** the cell window is compared to the **last redraw** using **expansion only** (new min/max must go past the previous min/max). Smooth camera motion can make floored corners **toggle ±1 cell** without the viewport actually needing new columns; strict inequality used to `QueueRedraw` every tick and re-sample continuous elevation (hill/coast tints), which reads as **light/dark flicker**. Reset still happens on floor/map replace via `MarkShellViewDirty`.

**Godot editor:** **Debugger → Monitors** — watch **Process → Frame Time**, **Canvas Item → Canvas Item Drawn in Frame**, and redraw-related counters while holding WASD.

**Rolling average from the shell:** in [`config.ini`](../src/Godot/config.ini) under **`[shell]`**, set **`profile_shell_draw=true`**, or set process environment **`SPECIALPG_PROFILE_SHELL_DRAW=1`** (or `true`) before starting the game. After ~90 samples, the console prints split averages:

| Bucket | What it measures |
|--------|------------------|
| `terrain_chunk_rebuild` | Budgeted dirty `TerrainChunkView` CPU bakes in `_PhysicsProcess` |
| `surface_sync` | Budgeted decor chunk sync (+ entities) in `_PhysicsProcess` |
| `grid_draw` | `DrawGridLines` in `_Draw` |
| (total line) | Grid draw wall time per `_Draw` sample (terrain/surface are separate buckets) |

Pan the camera across a large map and compare which bucket dominates. Prefer **`profile_shell_draw`** when launching from the editor so you do not depend on inherited env vars.

**HUD perf line:** The upper-right **`… ms/frame (from FPS)`** value is `1000 / ShellFps` (implied frame time from the smoothed FPS counter), **not** the terrain bake average. When profiling is on and at least one rolling average has been computed, the same label also shows **`Draw … ms (terr … surf … grid …)`** from [`GameRoot`](../src/Godot/GameRoot.cs) (same split as the console log).

**CPU bake vs editor reimport:** chunk bake reads `terrain_atlas.png` from disk at runtime. Godot **reimport** under `src/Godot/.godot/imported/` is for editor preview; dimensions must match [`TerrainAtlasCatalog`](../src/Godot/Terrain/TerrainAtlasCatalog.cs) (see terrain-art-import doc).

### Godot crashes or hangs immediately on Play (terrain atlas)

If the editor closes or freezes as soon as you press Play, the cause is usually **sprite bake** (`terrain_use_sprites=true`) with an atlas that does not match [`TerrainAtlasCatalog`](../src/Godot/Terrain/TerrainAtlasCatalog.cs) layout.

1. **Bisect:** set **`terrain_use_sprites=false`** in [`config.ini`](../src/Godot/config.ini) and restart — Play should succeed (color bake).
2. **Regenerate** the placeholder atlas from repo root:
   `python scripts/gen_terrain_placeholder_atlas.py`
3. **Reimport** in Godot: select `res://art/terrain/terrain_atlas.png`, or delete `.godot/imported/*terrain_atlas*` and reopen the project.
4. Set **`terrain_use_sprites=true`** again and restart. If the atlas is the wrong size, load fails safely and the shell uses color bake (no native `BlitRect` crash).

### Low FPS after enabling terrain sprites (REV 57)

If the shell drops to a few FPS with **`terrain_use_sprites=true`**:

1. **Restart** after editing [`config.ini`](../src/Godot/config.ini) — default **`terrain_water_animate=false`**.
2. With **`terrain_water_animate=true`**, water frames only rebuild **chunks that contain water** in the current cull window (not every visible chunk). Land-only views should not rebake on the 200 ms water tick.
3. Enable **`profile_shell_draw=true`** and watch the HUD **`Draw … ms (terr …)`** line — if **`terr`** dominates while panning is idle, chunk rebakes are still firing (check water anim or map edits).

**Phase 4b** (corner transitions, `Main8x8`) and **Phase 6.5** (elevation LUT shader) remain deferred until profiling shows they are needed.

## Movement tuning (WASD + physics cadence)

- **Discrete speed:** [`wasd_steps_per_second`](../src/Godot/config.ini) in **`[shell]`** — sub-tile steps applied per second while keys are held (see [`GameRoot.TickWasdDiscreteMovement`](../src/Godot/GameRoot.cs)); clamped **1..1024** (see [`ShellAppConfig`](../src/Godot/ShellAppConfig.cs)). The **Movement** panel on the right HUD preset stack can change this live; values are also written to `config.ini`. Approximate pixels per second along an axis: `wasd_steps_per_second × (cell_size_px / 16)` (16 sub-cells per tile).
- **Burst cap:** **`wasd_max_sub_steps_per_physics_frame`** (default **16**, clamped **1..256**) limits how many steps can run in a single physics tick when the frame is long, reducing visible “teleport” pops; raise it when using a high `wasd_steps_per_second` so long frames still drain step debt. The shell also clamps this to **32** at runtime so a very high config value (e.g. **123**) cannot freeze the main thread for hundreds of sub-steps per tick.
- **Chunk bake budget:** terrain/decor CPU bakes run in **`_PhysicsProcess`** with a per-tick time budget (~4 ms) and a low chunk count cap so Play stays responsive while the map streams in; child chunk textures update without forcing a full grid **`_Draw`** every frame.
- **Visual smoothing:** [`ShellPlayer`](../src/Godot/ShellPlayer.cs) moves the marker/camera path in `_Process` toward the Core foot target set in `_PhysicsProcess`; grid truth stays in Core via **`AuthoritativeFootWorld`**.
- **Physics vs display:** WASD stepping runs in **`_PhysicsProcess`**. If movement feels uneven at high refresh rates, confirm **Project Settings → Physics → Common → Physics Ticks Per Second** (default **60**) and compare with your monitor refresh; see also [`architecture.md`](architecture.md) shell tuning paragraph.

### WASD blocked near forced-land coasts (patch / bridge edges)

Procedural maps apply [`OriginWalkabilityPatch`](../src/Core/Maps/OriginWalkabilityPatch.cs) and [`LandmassBridgeToLargestComponent`](../src/Core/Maps/LandmassBridgeToLargestComponent.cs) (optional `max_land_bridge_cells` in [`config.ini`](../src/Godot/config.ini) caps the bridge length; **0** = unlimited), then [`ForceLandWalkMargin`](../src/Core/Maps/ForceLandWalkMargin.cs) expands `ForceLand` by **one 4-connected ring** so the next sub-step off a synthetic land tongue is not still classified as noise water. If a step is rejected, [`GameRoot`](../src/Godot/GameRoot.cs) logs a throttled line with [`SubTileTraversal.DiagnoseUnwalkable`](../src/Core/Maps/SubTileTraversal.cs) (for example `sub-tile noise water at world(...)`). Regenerate the map after changing this pipeline.

## Related

- [README.md](../README.md) — build, tasks, short pointer here
- [architecture.md](architecture.md) — Core + Shell, coordinates, Active Floor
