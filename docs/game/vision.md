# Game vision (SpecialPG)

**Status:** Draft for review — hybrid Rifts-world direction; mechanics phased in over time.

## Elevator pitch

SpecialPG is a **grid-based** game set on **post-apocalyptic Earth** after the Coming of the Rifts: dimensional tears returned magic, monsters, and lost civilizations. The player operates at **colony, squad, and regional** scale on a **readable map** — not a tabletop battle mat first.

The Shell uses the **melange view** ([visual-direction-guide.md](../visual-direction-guide.md)): orthographic top-down clarity with tall / three-quarter presentation where height matters. The simulation stays on an integer **(X, Y, Z)** grid in Core ([architecture.md](../architecture.md)).

## Hybrid stance (Rifts + original digital systems)

| From Rifts (setting) | In SpecialPG (implementation) |
|----------------------|-------------------------------|
| Tone, factions, regions, technology level | Authored in our data and docs; PDFs as reference |
| O.C.C.s, skills, combat, M.D., P.P.E., etc. | **Deferred** — simplified or original rules when we reach Phase 5+ |
| Bestiary, gear tables, spell lists | **Not ported verbatim** — data-driven equivalents later |

We are **Rifts in the world**, not a 1:1 Palladium rules emulator on day one. When tabletop rules conflict with real-time grid simulation, we **adapt** and document the choice here or in [scope-and-phases.md](scope-and-phases.md).

## What the player does (target experience)

1. **Read the map** — floors, hazards, units, and structures at a glance (colony-management clarity, RimWorld / Factorio family).
2. **Move and interact** on a fair grid — walkability and picks match what they see ([architecture.md](../architecture.md) interaction summary).
3. **Explore a dangerous world** — regions differ by biome and faction presence; threats escalate with distance and story.
4. **Grow capability** — allies, gear, and base development over time (exact loops TBD per phase).

Early builds prove **movement, map, terrain, and persistence**. Narrative and RPG depth follow once the Shell is trustworthy.

## Presentation and engine alignment

- **Map model:** Factorio-style chunks, seed + deltas, sparse `FloorSlice` storage — see architecture § Maps.
- **Entities:** Props, NPCs, machines via `EntityStore` — not every thing is a tile.
- **Vertical play:** `Z` = floor index; stairs and links must satisfy map integrity rules.
- **Interaction:** 3D ray pick → `GridPickResult`; gameplay intents stay in Core as they mature.

## Non-goals (early builds)

- Shipping Palladium PDF content or stat blocks inside the repo.
- 1:1 tabletop combat rounds, initiative, or gridless narrative scenes as the core loop.
- Licensed Rifts art — use placeholders (e.g. Kenney) until art direction is settled.
- Full conversion of every World Book region before one region is fun end-to-end.

## Reference material

- **Phased delivery:** [scope-and-phases.md](scope-and-phases.md)
- **PDF bibliography (external):** [rifts-source-index.md](rifts-source-index.md) — set `RIFTS_SOURCE_ROOT` locally.
- **Engine:** [architecture.md](../architecture.md), [agent-pitfalls.md](../agent-pitfalls.md)

## Changelog

- **2026-05** — Initial hybrid vision draft (setting first, rules phased).
