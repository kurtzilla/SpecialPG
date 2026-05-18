# Game design docs (SpecialPG)

Product direction and phased goals for the game we are building on top of the **Core + Shell** engine.

| Doc | Role |
|-----|------|
| [vision.md](vision.md) | **North star** — setting, hybrid Rifts stance, presentation goals |
| [scope-and-phases.md](scope-and-phases.md) | **Now vs later** — what to implement in each phase |
| [rifts-source-index.md](rifts-source-index.md) | **External bibliography** — Palladium PDF paths (no rule text in repo) |

## How this relates to other docs

| Layer | Doc |
|-------|-----|
| **Engine contracts** | [architecture.md](../architecture.md) — coordinates, maps, saves |
| **Agent regressions** | [agent-pitfalls.md](../agent-pitfalls.md) — how not to break the Shell |
| **Presentation** | [visual-direction-guide.md](../visual-direction-guide.md), [melange-view-pattern.md](../melange-view-pattern.md) |
| **Engine changelog** | [shell-feature-revision-log.md](../shell-feature-revision-log.md) |

**Rule of thumb:** `architecture.md` wins on *how* systems work; `game/vision.md` and `scope-and-phases.md` win on *whether* a feature belongs in the current phase.

## External Rifts library

Palladium **Rifts** rulebooks live outside this repo (default: `D:\source\Rifts`). Set `RIFTS_SOURCE_ROOT` in a local `.env` file if your path differs — see [.env/.env.example](../../.env/.env.example).

Do **not** commit PDFs or paste copyrighted rule text into SpecialPG. Use [rifts-source-index.md](rifts-source-index.md) to find which book to open when designing a system.
