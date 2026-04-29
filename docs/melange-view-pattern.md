# Melange view pattern

This document is the **canonical description** of SpecialPG’s **melange view**: how we present the world in the Shell while keeping simulation on a **grid in Core**.

**Also read:** [visual-direction-guide.md](visual-direction-guide.md) (goals), [architecture.md](architecture.md) (`(X,Y,Z)`, ActiveFloor), [view-rendering-discussion.md](view-rendering-discussion.md) (fuller background).

---

## Definition

**Melange view** means:

1. **Simulation** — A **discrete 3D integer grid** in Core: `(X, Y, Z)` where **`Z` is floor index** (see [architecture.md](architecture.md)). On each floor, walkability and gameplay are fundamentally **top-down**: neighbors on the **same** `Z` are the primary horizontal graph.

2. **Presentation** — **Orthographic top-down** (or nearly): the player reads the **floor as a flat map** (square or nearly square cells), not classic **diamond isometric** tile math.

3. **Vertical read** — **Tall / three-quarter** art and/or **simple 3D** forms so structures **show height** (roofs plus façades, or meshes under an ortho camera), inspired in part by **Bad North–style** readability: **clear silhouettes**, calm readable materials, toy-diorama clarity—not a mandate to copy any single commercial pipeline.

4. **Non-goals** — We are **not** committing to classic **sprite isometric** as the **defining** screen mapping for the whole game unless we explicitly revisit that choice. Diamond iso remains a **historical prototype** only until replaced.

---

## Layers (who owns what)

| Layer | Responsibility |
|--------|----------------|
| **Core** | Grid positions, floors `Z`, rules, pathfinding graph, what is blocked or interactable. **No** Godot types. |
| **Shell (Godot)** | Camera, projection, meshes/sprites, draw order, Core→screen mapping, input ray, occlusion UX. |

Core never chooses draw order or camera pitch; the Shell never invents a second truth for “which cell is occupied” without Core data.

---

## Allowed Shell variants (same pattern, different cost)

These are all **melange-compatible**; pick or blend based on production needs.

### A. Pure 2D melange

- **Floor:** `TileMapLayer` / `Node2D` grid, **square** cell mapping to pixels.
- **Verticals:** Tall **sprites** (hand-painted, baked from Blender, etc.) with **3/4** read—RimWorld-like placement, Bad North–like graphic discipline if desired.
- **Picking:** Screen → grid or 2D geometry; may later add invisible 3D colliders if architecture’s 3D raycast rule is enforced literally.

### B. Hybrid melange

- **Floor + most entities:** 2D as in A.
- **Hero props:** Selected buildings or cliffs as **`MeshInstance3D`** (or quads) under an **orthographic** `Camera3D`, optionally in a subtree or `SubViewport`, while the rest stays 2D.

### C. Full 3D ortho melange

- **World:** Low-poly **3D** tiles and props; **ortho** camera, often slight pitch.
- **Core:** Unchanged integer grid; Shell **snaps** or derives occupancy from transforms.
- **Picking:** Natural fit for **3D raycasting** as described in [architecture.md](architecture.md).

The pattern **name** applies to A, B, and C; the difference is **implementation depth**, not philosophy.

---

## Hard rules (traversability and art)

1. **Footprint vs overhang** — **Pathfinding and occupancy** use a declared **logical footprint** (e.g. N×M cells). **Meshes and sprites may visually overhang** adjacent cells for silhouette; overhang **does not** block those cells unless Core marks them blocked.

2. **Occlusion UX** — When the player’s **cursor, selection, or move preview** targets a cell **covered** by tall art, the Shell must support **readability**: e.g. fade/dither hull, **cell outline**, **floor ring**, or **ghost** at the logical cell. Exact implementation is Shell detail; the requirement is **no unreadable hidden gameplay**.

3. **ActiveFloor contract** — Default draw remains **`Z == ActiveFloor`** for the main pass unless [architecture.md](architecture.md) is updated with new official passes (transparent upper floor, pits, etc.).

4. **Axis mapping** — Whenever 3D is used, document **`(Core.X, Core.Y, Core.Z_floor) → Godot Vector3`** in the Shell and cross-link from [architecture.md](architecture.md) once stable.

---

## Naming in other docs

- Prefer **melange view** or **orthographic top-down (melange)** in design discussion.
- Avoid calling the shipped camera **“isometric”** unless we explicitly mean **diamond iso**; that word confused **axonometric 2D** with **ortho colony sim** in early conversation.

---

## Changelog

- **2026-04** — Pattern adopted; document added alongside [view-rendering-discussion.md](view-rendering-discussion.md).
