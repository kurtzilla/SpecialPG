# Shell Feature Revision Log

Revision: 35
TimestampUtc: 2026-05-06T06:10:08.1251961Z

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
- Cold start: procedural map from config.ini startup_seed / startup_land_percent (startup_use_json_sample for JSON-first dev).
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
