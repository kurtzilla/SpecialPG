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

## Profiling shell terrain redraw (movement jitter)

[`GameRoot`](../src/Godot/GameRoot.cs) repaints visible terrain + grid when the **visible global cell window**, **active floor**, or **zoom** changes; sub-tile camera motion alone does not force a full `_Draw` (see shell changelog REV 37). **REV 44:** the cell window is compared to the **last redraw** using **expansion only** (new min/max must go past the previous min/max). Smooth camera motion can make floored corners **toggle ±1 cell** without the viewport actually needing new columns; strict inequality used to `QueueRedraw` every tick and re-sample continuous elevation (hill/coast tints), which reads as **light/dark flicker**. Reset still happens on floor/map replace via `MarkShellViewDirty`.

**Godot editor:** **Debugger → Monitors** — watch **Process → Frame Time**, **Canvas Item → Canvas Item Drawn in Frame**, and redraw-related counters while holding WASD.

**Rolling average from the shell:** in [`config.ini`](../src/Godot/config.ini) under **`[shell]`**, set **`profile_shell_draw=true`**, or set process environment **`SPECIALPG_PROFILE_SHELL_DRAW=1`** (or `true`) before starting the game. After ~90 `_Draw` calls, the console prints average milliseconds per draw (terrain + grid). Prefer **`profile_shell_draw`** when launching from the editor so you do not depend on inherited env vars.

**HUD perf line:** The upper-right **`… ms/frame (from FPS)`** value is `1000 / ShellFps` (implied frame time from the smoothed FPS counter), **not** the terrain `_Draw` average. When profiling is on and at least one rolling average has been computed, the same label also shows **`Draw … ms avg (terrain+grid)`** from [`GameRoot`](../src/Godot/GameRoot.cs) (same metric as the console log).
**If numbers are high:** the next architectural step is to **split static terrain from per-frame work** (for example rasterize visible terrain to a texture or `SubViewportTexture` when the culled cell range changes, and scroll that texture with the camera).

## Movement tuning (WASD + physics cadence)

- **Discrete speed:** [`wasd_steps_per_second`](../src/Godot/config.ini) in **`[shell]`** — sub-tile steps applied per second while keys are held (see [`GameRoot.TickWasdDiscreteMovement`](../src/Godot/GameRoot.cs)); clamped **1..1024** (see [`ShellAppConfig`](../src/Godot/ShellAppConfig.cs)). The **Movement** panel on the right HUD preset stack can change this live; values are also written to `config.ini`. Approximate pixels per second along an axis: `wasd_steps_per_second × (cell_size_px / 16)` (16 sub-cells per tile).
- **Burst cap:** **`wasd_max_sub_steps_per_physics_frame`** (default **16**, clamped **1..256**) limits how many steps can run in a single physics tick when the frame is long, reducing visible “teleport” pops; raise it when using a high `wasd_steps_per_second` so long frames still drain step debt.
- **Visual smoothing:** [`ShellPlayer`](../src/Godot/ShellPlayer.cs) moves the marker/camera path in `_Process` toward the Core foot target set in `_PhysicsProcess`; grid truth stays in Core via **`AuthoritativeFootWorld`**.
- **Physics vs display:** WASD stepping runs in **`_PhysicsProcess`**. If movement feels uneven at high refresh rates, confirm **Project Settings → Physics → Common → Physics Ticks Per Second** (default **60**) and compare with your monitor refresh; see also [`architecture.md`](architecture.md) shell tuning paragraph.

### WASD blocked near forced-land coasts (patch / bridge edges)

Procedural maps apply [`OriginWalkabilityPatch`](../src/Core/Maps/OriginWalkabilityPatch.cs) and [`LandmassBridgeToLargestComponent`](../src/Core/Maps/LandmassBridgeToLargestComponent.cs) (optional `max_land_bridge_cells` in [`config.ini`](../src/Godot/config.ini) caps the bridge length; **0** = unlimited), then [`ForceLandWalkMargin`](../src/Core/Maps/ForceLandWalkMargin.cs) expands `ForceLand` by **one 4-connected ring** so the next sub-step off a synthetic land tongue is not still classified as noise water. If a step is rejected, [`GameRoot`](../src/Godot/GameRoot.cs) logs a throttled line with [`SubTileTraversal.DiagnoseUnwalkable`](../src/Core/Maps/SubTileTraversal.cs) (for example `sub-tile noise water at world(...)`). Regenerate the map after changing this pipeline.

## Related

- [README.md](../README.md) — build, tasks, short pointer here
- [architecture.md](architecture.md) — Core + Shell, coordinates, Active Floor
