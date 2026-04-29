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

## Related

- [README.md](../README.md) — build, tasks, short pointer here
- [architecture.md](architecture.md) — Core + Shell, coordinates, Active Floor
