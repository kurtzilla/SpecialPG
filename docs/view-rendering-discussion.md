# View and rendering — design discussion (archived)

This document preserves a **design conversation** (April 2026) about camera style, terminology, tall-building readability, and the chosen **melange view** direction. For the **canonical pattern**, see [melange-view-pattern.md](melange-view-pattern.md). For **goals and success criteria**, see [visual-direction-guide.md](visual-direction-guide.md).

**Related:** [architecture.md](architecture.md) (coordinates, ActiveFloor, interaction summary).

---

## 1. Vocabulary

### True isometric (classic 2D “diamond” grid)

The world’s two horizontal axes are drawn **at an angle** on screen; floor tiles read as **rhombuses**. Height is usually **sprite stacking and draw order**, not a pitched 3D camera. The early Godot prototype used this mapping in `GameRoot` (`GridToScreen` with half-width / half-height diamond layout).

### Orthographic top-down (RimWorld / Factorio family)

The camera looks **mostly along vertical “up”** in the fiction; the ground reads as **square** (or hex) cells from above. Vertical detail comes from **sprites, layers, and UX** (cutaway, transparency), not from rotating the whole map into diamond iso.

### Three-quarter (3/4) art (informal)

In character art, 3/4 means “between profile and front.” In **top-down builders** it usually means: **not** roof-only stamps, but assets that show **roof/deck plus a vertical façade** as if the viewpoint were **slightly pitched**, even when the **camera** remains orthographic from above. It is **façade illusion on a flat grid**, not necessarily diamond-tile isometric **math**.

### 3D orthographic shell

Godot **`Node3D`** world with **`Camera3D`** set to **orthographic** projection: real meshes/quads in space, **no perspective shrink** with distance. Often combined with a **slight pitch** so boxes read as tall. Core can stay an integer grid; the Shell maps `(X, Y, Z_floor)` to `Vector3` (see naming trap below).

**Naming trap (SpecialPG):** In [architecture.md](architecture.md), **`Z` is floor index** in Core—not automatically Godot’s “up” axis. Any 3D shell must document **Core → Godot** mapping in one place.

---

## 2. Real-world references (mental anchors)

These are **visual references**, not strict taxonomies.

| Bucket | Examples | Internal picture |
|--------|----------|------------------|
| Classic 2D diamond iso | *Diablo II*, *Baldur’s Gate*, *Fallout 1/2*, *Age of Empires II* (2D), *SimCity 2000/3000*, *TTD* | Corner of each floor tile dominates; rhombus ground. |
| 2D ortho top-down | *Factorio*, *Prison Architect*, classic *Zelda* top-down, *Crypt of the NecroDancer* | Square walkable cells on the minimap. |
| 2D top-down + tall / 3/4 art | *RimWorld* | Flat floor grid; walls are tall façades on edges. |
| 3D + ortho camera | *Into the Breach*, *Bad North* (readability / silhouette) | Props read as volumes; slight orbit still “boxy.” |
| 3D perspective (contrast) | *XCOM 2*, *Cities: Skylines*, *Dwarf Fortress* (Steam) | Size falloff, miniature-photography feel. |
| Hybrids | *Stardew* (oblique tile walk), *Diablo III/IV* vs *II* | Same word “isometric” in player language, different pipelines. |

---

## 3. Techniques for height without hiding the ground

1. **Cutaway / transparency** — Roofs or upper shells fade on cursor, selection, or roof toggle (aligns with optional ActiveFloor passes in architecture).
2. **Edge-only verticals** — Vertical mass on tile edges so cell centers stay visible.
3. **Floor slices** — ActiveFloor primary draw; ghosts / UI for other `Z` layers.
4. **Silhouette or tint** under occluders — Show that a tile exists beneath art.
5. **Slight camera pitch** — Ortho + pitch (often 3D) for parallax without full diamond iso.
6. **Tall sprites on a square grid** — Logic stays `(X, Y)`; art carries height.
7. **Directional dissolve / stencil** — Rare in early prototypes; hide only the overlap region.

---

## 4. 2D ortho top-down vs 3D ortho shell (summary)

**2D ortho (`Node2D` / TileMap):** Fast iteration, pure 2D art, explicit sort order; tall occlusion and multi-story sorting become **rules-heavy**; 3D raycasting in architecture may duplicate work unless you add invisible 3D picks.

**3D ortho shell:** Natural height, depth buffer, unified screen→ray→collider picking; more pipeline complexity (meshes, materials, axis mapping, instancing for scale).

**Hybrid:** 2D ground + units, 3D ortho for selected buildings—or billboards in 3D.

---

## 5. Melange view (decision)

**Project direction:** adopt the **melange view**—see [melange-view-pattern.md](melange-view-pattern.md).

In short: **Core** stays a **flat `(X, Y)` walk graph per floor** with integer **floor index `Z`**. **Shell** combines **orthographic top-down readability** with **tall / three-quarter presentation** and **Bad North–inspired** clarity (chunky silhouettes, readable materials, optional real 3D height where it pays off), without requiring classic diamond isometric **grid math**.

**Traversability:** Tall art must not imply blocked cells. Use **logical footprint vs visual overhang**, plus **occlusion UX** (fade, outlines, slice toggles) when gameplay targets cells hidden by meshes.

---

## 6. Prototype note

At the time of this archive, the Godot sample scene still drew a **diamond isometric** board for experimentation. The **documented direction** is melange / ortho-aligned Shell work as the game progresses.
