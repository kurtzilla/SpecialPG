# SpecialPG architecture

This document is the **source of truth** for coordinate conventions, floor slicing, and map connectivity. If game logic in `src/Core` changes any of these rules, update this file first (see `.cursor/.cursorrules.md`).

## Core + Shell

- **`src/Core`**: Pure C# using the .NET Base Class Library only. No Godot types or assemblies.
- **`src/Godot`**: The **Shell**—the only place Godot APIs and generated game code live. The Shell reads Core data, renders, and forwards input; simulation rules stay in Core.

Performance: prefer **`struct`** types for dense, hot data (tiles, stats) and minimize GC allocations.

## Coordinate system (X, Y, Z)

The world is a **3D integer grid**. All logical positions use the same `(X, Y, Z)` triple in Core and Shell.

| Axis | Meaning |
|------|--------|
| **X** | Horizontal index along one edge of the floor plane (e.g. increasing **east**). |
| **Y** | Horizontal index along the other edge of the floor plane (e.g. increasing **north**). |
| **Z** | **Floor index**—discrete vertical layer. Higher `Z` is “upper” floors unless a specific map overrides elevation flavor. Not fractional height. |

**Tile identity**: A floor cell is addressed by `(X, Y)` **on** floor `Z`. Tile *payload* (e.g. `TileData`) does not repeat `(X, Y, Z)` if the map storage already keys cells by position.

**Screen space**: Isometric projection, camera, and sprites live in the Shell only. Core stays in grid/world space.

## Active Floor rendering rule

**`ActiveFloor`**: The floor index `Z` used for the primary view—typically the player’s current floor, or an editor/camera override.

**Default slice (v1)**:

- Draw **floor tiles and floor-bound entities** with **`Z == ActiveFloor`** for the main pass.
- Optional later passes (transparent upper floor, pits, effects) are **not** the default contract; document them here if added.

**Ordering within a slice**: At fixed `Z`, sort deterministically for stable overlaps (e.g. **Y** ascending then **X**, or diagonal painter order). Shell must match whatever rule is documented in the Godot layer; Core does not own draw order.

## Map Integrity rule

Every **floor `Z` that appears in map data** (has at least one defined tile or gameplay volume) must have **at least one** designed **vertical connection** to another floor (usually `Z ± 1`), such as stairs, ladder, elevator, portal, or an intentional one-way drop **with** a documented return path unless the design is explicitly one-way.

- **In/out**: By default the connection graph should allow leaving that floor and **eventually returning** without soft-locking, except where a feature is deliberately one-way.
- **Validation**: Map load or authoring tools in Core (or editor checks) should **reject or flag** maps that violate this rule.

## Interaction (summary)

Per project rules: use **3D raycasting** for interaction and hit logic, not grid-snapped collision as the primary model. Details belong in Core modules once implemented; update this section when behavior is fixed.
