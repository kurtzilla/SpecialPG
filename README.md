# SpecialPG

Isometric game engine (Core + Shell). Design rules live in [`.cursor/.cursorrules.md`](.cursor/.cursorrules.md); coordinates and floor rules in [`docs/architecture.md`](docs/architecture.md). **Run / debug workflow** (F5, launch vs attach): [`docs/debugging.md`](docs/debugging.md). Optional Windows shortcut: [`.scripts/OpenCursorWithGodot.bat`](.scripts/OpenCursorWithGodot.bat).

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Godot 4.x](https://godotengine.org/download) (.NET / C# build)
- **Godot on `PATH`** (for tasks): the **Godot: Run Project** task runs the `godot` command. Add your install folder to PATH, or on Windows use a `godot.cmd` shim to `Godot_v4.x-stable_mono_win64_console.exe`.
- **No-picker F5 attach**: default debug profile updates its target PID via [`.scripts/ResolveGodotPid.ps1`](.scripts/ResolveGodotPid.ps1), then attaches automatically.
- **Headless GDScript LSP**: [`.vscode/settings.json`](.vscode/settings.json) uses **`godot`** on `PATH` for **`godotTools.editorPath.godot4`** (works with **gdvm**). Cursor must see the same `PATH` as your shell where `godot` works. Details: [`docs/debugging.md`](docs/debugging.md).

## Build (Core + Shell)

```bash
dotnet build SpecialPG.slnx
```

This builds [`src/Core/SpecialPG.Core.csproj`](src/Core/SpecialPG.Core.csproj) and [`src/Godot/SpecialPG.csproj`](src/Godot/SpecialPG.csproj) (Godot references Core).

In VS Code / Cursor, use the default build task: **build solution (Core + Shell)**.

If `dotnet build` fails on the Godot project because `SpecialPG.pdb` is locked, close the debugger or Godot and retry.

## Run the Godot Shell

**Terminal → Run Task… → Godot: Run Project** runs `godot --path` to [`src/Godot`](src/Godot).

## Debug C#

Requires the **C#** (or C# Dev Kit) extension for `coreclr` debugging.

### F5 — no-picker auto attach (recommended)

1. **Run and Debug** → choose **Godot: Run + Auto Attach (No Picker)** (it is the **first** configuration, so **F5** uses it by default).
2. Cursor builds, starts Godot, resolves the game PID, and attaches automatically.
3. C# breakpoints hit without process picker prompts.

Fallback profiles remain available:
- **Godot: Attach after run** (manual process picker)
- **Godot: Launch game (Experimental)** (direct launch mode)

For launch-vs-attach context and hot-reload notes, read **[docs/debugging.md](docs/debugging.md)**.

### External Debug Attach addon

The project enables [`src/Godot/addons/external_debug_attach`](src/Godot/addons/external_debug_attach): editor toolbar **Run + Attach Debug** and autoload `DebugWait` coordinate with a small local service (see addon README).

## Environment files

Copy [`.env/.env.example`](.env/.env.example) to another file under `.env/` for local-only values. Everything in `.env/` is ignored by git except `.env.example`.

## Git remote

Upstream: `https://github.com/kurtzilla/SpecialPG.git`
