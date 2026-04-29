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

This avoids intermittent `coreclr` launch-handshake failures (for example `configurationDone: 0x80004005`) and avoids manual process selection on each run.

If auto-attach cannot resolve a PID, use fallback profile **Godot: Attach after run**.

## Optional launch mode (experimental)

The **Godot: Launch game (Experimental)** configuration in [`.vscode/launch.json`](../.vscode/launch.json) runs a **direct executable path** to the installed Godot .NET binary (not the `gdvm` shim under `.gdvm/bin`).

This avoids Windows `coreclr` launch edge cases where shim resolution can fail during debugger initialization.

If your `gdvm` version changes, refresh the configured path to match `gdvm show --csharp` output.

**Run Task → Godot: Run Project** uses the `godot` command on your shell `PATH`.

## GDScript LSP without the Godot editor (headless)

This repo’s [`.vscode/settings.json`](../.vscode/settings.json) enables **`godotTools.lsp.headless`** so **Godot Tools** runs the GDScript language server as a **child process** instead of connecting to the editor on TCP (**6005** / **6008**). You do **not** need the Godot editor open for GDScript completion/diagnostics in Cursor when this works.

**`godotTools.editorPath.godot4`** is set to **`godot`** so it resolves from your **`PATH`** (e.g. **gdvm** or another shim that exposes `godot` on `PATH`). That matches the Godot Tools description: you can use a bare command name when the correct Godot 4 binary is on `PATH`. If headless spawn fails, verify **Cursor** was started in an environment where `godot` is on `PATH` (same as your terminal after `gdvm use`), or change that setting to a full path or `${env:…}`.

Experimental launch mode in [`launch.json`](../.vscode/launch.json) uses an absolute executable path because `coreclr` launch is more reliable this way on Windows than through shell shims.

After changing these settings, use **Developer: Reload Window** once so Godot Tools picks them up.

If headless fails (spawn errors, wrong version), confirm the resolved `godot` is the **.NET** Godot 4 build that matches [`src/Godot/project.godot`](../src/Godot/project.godot) (`config/features` C# / version line).

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

## Optional: open Cursor after ensuring Godot is running (Windows)

[`.scripts/OpenCursorWithGodot.bat`](../.scripts/OpenCursorWithGodot.bat) is a local workaround: if **no** process whose image name contains `Godot_` is running, it starts the **Godot editor** with `--path` to [`src/Godot`](../src/Godot) and `--editor`, waits a couple of seconds, then launches **Cursor** on the repo root.

- Set **`GODOT_PATH`** to a full `.exe` before running the batch if `godot` is not on `PATH` when double-clicking (common with **gdvm** unless your shell profile runs first).
- Edit the **`CURSOR_EXE`** line in the batch if Cursor is not under `%LOCALAPPDATA%\Programs\cursor\`.
- Detection is **coarse** (any Godot counts); see comments in the script.

## Launch vs attach

| | **Launch** (`Godot: Launch game (Experimental)`) | **Attach** (`Godot: Run + Auto Attach (No Picker)` / `Godot: Attach after run` / `.NET Attach (Godot)`) |
|---|--------------------------------------------------------|-------------------------------------|
| Who starts Godot? | **Cursor** starts the configured absolute Godot .NET executable with `--path` to this repo’s Godot project. | **You** start Godot (editor **Play**, or **Godot: Run Project** task). |
| Need editor open first? | **No** — a game window starts directly (main scene). | Auto profile starts one for you; manual attach requires an already-running process. |
| Good for | Quick “run like a player” + breakpoints from the IDE. | Reliable debugging when launch mode is flaky; auto profile is one-key F5. |

Neither mode replaces the other: use **launch** when you want **F5 to autostart** the game; use **attach** when Godot already owns the run.

## Why attach is often recommended for editor work

Many teams spend most of their time in the **Godot editor**: edit scenes, press **Play**, inspect the remote scene tree, stop, repeat. **Attach** from Cursor connects the debugger to **that** process after Play. **Launch** from Cursor starts a **separate** game process without the full editor UI.

Pick the workflow that matches where you are working.

## Hot reload / C# assembly reload

**Attach vs launch does not enable or disable “hot reload.”** It only changes **which process** you debug.

Godot **.NET** can reload or rebuild game assemblies in the editor in ways that are **easy to get wrong** (domain reload, tooling quirks). If something behaves oddly after a C# change, a full **stop Play → rebuild → Play** (or restarting the editor) is still a reliable reset.

The **External Debug Attach** addon under [`src/Godot/addons/external_debug_attach`](../src/Godot/addons/external_debug_attach) keeps its **editor plugin** in **GDScript** partly to reduce C# assembly reload friction in the **plugin** itself. See the addon’s README for that workflow; it complements manual attach from Cursor.

## Related

- [README.md](../README.md) — build, tasks, short pointer here
- [architecture.md](architecture.md) — Core + Shell, coordinates, Active Floor
