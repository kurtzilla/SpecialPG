# SpecialPG

Isometric game engine (Core + Shell). Design rules live in [`.cursor/.cursorrules.md`](.cursor/.cursorrules.md); coordinates and floor rules in [`docs/architecture.md`](docs/architecture.md).

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Godot 4.x](https://godotengine.org/download) (mono / .NET build for C# games)

## Build (Core logic)

```bash
dotnet build SpecialPG.slnx
```

Or open the repo in VS Code / Cursor and use the default **build Core** task.

## Run the Godot Shell

1. Replace the placeholder Godot path in [`.vscode/tasks.json`](.vscode/tasks.json) (**Godot: run project**) with your installed `Godot_*_console.exe` (or non-console build).
2. **Terminal → Run Task… → Godot: run project** once [`src/Godot`](src/Godot) contains a real Godot project (`project.godot`).

## Debug C#

Use **Run and Debug → .NET Core Attach** and pick the running Godot (or `dotnet`) process after starting the game from the editor or the Godot task.

## Environment files

Copy [`.env/.env.example`](.env/.env.example) to another file under `.env/` for local-only values. Everything in `.env/` is ignored by git except `.env.example`.

## Git remote

Upstream: `https://github.com/kurtzilla/SpecialPG.git`
