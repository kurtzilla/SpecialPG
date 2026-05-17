# SpecialPG

Grid-based game (Core + Shell) using the **melange view** for Shell rendering—see [`docs/melange-view-pattern.md`](docs/melange-view-pattern.md). Design rules: [`.cursor/.cursorrules.md`](.cursor/.cursorrules.md); coordinates, floors, and **Factorio-style map/chunk conventions**: [`docs/architecture.md`](docs/architecture.md). **Run / debug workflow** (no-picker F5 attach): [`docs/debugging.md`](docs/debugging.md).

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Godot 4.x](https://godotengine.org/download) (.NET / C# build)
- **Godot on `PATH`** (for tasks): detached startup/debug tasks run the `godot` command. Add your install folder to PATH, or on Windows use a `gdvm` shim.
- **No-picker F5 attach**: default debug profile updates its target PID via [`.scripts/ResolveGodotPid.ps1`](.scripts/ResolveGodotPid.ps1), then attaches automatically.
- **Non-headless GDScript LSP**: workspace config keeps `godotTools.lsp.headless = false`, so Godot editor must be running for TCP LSP on `127.0.0.1:6005`.

## Build (Core + Shell)

```bash
dotnet build SpecialPG.slnx
```

This builds [`src/Core/SpecialPG.Core.csproj`](src/Core/SpecialPG.Core.csproj) and [`src/Godot/SpecialPG.csproj`](src/Godot/SpecialPG.csproj) (Godot references Core).

In VS Code / Cursor, use the default build task: **build solution (Core + Shell)**.

If `dotnet build` fails on the Godot project because `SpecialPG.pdb` is locked, close the debugger or Godot and retry.

## Run the Godot Shell

Use **Terminal → Run Task… → SpecialPG: Ensure Godot editor** to ensure the editor is running for [`src/Godot`](src/Godot) when needed. This is manual-only (no folder-open autorun).

## Debug C#

Requires the **C#** (or C# Dev Kit) extension for `coreclr` debugging.

### F5 — no-picker auto attach (recommended)

1. **Run and Debug** → choose **Godot: Run + Auto Attach (No Picker)** (it is the **first** configuration, so **F5** uses it by default).
2. Cursor builds, starts Godot, resolves the game PID, and attaches automatically.
3. C# breakpoints hit without process picker prompts.

Fallback profile remains available:
- **.NET Attach (Godot)** (manual process picker)

For launch-vs-attach context and hot-reload notes, read **[docs/debugging.md](docs/debugging.md)**.

### External Debug Attach addon

The project enables [`src/Godot/addons/external_debug_attach`](src/Godot/addons/external_debug_attach): editor toolbar **Run + Attach Debug** and autoload `DebugWait` coordinate with a small local service (see addon README).

## Art assets (Kenney CC0)

Local Kenney bundles are **not** in git. Rebuild committed atlases and 3D props from `D:\source\KenneyAssets` (or set `KENNEY_ASSETS_ROOT`): see **[docs/kenney-asset-pipeline.md](docs/kenney-asset-pipeline.md)**.

## Environment files

Copy [`.env/.env.example`](.env/.env.example) to another file under `.env/` for local-only values. Everything in `.env/` is ignored by git except `.env.example`.

## Git remote

Upstream: `https://github.com/kurtzilla/SpecialPG.git`
