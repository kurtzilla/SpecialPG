# Agent session handoff (template)

Copy the block below into the **first message** of a new agent session (fill in the placeholders). Keeps context out of chat history and in the repo.

---

## Task

(one sentence — what you want done this session)

## Read first

- [docs/agent-pitfalls.md](agent-pitfalls.md) — sections: _(e.g. Chunk seams, Coast visual layers)_
- [docs/architecture.md](architecture.md) — sections: _(e.g. Terrain rendering, Coordinates)_
- [docs/game/scope-and-phases.md](game/scope-and-phases.md) — phase: _(e.g. Phase 0 Shell only)_
- [docs/game/vision.md](game/vision.md) — _(only for gameplay, faction, or narrative work)_

## Done when

- [ ] `dotnet test` on `tests/SpecialPG.Core.Tests` passes
- [ ] Manual: _(e.g. edit chunk-edge cell, pan, no seam; restart after config.ini change)_

## Do not

- _(e.g. re-enable Water↔Ground Side transitions while shoreline contour is on)_
- _(e.g. touch GameRoot camera follow without reading pitfalls § Camera)_

## Last failure (if any)

- _(what the previous agent got wrong and how you noticed)_
