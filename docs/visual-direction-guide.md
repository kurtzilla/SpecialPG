# Visual direction guide

What SpecialPG’s **presentation** is trying to achieve when you ship systems on top of the **Core + Shell** split ([architecture.md](architecture.md)).

**Canonical rendering pattern:** [melange-view-pattern.md](melange-view-pattern.md)  
**Background and terminology:** [view-rendering-discussion.md](view-rendering-discussion.md)

---

## Primary goals

1. **Readable colony / map at a glance**  
   The player can answer: *Where are floors, walls, hazards, and units?* without fighting the camera. The default read is **top-down**: a **map first**, diorama second.

2. **Honest height**  
   Tall structures **read as tall**—through **three-quarter** sprites, stacked floors, and/or **simple 3D** under an orthographic camera—without turning the entire world into classic **diamond isometric** grid math.

3. **Traversable space stays fair**  
   If a cell is walkable or interactable in Core, the Shell must not **visually imply** it is impossible to use. Use **footprint vs overhang** and **occlusion UX** (highlights, cutaway, transparency, slice toggles) so “behind the façade” remains playable and understandable.

4. **Alignment with simulation**  
   One **integer grid** in Core `(X, Y, Z)`; the Shell is a **projection** of that truth, not a competing model of occupancy.

5. **Inspiration, not imitation**  
   Borrow **clarity** from references such as *RimWorld* (tall walls on a flat grid), *Factorio* (square readability), *Into the Breach* / *Bad North* (strong silhouettes, board-game legibility). Do not assume we need any one studio’s exact engine setup.

---

## Non-goals (for this direction)

- **Pure classic isometric** as the flagship look—unless the team explicitly revisits and documents a pivot.
- **Heavy perspective drama** as the default (cinematic perspective may exist later for special modes; it is not the melange default).
- **Art that wins screenshots at the cost of** instant tactical readability during stress.

---

## Success criteria (reviewable)

- A new contributor can read [melange-view-pattern.md](melange-view-pattern.md) and implement a **floor slice + one tall building** without inventing a second coordinate system.
- A playtester can **path and select** units behind partial building occlusion **without** guessing which tile is active.
- Documentation stays consistent: [architecture.md](architecture.md) for **data rules**; melange docs for **how we show them**.

---

## Relationship to interaction

[architecture.md](architecture.md) states a preference for **3D raycasting** for interaction. **Melange** does not forbid 2D picking early in development, but **full 3D ortho Shell** and hybrid approaches should converge on **one coherent pick model** as the game matures.

---

## Changelog

- **2026-04** — Initial guide; melange view adopted as project direction.
