# Shell Feature Revision Log

Revision: 27
TimestampUtc: 2026-05-05T19:45:54.2146820Z

Highlights:
- config.ini shell tuning; Camera2D + discrete WASD sub-tile steps; zoom (wheel, =/-, keypad); upper-right world XY (2 decimals).
- Grid: world origin; darker mid-grey lines; viewport culling; JSON defaults from config when no file.
- Debug placeholders: seeded scatter (blocked tiles, extra stairs, sample path for Paths toggle).
- F5 debug overlay: round toggles (upper-left); walkability / links / ray / paths.
- 3D ray pick → GridPickResult (see architecture Interaction).
- Core WorldState + walkable tiles + vertical link traversal rules.
- HUD: live map stats, source, integrity.
- WorldMap JSON load; MapIntegrity errors reject bad files.
- Cold start at world (0,0); fog-of-war reveal (2× half-extents once); shell default map 2048×1024 cells; wheel zoom via unhandled input.
- Root ShellHudLayer: ESC pause menu (Quit first, Resume); HUD off GridMap CanvasLayer.
- Upper-right world XY uses fixed 2 decimal places (F2), not 2 significant figures.
- Upper-right FPS line above coords; public GameRoot.ShellFps (smoothed).
- Perf: ray-pick HUD only on cell change; physics skips GameRoot.QueueRedraw on move when GPU fog (terrain uses camera transform).
- Top-right stack: perf + FPS + coords + FLR + ZOM.
- HUD readouts throttled (~12Hz) to stay snappy without per-frame text churn.
- Config knobs: render_scale / max_fps / vsync_mode for quick perf profiling.
- Fog: GPU mask + shader overlay path (world-space texture) with CPU legacy toggle on F6.
- Pause menu: Map generator / Map editor (shared land-water UI); GameRoot.ApplyMapFromWorkbench + MapSaveEnvelope types.
- Cold start: procedural map from config.ini startup_seed / startup_land_percent (startup_use_json_sample for JSON-first dev).
- TileTraversal: water surface (elevation below threshold) is never walkable.
- Sub-tile grid (16×16 per cell): Core TryStepSubTile + SubTileTraversal; shell foot collision + actor sync use fractional cell position.
- WASD: discrete one sub-tile step per key event (incl. repeat); continuous pixel glide removed from ShellPlayer.
- Fog reveal: float actor center + land-only sub sampling (Core + GPU mask) so shorelines follow noise, not whole tiles.
- Milestone 7: TerrainVisualColor + subdivided board draw + workbench preview sample continuous noise for coast/hill tint.
- Milestone 8: MapIntegrity.ValidateModification / ValidateVerticalLink for local link walkability without full-map scan.
