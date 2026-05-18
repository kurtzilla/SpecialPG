# Scope and phases

Maps **game goals** to **engine work**. Agents: implement only the phase the task or handoff names. Default active phase: **Phase 0**.

| Flag | Meaning |
|------|---------|
| **Implement** | Agents may write Core/Shell code for this phase when the task asks for it |
| **Design only** | Document and stub types OK; no gameplay logic unless the user explicitly approves |

---

## Phase 0 — Shell (current focus) — **Implement**

**Goal:** Trustworthy grid, movement, terrain, HUD, and map tooling.

| Area | Examples |
|------|----------|
| Grid & floors | `WorldState`, sub-tile WASD, `VerticalLink`, map integrity |
| Map | Procedural bootstrap, JSON sample maps, workbench, `MapSaveEnvelope` types |
| Rendering | Terrain chunk bake, melange Shell, decor/entities on surface layer |
| Debug | F5 attach, coast layers, profiling toggles |

**Code anchors:** `GameRoot`, `TerrainChunkRasterizer`, `ProceduralWorldMapGenerator`, `ShellHudLayer`.

**Not in scope:** Combat stats, spell lists, faction AI campaigns.

---

## Phase 1 — Place & persist — **Implement** (partially started)

**Goal:** Save and load meaningful places; chunk-oriented deltas.

| Area | Examples |
|------|----------|
| Saves | `MapSaveEnvelope`, `WorldMapJson`, `EntityStoreJson` |
| Authoring | Hand-edited maps, workbench commit, session seed tracking |
| Streaming prep | Chunk eviction, modified-chunk tracking (architecture § Maps) |

**Design only until approved:** Full “infinite world” streaming beyond bounded box.

---

## Phase 2 — World identity — **Design only**

**Goal:** Regions feel distinct; faction and biome tags inform generation and UI.

| Area | Examples |
|------|----------|
| Data | Region ids, biome weights, faction territory masks |
| Procedural | Seed + region table → tile/decor biases |
| Reference | World Books in [rifts-source-index.md](rifts-source-index.md) |

**Stub in doc only:** `Region`, `FactionId` Core types — no implementation until user approves Phase 2.

---

## Phase 3 — Entities & factions — **Design only**

**Goal:** NPCs and props with affiliation, not just terrain.

| Area | Examples |
|------|----------|
| Core | Extend `EntityRecord` — faction, role, interactable flag |
| Shell | Sprites/catalog per faction; sort and cull with map |

**Code today:** `EntityStore`, `EntityFloorLayer` — use for placement tests only.

---

## Phase 4 — Interaction & missions — **Design only**

**Goal:** Pick cell → intent → simple objectives and dialogue hooks.

| Area | Examples |
|------|----------|
| Core | Consume `GridPickResult`; mission state machine (minimal) |
| Shell | HUD prompts, target highlighting |

**Code today:** `InteractionRay3D`, `GridPickResult` — wiring only.

---

## Phase 5 — RPG systems — **Design only**

**Goal:** Character capabilities and conflict resolution **inspired by** Rifts, not copied.

| Area | Examples |
|------|----------|
| Core | Attributes, skills, damage resolution, equipment slots |
| Reference | `Rifts - Main`, `Game Master Guide` (see source index) |

**Agent rule:** Do **not** implement stat blocks or combat math unless the task explicitly says **Phase 5**.

---

## Phase 6 — Magic, psionics & tech — **Design only**

**Goal:** Powers and gear as data-driven systems balanced for our loop.

| Area | Examples |
|------|----------|
| Core | Power definitions, cooldowns, M.D. vs S.D.C. style categories (simplified) |
| Reference | Book of Magic, Bionics Sourcebook, Coalition tech books |

Depends on Phase 5 foundations.

---

## Phase summary

| Phase | Name | Agent default |
|-------|------|----------------|
| 0 | Shell | **Implement** |
| 1 | Place & persist | Implement when task says save/map |
| 2 | World identity | Design only |
| 3 | Entities & factions | Design only |
| 4 | Interaction & missions | Design only |
| 5 | RPG systems | Design only |
| 6 | Magic / psionics / tech | Design only |

When in doubt, stay in **Phase 0–1** and ask the user.
