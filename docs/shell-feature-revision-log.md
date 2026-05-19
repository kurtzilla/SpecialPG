# Shell Feature Revision Log

Revision: 61
TimestampUtc: 2026-05-19T18:32:02.2875335Z

Highlights:
- config.ini shell tuning; Camera2D + discrete WASD sub-tile steps; zoom (wheel, =/-, keypad); upper-right world XY (2 decimals).
- Grid: world origin; darker mid-grey lines; viewport culling; JSON defaults from config when no file.
- Debug placeholders: seeded scatter (blocked tiles, extra stairs, sample path for Paths toggle).
- F5 debug overlay: round toggles (upper-left); walkability / links / ray / paths.
- 3D ray pick → GridPickResult (see architecture Interaction).
- Core WorldState + walkable tiles + vertical link traversal rules.
- HUD: live map stats, source, integrity.
- WorldMap JSON load; MapIntegrity errors reject bad files.
- Cold start at world (0,0); default map size from config.ini (human-scale defaults); wheel zoom via unhandled input.
- Root ShellHudLayer: ESC pause menu (Quit first, Resume); HUD off GridMap CanvasLayer.
- Upper-right world XY uses fixed 2 decimal places (F2), not 2 significant figures.
- Upper-right FPS line above coords; public GameRoot.ShellFps (smoothed).
- Top-right stack: perf + FPS + coords + FLR + ZOM.
- HUD readouts throttled (~12Hz) to stay snappy without per-frame text churn.
- Config knobs: render_scale / max_fps / vsync_mode for quick perf profiling.
- Pause menu: Map generator / Map editor (shared land-water UI); GameRoot.ApplyMapFromWorkbench + MapSaveEnvelope types.
- Cold start: procedural map from startup_seed / startup_land_percent (optional randomize_startup_seed per session; JSON via startup_use_json_sample).
- TileTraversal: water surface (elevation below threshold) is never walkable.
- Sub-tile grid (16×16 per cell): Core TryStepSubTile + SubTileTraversal; shell foot collision + actor sync use fractional cell position.
- WASD: discrete one sub-tile step per key event (incl. repeat); continuous pixel glide removed from ShellPlayer.
- Milestone 7: TerrainVisualColor + subdivided board draw + workbench preview sample continuous noise for coast/hill tint.
- Milestone 8: MapIntegrity.ValidateModification / ValidateVerticalLink for local link walkability without full-map scan.
- ColdStartView boot log (camera cull vs actor).
- WASD: physics-timed discrete steps (immediate first step on press, steady repeat while held; no OS repeat delay).
- REV 33: All fog-of-war and fog overlay code removed; terrain always renders, no reveal mask, no shader.
- REV 34: Spawn at global (0,0) with procedural land-bridge to LCC; HUD hover tile under stats; grid line canvas transform fix.
- REV 35: Procedural maps centered on tile (0,0); hover uses global canvas transform; grid stroke scales at low zoom; larger shell fonts.
- REV 36: _PhysicsProcess syncs Camera2D after WASD tick; env SPECIALPG_PROFILE_SHELL_DRAW logs avg _Draw ms (terrain+grid).
- REV 37: Terrain QueueRedraw gated on visible cell bounds + floor + zoom; profile_shell_draw + randomize_startup_seed in config.ini; random procedural cold-start session seed when enabled.
- REV 38: WASD no longer QueueRedraw/MarkShellViewDirty each sub-step (cell gate effective); wasd_steps_per_second in config.ini.
- REV 39: ShellPlayer smooths visual foot toward Core target; wasd_max_sub_steps_per_physics_frame; Core sync uses AuthoritativeFootWorld.
- REV 40: Removed move_speed_px_s / MoveSpeedPxS (unused); discrete speed remains wasd_steps_per_second only.
- REV 41: Grid lines use antialiased strokes, no canvas snap (aligns with terrain); softer chunk vs cell stroke ratio; wasd_max_sub_steps default 16 (clamp raised in REV 45).
- REV 42: Thinner tile grid strokes + lower zoom floor so chunk boundaries read clearly vs cell edges.
- REV 43: ForceLandWalkMargin after origin patch + land bridge; HUD ms/frame vs optional Draw avg; softer ForceLand terrain tint.
- REV 44: Terrain QueueRedraw only when cull window expands past last draw (smooth camera ±1 cell chatter).
- REV 45: WASD clamps 1..512 steps/s and 1..128 burst; max_land_bridge_cells + spawn on LCC when origin is a small island.
- REV 46: Runtime WASD tuning (no restart); ceilings 1024/256; PersistWasdMovementSettings to config.ini.
- REV 47: WASD speed sliders on right preset stack (in-game) instead of pause menu.
- REV 48: Visible terrain fill baked to ImageTexture on cull change; _Draw draws texture + grid (fewer CanvasItem ops while panning).
- REV 49: Optional terrain_use_sprites in config.ini — atlas blit bake via TileSpriteResolver (color fallback when atlas missing).
- REV 50: Per-chunk TerrainChunkView terrain (32×32); viewport monolithic bake removed; dirty chunk rebuild on edit/cull.
- REV 51: TileMainPatchPlanner 4×4/2×2/1×1 main patches with global anchors; expanded atlas; PaintOp multi-cell blit.
- REV 53: SurfaceFloorLayer — procedural decor scatter + EntityStore prop sprites above terrain.
- REV 54: Split shell draw profiling; decor sprite pool + optional MultiMesh; animated water; terrain-art-import doc.
- REV 55: Neighbor chunk dirty fan-out; TileDrawOp sort; surface/entity dirty hooks on tile edit and EntityStore.
- REV 56: TileTransitionPlanner Side sprites; two-pass chunk rasterize; transition atlas strips.
- REV 57: Terrain perf — scoped water-chunk dirty; terrain_transitions_enabled; water anim default off; no HUD MarkAllDirty.
- REV 58: Factorio-aligned 64px/tile — cell_size_px, terrain/decor/entity atlases, placeholder regen.
- REV 59: Kenney CC0 2D atlases + kenney-asset-pipeline doc + pack_kenney_2d_atlases.py.
- REV 60: Kenney 3D props — Prop3DLayer, decor_use_3d, import_kenney_3d_props.py.
- REV 61: Stable center-based terrain cull + persist margin; camera follows authoritative foot (fixes turn pop).
