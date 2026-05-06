#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Godot;
using SpecialPG;
using SpecialPG.Core.Maps;
using CoreTileCell = SpecialPG.Core.Maps.TileCell;

/// <summary>
/// Shell entry: owns the scene tree branch that will drive rendering and input; reads Core types only via normal C# references.
/// Actor pose and move rules live in <see cref="WorldState"/>.
/// </summary>
public partial class GameRoot : Node2D
{
    private const string SampleMapPath = "res://maps/sample_twofloor.json";
    private const string ShellRevisionLogPath = "res://../../docs/shell-feature-revision-log.md";

    /// <summary>Bump when you add user-visible shell behavior; add a line to <see cref="ShellFeatureChangelogLines"/>.</summary>
    private const int ShellFeatureRevision = 35;

    private const int StartupZoomNudgeFloorCellSpan = 512;

    private static readonly string[] ShellFeatureChangelogLines =
    {
        "config.ini shell tuning; Camera2D + discrete WASD sub-tile steps; zoom (wheel, =/-, keypad); upper-right world XY (2 decimals).",
        "Grid: world origin; darker mid-grey lines; viewport culling; JSON defaults from config when no file.",
        "Debug placeholders: seeded scatter (blocked tiles, extra stairs, sample path for Paths toggle).",
        "F5 debug overlay: round toggles (upper-left); walkability / links / ray / paths.",
        "3D ray pick → GridPickResult (see architecture Interaction).",
        "Core WorldState + walkable tiles + vertical link traversal rules.",
        "HUD: live map stats, source, integrity.",
        "WorldMap JSON load; MapIntegrity errors reject bad files.",
        "Cold start at world (0,0); default map size from config.ini (human-scale defaults); wheel zoom via unhandled input.",
        "Root ShellHudLayer: ESC pause menu (Quit first, Resume); HUD off GridMap CanvasLayer.",
        "Upper-right world XY uses fixed 2 decimal places (F2), not 2 significant figures.",
        "Upper-right FPS line above coords; public GameRoot.ShellFps (smoothed).",
        "Top-right stack: perf + FPS + coords + FLR + ZOM.",
        "HUD readouts throttled (~12Hz) to stay snappy without per-frame text churn.",
        "Config knobs: render_scale / max_fps / vsync_mode for quick perf profiling.",
        "Pause menu: Map generator / Map editor (shared land-water UI); GameRoot.ApplyMapFromWorkbench + MapSaveEnvelope types.",
        "Cold start: procedural map from config.ini startup_seed / startup_land_percent (startup_use_json_sample for JSON-first dev).",
        "TileTraversal: water surface (elevation below threshold) is never walkable.",
        "Sub-tile grid (16×16 per cell): Core TryStepSubTile + SubTileTraversal; shell foot collision + actor sync use fractional cell position.",
        "WASD: discrete one sub-tile step per key event (incl. repeat); continuous pixel glide removed from ShellPlayer.",
        "Milestone 7: TerrainVisualColor + subdivided board draw + workbench preview sample continuous noise for coast/hill tint.",
        "Milestone 8: MapIntegrity.ValidateModification / ValidateVerticalLink for local link walkability without full-map scan.",
        "ColdStartView boot log (camera cull vs actor).",
        "WASD: physics-timed discrete steps (immediate first step on press, steady repeat while held; no OS repeat delay).",
        "REV 33: All fog-of-war and fog overlay code removed; terrain always renders, no reveal mask, no shader.",
        "REV 34: Spawn at global (0,0) with procedural land-bridge to LCC; HUD hover tile under stats; grid line canvas transform fix.",
        "REV 35: Procedural maps centered on tile (0,0); hover uses global canvas transform; grid stroke scales at low zoom; larger shell fonts.",
    };

    private static readonly Color GridLineColor = new(0.085f, 0.09f, 0.105f, 0.81f);

    /// <summary>Sub-rectangles per cell for smoother shoreline when &gt; 1; shell uses 1 so each cell is one flat fill (no inner seams).</summary>
    private const int TerrainContourSubdivisions = 1;

    private ShellAppConfig _shell = null!;
    private float _cellSizePx = 32f;
    private float _activeRenderScale = 1.0f;
    private int _activeMaxFps;
    private int _activeVsyncMode = -1;
    private Camera2D? _camera2D;
    private ShellPlayer? _shellPlayer;
    private ShellHudLayer? _shellHud;
    private Vector2I _lastSyncedCell = new(-1, -1);
    private int _lastSyncedActorZ = int.MinValue;
    private Vector2 _zoom = Vector2.One;
    private Vector2 _lastPhysicsRedrawPlayerPos = new(float.NaN, float.NaN);
    private Vector2 _lastPhysicsRedrawZoom = new(float.NaN, float.NaN);
    private double _hudReadoutAccumS = 1.0;
    private ulong _lastBlockedStepLogMs;

    private WorldState _world = null!;
    private int[] _presentZs = Array.Empty<int>();
    private DebugGridOverlay? _debugGridOverlay;
    private DebugChannelPanel? _debugChannelPanel;
    private string _mapSourceSummary = "";
    private int _integrityErrorCount;
    private int _integrityWarningCount;
    private bool _warnedEscMissingHud;

    private SessionMapOrigin _sessionMapOrigin;
    private MapGenerationParameters? _committedGenerationParameters;
    private int? _committedOriginPatchChebyshevRadius;

    /// <summary>Dev-only polyline on Z=0 for the Paths debug channel (see <see cref="ApplyDebugPlaceholders"/>).</summary>
    private readonly List<Vector2I> _debugPlaceholderPath = new();

    private const int DebugPlaceholderRngSeed = unchecked((int)0xC0FFEE);
    private const double HudReadoutIntervalS = 1.0 / 12.0;
    private const ulong BlockedStepLogIntervalMs = 250;

    /// <summary>
    /// WASD steps per second while keys are held (physics-driven; avoids OS key-repeat delay).
    /// </summary>
    private const double WasdStepsPerSecond = 14.0;

    private double _wasdStepDebtAccum;
    private bool _wasdKeysHeldLastPhysics;

    private readonly record struct RuntimeConfigSnapshot(
        float RenderScale,
        int MaxFps,
        int VsyncMode);

    public int ShellMapWidth => _world.Map.Width;

    public int ShellMapHeight => _world.Map.Height;

    public int ShellMapMinX => _world.Map.MinX;

    public int ShellMapMinY => _world.Map.MinY;

    public int ShellDefaultMapWidthCells => _shell.DefaultMapWidthCells;

    public int ShellDefaultMapHeightCells => _shell.DefaultMapHeightCells;

    public int ShellChunkWidthCells => _shell.ChunkWidthCells;

    public int ShellChunkHeightCells => _shell.ChunkHeightCells;

    public SessionMapOrigin ShellSessionMapOrigin => _sessionMapOrigin;

    public MapGenerationParameters? ShellCommittedGenerationParameters => _committedGenerationParameters;

    /// <summary>Land %% target for the active procedural session (committed parameters or config).</summary>
    public int ShellEffectiveLandPercent =>
        _committedGenerationParameters?.LandPercent ?? _shell.StartupLandPercent;

    /// <summary>Noise seed for HUD (committed generation, else terrain config on the active map, else config).</summary>
    public int ShellEffectiveSeed =>
        _committedGenerationParameters?.Seed
        ?? (_world is not null ? _world.Map.TerrainConfig.Seed : _shell.StartupSeed);

    /// <summary>HUD knob for guaranteed flat land radius at global (0,0); committed procedural session else config.</summary>
    public int ShellEffectiveOriginPatchChebyshevRadius =>
        _committedOriginPatchChebyshevRadius ?? _shell.StartupOriginPatchChebyshevRadius;

    /// <summary>Land %% slider + regenerate apply only when not using the JSON sample map.</summary>
    public bool ShellCanApplyLandPercentPreset =>
        !_shell.StartupUseJsonSample &&
        (_sessionMapOrigin is SessionMapOrigin.ProceduralColdStart or SessionMapOrigin.ProceduralWorkbench);

    public WorldMap ShellWorldMap => _world.Map;

    public bool ShellCanOpenMapEditor =>
        (_sessionMapOrigin is SessionMapOrigin.ProceduralWorkbench or SessionMapOrigin.ProceduralColdStart) &&
        _committedGenerationParameters is not null;

    public int ShellActorZ => _world.ActorZ;

    public float ShellCellSizePixels => _cellSizePx;

    public float MoveSpeedPxS => _shell.MoveSpeedPxS;

    /// <summary>Smoothed frames per second from <see cref="Engine.GetFramesPerSecond"/>; updated each <c>_Process</c> for HUD and external readers.</summary>
    public float ShellFps { get; private set; }

    public FloorSlice ShellGetActiveFloorSlice()
    {
        if (_world.Map.TryGetFloor(_world.ActorZ, out var slice) && slice is not null)
        {
            return slice;
        }

        return _world.Map.GetOrCreateFloor(_world.ActorZ);
    }

    public Vector2 ShellGetGridOrigin(int width, int height) => GetGridOrigin(width, height);

    public Rect2 ShellGetCellRect(int x, int y)
    {
        var floor = ShellGetActiveFloorSlice();
        return CellRectGlobal(x, y, floor, GetGridOrigin(floor.Width, floor.Height));
    }

    /// <summary>Visible global cell bounds for the active floor, matching the shell's current camera cull rules.</summary>
    public void ShellGetVisibleCellBounds(out int minGx, out int maxGx, out int minGy, out int maxGy)
    {
        var floor = ShellGetActiveFloorSlice();
        var visible = GetExpandedVisibleCullRect();
        GetVisibleGlobalCellBounds(floor, visible, out minGx, out maxGx, out minGy, out maxGy);
    }

    /// <summary>Shell-only: try to move <see cref="ShellPlayer"/> by <paramref name="delta"/> with axis slide and walkability.</summary>
    public void TryApplyPlayerStep(Vector2 delta)
    {
        if (_shellPlayer is null)
        {
            return;
        }

        var floor = ActiveFloorSlice;
        var next = _shellPlayer.Position + delta;
        if (IsFootWalkableWorld(next, floor))
        {
            _shellPlayer.Position = next;
        }
        else
        {
            const float axisEps = 1e-6f;
            var slid = false;
            if (Mathf.Abs(delta.X) > axisEps)
            {
                var xOnly = new Vector2(next.X, _shellPlayer.Position.Y);
                if (IsFootWalkableWorld(xOnly, floor))
                {
                    _shellPlayer.Position = xOnly;
                    slid = true;
                }
            }

            if (!slid && Mathf.Abs(delta.Y) > axisEps)
            {
                var yOnly = new Vector2(_shellPlayer.Position.X, next.Y);
                if (IsFootWalkableWorld(yOnly, floor))
                {
                    _shellPlayer.Position = yOnly;
                }
            }
        }

        SyncActorFromPlayerFoot();
    }

    public VerticalLinkHint ShellVerticalLinkHint(int x, int y, int z) => VerticalLinkHintAt(x, y, z);

    public IReadOnlyList<Vector2I> ShellDebugPlaceholderPath => _debugPlaceholderPath;

    public override void _Ready()
    {
        _shell = ShellAppConfig.LoadOrDefault();
        _cellSizePx = _shell.CellSizePx;
        EnsureUiCancelBinding();

        var parent = GetParent();
        _shellHud = parent?.GetNodeOrNull<ShellHudLayer>("ShellUi/ShellHudRoot");
        if (_shellHud is null)
        {
            GD.PushError("[GameRoot] Missing ShellUi/ShellHudRoot (ShellHudLayer). HUD and pause menu will not work.");
        }

        _debugGridOverlay = GetNodeOrNull<DebugGridOverlay>("DebugGridOverlay");
        if (_debugGridOverlay is null)
        {
            GD.PushWarning("[GameRoot] Missing DebugGridOverlay node; debug overlay drawing is disabled.");
        }

        _debugChannelPanel = parent?.GetNodeOrNull<DebugChannelPanel>("ShellUi/ShellHudRoot/LeftHudColumn/DebugChannelPanel");
        if (_debugChannelPanel is null)
        {
            GD.PushWarning("[GameRoot] Missing DebugChannelPanel node; F5 debug channel UI is disabled.");
        }
        else
        {
            _debugChannelPanel.Visible = false;
        }

        _camera2D = GetNodeOrNull<Camera2D>("Camera2D");
        if (_camera2D is not null)
        {
            _camera2D.Enabled = true;
            _camera2D.PositionSmoothingEnabled = false;
            _camera2D.Zoom = _zoom;
            _camera2D.MakeCurrent();
        }

        _shellPlayer = GetNodeOrNull<ShellPlayer>("Player");

        _sessionMapOrigin = SessionMapOrigin.Unknown;
        _committedGenerationParameters = null;
        _committedOriginPatchChebyshevRadius = null;

        // Future: when loading a save game, deserialize WorldMap from the save envelope here and skip cold-start selection.
        WorldMap map;
        if (_shell.StartupUseJsonSample)
        {
            var mapChain = new ChainedWorldMapSource(
                new JsonWorldMapSource(SampleMapPath),
                new FallbackSampleWorldMapSource(_shell));
            var chainMap = mapChain.TryBuildWorldMap(out _mapSourceSummary, out var mapBuildError);
            if (chainMap is null)
            {
                GD.PrintErr($"[GameRoot] Map chain failed unexpectedly: {mapBuildError}");
                map = SampleWorldMapBootstrap.CreateFallbackMap(_shell);
                _mapSourceSummary = "Emergency fallback after map source chain failure";
            }
            else
            {
                map = chainMap;
            }

            if (_mapSourceSummary.StartsWith("JSON", StringComparison.Ordinal))
                _sessionMapOrigin = SessionMapOrigin.JsonLoaded;
        }
        else
        {
            var parameters = MapGenerationParameters.Create(_shell.StartupSeed, _shell.StartupLandPercent);
            var originR = Mathf.Clamp(_shell.StartupOriginPatchChebyshevRadius, 0,
                ShellAppConfig.MaxStartupOriginPatchChebyshevRadius);
            _committedOriginPatchChebyshevRadius = originR;
            var (procMinX, procMinY) = ProceduralMapMinCorner(_shell.DefaultMapWidthCells, _shell.DefaultMapHeightCells);
            map = ProceduralWorldMapGenerator.BuildBoundedWorld(
                _shell.DefaultMapWidthCells,
                _shell.DefaultMapHeightCells,
                _shell.ChunkWidthCells,
                _shell.ChunkHeightCells,
                parameters,
                minX: procMinX,
                minY: procMinY,
                originPatchChebyshevRadius: originR);
            _committedGenerationParameters = parameters;
            _sessionMapOrigin = SessionMapOrigin.ProceduralColdStart;
            _mapSourceSummary =
                $"Procedural cold start seed {parameters.Seed} land={parameters.LandPercent}% ({_shell.DefaultMapWidthCells}×{_shell.DefaultMapHeightCells})";
        }

        BootstrapWorldFromMap(map, logBootLine: true);
        // Apply after world exists so any QueueRedraw from perf setup cannot run against a null _world,
        // and the first terrain draw sees the final window/max_fps/vsync state.
        ApplyPerfPreset(_shell.RenderScale, _shell.MaxFps, _shell.VsyncMode, "config", trackUndo: false, persist: false);
    }

    /// <summary>Replace the active session map (e.g. map workbench commit).</summary>
    public void ApplyMapFromWorkbench(WorldMap map, MapGenerationParameters parameters,
        int originPatchChebyshevRadius)
    {
        _committedOriginPatchChebyshevRadius = Mathf.Clamp(originPatchChebyshevRadius, 0,
            ShellAppConfig.MaxStartupOriginPatchChebyshevRadius);
        ApplySessionMapFromGeneration(map, parameters, SessionMapOrigin.ProceduralWorkbench,
            $"Map workbench seed {parameters.Seed} land={parameters.LandPercent}%");
    }

    /// <summary>
    /// Replace the session from a <see cref="MapGenerationParameters"/> snapshot (workbench, save envelope, etc.).
    /// </summary>
    public void ApplySessionMapFromGeneration(WorldMap map, MapGenerationParameters generation,
        SessionMapOrigin origin, string mapSourceSummary)
    {
        ArgumentNullException.ThrowIfNull(map);
        _committedGenerationParameters = generation;
        if (origin is SessionMapOrigin.JsonLoaded or SessionMapOrigin.SaveEnvelopeLoaded)
        {
            _committedOriginPatchChebyshevRadius = null;
        }

        _sessionMapOrigin = origin;
        _mapSourceSummary = mapSourceSummary;
        BootstrapWorldFromMap(map, logBootLine: false);
    }

    /// <summary>Rebuild procedural map from HUD controls; persists startup land %%, seed, and origin-patch radius.</summary>
    public void ApplyProceduralPresetFromHud(int landPercent, int seed, int originPatchChebyshevRadius)
    {
        if (!ShellCanApplyLandPercentPreset)
        {
            GD.Print("[GameRoot] Map preset applies only to procedural cold start / workbench sessions.");
            return;
        }

        landPercent = Mathf.Clamp(landPercent, 0, 100);
        var persistLandErr = ShellAppConfig.PersistStartupLandPercent(landPercent);
        if (persistLandErr != Error.Ok)
        {
            GD.PushWarning($"[GameRoot] Persist startup_land_percent failed: {persistLandErr}");
        }

        var persistSeedErr = ShellAppConfig.PersistStartupSeed(seed);
        if (persistSeedErr != Error.Ok)
        {
            GD.PushWarning($"[GameRoot] Persist startup_seed failed: {persistSeedErr}");
        }

        originPatchChebyshevRadius = Mathf.Clamp(originPatchChebyshevRadius, 0,
            ShellAppConfig.MaxStartupOriginPatchChebyshevRadius);
        var persistOriginErr = ShellAppConfig.PersistStartupOriginPatchChebyshevRadius(originPatchChebyshevRadius);
        if (persistOriginErr != Error.Ok)
        {
            GD.PushWarning($"[GameRoot] Persist startup_origin_patch_chebyshev_radius failed: {persistOriginErr}");
        }

        _shell = ShellAppConfig.LoadOrDefault();

        var parameters = MapGenerationParameters.Create(seed, landPercent);
        _committedOriginPatchChebyshevRadius = originPatchChebyshevRadius;
        var (procMinX, procMinY) = ProceduralMapMinCorner(_shell.DefaultMapWidthCells, _shell.DefaultMapHeightCells);
        var map = ProceduralWorldMapGenerator.BuildBoundedWorld(
            _shell.DefaultMapWidthCells,
            _shell.DefaultMapHeightCells,
            _shell.ChunkWidthCells,
            _shell.ChunkHeightCells,
            parameters,
            minX: procMinX,
            minY: procMinY,
            originPatchChebyshevRadius: originPatchChebyshevRadius);

        _committedGenerationParameters = parameters;
        var preservedOrigin = _sessionMapOrigin;
        _mapSourceSummary =
            $"Procedural seed {parameters.Seed} land={parameters.LandPercent}% ({_shell.DefaultMapWidthCells}×{_shell.DefaultMapHeightCells})";

        BootstrapWorldFromMap(map, logBootLine: false);

        _sessionMapOrigin = preservedOrigin is SessionMapOrigin.ProceduralWorkbench
            ? SessionMapOrigin.ProceduralWorkbench
            : SessionMapOrigin.ProceduralColdStart;

        ApplyPerfPreset(_shell.RenderScale, _shell.MaxFps, _shell.VsyncMode, "after-map-regen", trackUndo: false,
            persist: false);
        RefreshShellHud();
    }

    /// <summary>Rebuild procedural map using committed/config seed and current origin-patch radius (compat convenience).</summary>
    public void ApplyProceduralLandPercentFromHud(int landPercent)
    {
        var seed = _committedGenerationParameters?.Seed ?? _shell.StartupSeed;
        ApplyProceduralPresetFromHud(landPercent, seed, ShellEffectiveOriginPatchChebyshevRadius);
    }

    private void BootstrapWorldFromMap(WorldMap map, bool logBootLine)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (!map.IsBounded)
            throw new NotSupportedException(
                "Unbounded maps are Core-only until streaming shell work lands; use a bounded WorldMap in the Godot shell.");
        var parent = GetParent();

        var floor0 = map.GetOrCreateFloor(0);
        var originGx = Mathf.Clamp(0, floor0.MinX, floor0.MinX + floor0.Width - 1);
        var originGy = Mathf.Clamp(0, floor0.MinY, floor0.MinY + floor0.Height - 1);
        _world = new WorldState(map, originGx, originGy, 0);

        RefreshPresentZs();
        if (_presentZs.Length == 0)
        {
            _world.Map.GetOrCreateFloor(0);
            RefreshPresentZs();
        }

        BuildSampleTilesIfEmpty();
        ApplyDebugPlaceholders();
        _world.ClampAfterShellMapMutation();
        RevalidateIntegrity();

        floor0 = map.GetOrCreateFloor(0);
        originGx = Mathf.Clamp(0, floor0.MinX, floor0.MinX + floor0.Width - 1);
        originGy = Mathf.Clamp(0, floor0.MinY, floor0.MinY + floor0.Height - 1);
        var terrainCfg = _world.Map.TerrainConfig;
        if (TileTraversal.IsWalkable(floor0.Get(originGx, originGy), terrainCfg) &&
            IsSubTileSpawnViable(_world.Map, _world.ActorZ, originGx, originGy, _world.TerrainEvaluator))
        {
            _world.SetActorCellFromShell(originGx, originGy, _world.ActorZ);
        }
        else
        {
            var centerGx = floor0.MinX + floor0.Width / 2;
            var centerGy = floor0.MinY + floor0.Height / 2;
            if (!TryFindWalkableSpawnNearCenter(floor0, centerGx, centerGy, out var walkGx, out var walkGy))
            {
                GD.PrintErr(
                    "[GameRoot] No walkable cell near map center; origin (0,0) not viable; actor left at clamped spawn.");
            }
            else
            {
                GD.Print(
                    $"[GameRoot] Origin ({originGx},{originGy}) not viable; spawned on largest landmass near center at ({walkGx},{walkGy}).");
                _world.SetActorCellFromShell(walkGx, walkGy, _world.ActorZ);
            }
        }

        if (_shellPlayer is not null)
        {
            SnapPlayerToActorCell();
        }

        MaybeNudgeStartupZoomForLargeFloor();
        LogColdStartViewDiagnostics(logBootLine);
        RefreshShellHud();
        if (logBootLine)
        {
            LogShellFeatureRevisionToFile();
        }

        QueueRedraw();
        QueueDebugOverlayRedrawIfVisible();
        // One deferred redraw after the tree is fully settled (fixes occasional first-frame blank view on some GPUs / window modes).
        Callable.From(QueueRedraw).CallDeferred();

        parent?.GetNodeOrNull<InteractionRay3D>("Interaction3D")?.RebuildPickGeometry();

        if (!logBootLine)
        {
            return;
        }

        var floor = ActiveFloorSlice;
        var sample = floor.Get(1, 1);
        var activeCamera = GetViewport().GetCamera2D();
        var cameraName = activeCamera is null ? "(none)" : activeCamera.Name.ToString();
        GD.Print($"[GameRoot] Ready: map {_world.Map.Width}x{_world.Map.Height}, cell={_cellSizePx}px, floors=[{string.Join(",", _presentZs)}], ActorZ={_world.ActorZ}, source=\"{_mapSourceSummary}\", hudBound={_shellHud is not null}, activeCamera2D={cameraName}, sample ElevBucket={sample.ElevationBucket}.");
    }

    public void ApplyPerfPreset(float renderScale, int maxFps, int vsyncMode, string presetName, bool trackUndo = true,
        bool persist = true)
    {
        if (trackUndo)
        {
            PushRuntimeConfigUndoSnapshot();
        }

        _activeRenderScale = Mathf.Clamp(renderScale, 0.5f, 2.0f);
        _activeMaxFps = Mathf.Max(0, maxFps);
        _activeVsyncMode = vsyncMode;

        var window = GetWindow();
        if (window is not null)
        {
            // Keep viewport/UI coordinates stable so HUD and pause menu remain in-viewport.
            // Godot's window content scaling can shift perceived viewport placement on some setups.
            // We keep this pinned and use fps/vsync knobs for live presets.
            window.ContentScaleFactor = 1.0f;
        }

        Engine.MaxFps = _activeMaxFps;

        if (_activeVsyncMode >= 0)
        {
            var maxMode = (int)DisplayServer.VSyncMode.Enabled;
            var mode = Mathf.Clamp(_activeVsyncMode, 0, maxMode);
            DisplayServer.WindowSetVsyncMode((DisplayServer.VSyncMode)mode);
        }

        MarkShellViewDirty();
        RefreshShellHud();
        QueueRedraw();
        QueueDebugOverlayRedrawIfVisible();

        if (persist)
        {
            PersistRuntimeShellSettings();
        }

        GD.Print(
            $"[GameRoot] Perf preset={presetName} render_scale(requested)={_activeRenderScale:F2}, content_scale=1.00, max_fps={_activeMaxFps}, vsync_mode={_activeVsyncMode}.");
    }

    public void OnRayPickUpdated()
    {
        RefreshShellHud();
    }

    public override void _Process(double delta)
    {
        ShellFps = (float)Engine.GetFramesPerSecond();
        if (_shellHud is null || _camera2D is null)
        {
            return;
        }

        _hudReadoutAccumS += delta;
        if (_hudReadoutAccumS < HudReadoutIntervalS)
        {
            return;
        }

        _hudReadoutAccumS = 0.0;
        var fps = Mathf.Max(1f, ShellFps);
        var frameMs = 1000f / fps;
        _shellHud.SetPerfReadout($"{frameMs:F1} ms FTM");
        _shellHud.SetFpsReadout($"{Mathf.RoundToInt(ShellFps)} FPS");

        _shellHud.SetFloorReadout($"{_world.ActorZ} FLR");
        _shellHud.SetZoomReadout($"{FormatWorldCoord(_zoom.X)} ZOM");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_camera2D is null || _shellPlayer is null)
        {
            return;
        }

        _camera2D.Position = _shellPlayer.Position;
        _camera2D.Zoom = _zoom;

        TickWasdDiscreteMovement(delta);

        const float moveEpsSq = 1e-6f;
        var pos = _shellPlayer.Position;
        var moved = float.IsNaN(_lastPhysicsRedrawPlayerPos.X)
                    || (pos - _lastPhysicsRedrawPlayerPos).LengthSquared() > moveEpsSq;
        var zoomed = float.IsNaN(_lastPhysicsRedrawZoom.X) || _lastPhysicsRedrawZoom != _zoom;
        if (!moved && !zoomed)
        {
            QueueDebugOverlayRedrawIfVisible();
            return;
        }

        _lastPhysicsRedrawPlayerPos = pos;
        _lastPhysicsRedrawZoom = _zoom;

        // Camera moved (pan/zoom): repaint the visible terrain.
        QueueRedraw();

        QueueDebugOverlayRedrawIfVisible();
    }

    /// <summary>
    /// While WASD are held, advance discrete sub-steps at <see cref="WasdStepsPerSecond"/>; first step fires on the
    /// frame keys become pressed (no wait for OS key-repeat).
    /// </summary>
    private void TickWasdDiscreteMovement(double delta)
    {
        if (_shellPlayer is null || _world is null)
        {
            _wasdStepDebtAccum = 0;
            _wasdKeysHeldLastPhysics = false;
            return;
        }

        if (_shellHud is not null && _shellHud.IsModalHudOpen)
        {
            _wasdStepDebtAccum = 0;
            _wasdKeysHeldLastPhysics = false;
            return;
        }

        if (!TryComputeWasdSubStepDelta(out _, out _))
        {
            _wasdStepDebtAccum = 0;
            _wasdKeysHeldLastPhysics = false;
            return;
        }

        if (!_wasdKeysHeldLastPhysics)
            _wasdStepDebtAccum += 1.0;

        _wasdKeysHeldLastPhysics = true;

        _wasdStepDebtAccum += delta * WasdStepsPerSecond;
        while (_wasdStepDebtAccum >= 1.0)
        {
            _wasdStepDebtAccum -= 1.0;
            if (!TryApplyWasdSubTileStepOnce())
                break;
        }
    }

    /// <summary>Next <see cref="_PhysicsProcess"/> will schedule a redraw even if player X/Y and zoom are unchanged (e.g. floor index changed).</summary>
    private void MarkShellViewDirty()
    {
        _lastPhysicsRedrawPlayerPos = new Vector2(float.NaN, float.NaN);
        _lastPhysicsRedrawZoom = new Vector2(float.NaN, float.NaN);
        _hudReadoutAccumS = HudReadoutIntervalS;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.Pressed)
        {
            if (mb.ButtonIndex == MouseButton.WheelUp)
            {
                AdjustZoom(1);
                GetViewport().SetInputAsHandled();
                return;
            }

            if (mb.ButtonIndex == MouseButton.WheelDown)
            {
                AdjustZoom(-1);
                GetViewport().SetInputAsHandled();
                return;
            }
        }

        if (@event.IsActionPressed("ui_cancel"))
        {
            if (_shellHud?.TryConsumeEscForMapWorkbench() == true)
            {
                GetViewport().SetInputAsHandled();
                return;
            }

            if (_shellHud is null)
            {
                if (!_warnedEscMissingHud)
                {
                    _warnedEscMissingHud = true;
                    GD.PushWarning("[GameRoot] ui_cancel received but ShellHudLayer is missing; cannot toggle pause menu.");
                }
            }
            else
            {
                GD.Print("[GameRoot] ui_cancel received -> toggling pause menu.");
                _shellHud.TogglePauseMenuFromEsc();
                GetViewport().SetInputAsHandled();
                return;
            }
        }

        if (@event is not InputEventKey key || !key.Pressed || key.Echo)
        {
            return;
        }

        if (key.PhysicalKeycode == Key.F5)
        {
            if (_debugChannelPanel is null || _debugGridOverlay is null)
            {
                GD.PushWarning("[GameRoot] F5 pressed but debug panel/overlay is not available.");
                return;
            }

            var vis = !_debugChannelPanel.Visible;
            _debugChannelPanel.Visible = vis;
            _debugGridOverlay.Visible = vis;
            if (vis)
            {
                _debugChannelPanel.PushAllChannelsFromUi();
            }

            QueueDebugOverlayRedrawIfVisible();

            RefreshShellHud();
            QueueRedraw();
            GetViewport().SetInputAsHandled();
            return;
        }

        if ((key.CtrlPressed || key.MetaPressed) && key.PhysicalKeycode == Key.Z)
        {
            if (TryUndoRuntimeConfigChange())
            {
                GD.Print("[GameRoot] Runtime config undo (Ctrl+Z) applied.");
            }
            else
            {
                GD.Print("[GameRoot] Runtime config undo stack is empty.");
            }

            GetViewport().SetInputAsHandled();
            return;
        }

        if (TryCycleFloor(key))
        {
            GetViewport().SetInputAsHandled();
            return;
        }

        if (TryTraverseVerticalLink(key))
        {
            GetViewport().SetInputAsHandled();
            return;
        }

        if (!key.Echo && (key.PhysicalKeycode == Key.Equal || key.PhysicalKeycode == Key.KpAdd))
        {
            AdjustZoom(1);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (!key.Echo && (key.PhysicalKeycode == Key.Minus || key.PhysicalKeycode == Key.KpSubtract))
        {
            AdjustZoom(-1);
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Draw()
    {
        if (_camera2D is not null && _shellPlayer is not null)
        {
            _camera2D.Position = _shellPlayer.Position;
            _camera2D.Zoom = _zoom;
        }

        if (_world is null)
        {
            return;
        }

        var floor = ActiveFloorSlice;
        var origin = GetGridOrigin(floor.Width, floor.Height);
        var visible = GetExpandedVisibleCullRect();
        GetVisibleGlobalCellBounds(floor, visible, out var minGx, out var maxGx, out var minGy, out var maxGy);

        var sub = Mathf.Clamp(TerrainContourSubdivisions, 1, 8);
        var eval = _world.TerrainEvaluator;
        var terrainCfg = _world.Map.TerrainConfig;
        for (var gy = minGy; gy <= maxGy; gy++)
        {
            for (var gx = minGx; gx <= maxGx; gx++)
            {
                var cell = CellRectGlobal(gx, gy, floor, origin);
                if (!cell.Intersects(visible))
                {
                    continue;
                }

                var tile = floor.Get(gx, gy);
                var sw = cell.Size.X / sub;
                var sh = cell.Size.Y / sub;
                for (var iy = 0; iy < sub; iy++)
                {
                    for (var ix = 0; ix < sub; ix++)
                    {
                        var r = new Rect2(cell.Position.X + ix * sw, cell.Position.Y + iy * sh, sw, sh);
                        if (!r.Intersects(visible))
                        {
                            continue;
                        }

                        var worldX = gx + (ix + 0.5f) / sub;
                        var worldY = gy + (sub - 1 - iy + 0.5f) / sub;
                        var rgb = TerrainVisualColor.AtWorld(worldX, worldY, tile, eval, terrainCfg);
                        DrawRect(r, new Color(rgb.R, rgb.G, rgb.B), true);
                    }
                }
            }
        }

        DrawGridLines(floor, origin, visible);
    }

    private FloorSlice ActiveFloorSlice
    {
        get
        {
            if (_world.Map.TryGetFloor(_world.ActorZ, out var slice) && slice is not null)
            {
                return slice;
            }

            return _world.Map.GetOrCreateFloor(_world.ActorZ);
        }
    }

    public enum VerticalLinkHint
    {
        None,
        Outgoing,
        Reverse,
        Both,
    }

    private VerticalLinkHint VerticalLinkHintAt(int x, int y, int z)
    {
        var from = _world.Map.TryGetVerticalLinkFrom(x, y, z, out _);
        var rev = _world.Map.TryGetVerticalLinkReverse(x, y, z, out _);
        if (from && rev)
        {
            return VerticalLinkHint.Both;
        }

        if (from)
        {
            return VerticalLinkHint.Outgoing;
        }

        return rev ? VerticalLinkHint.Reverse : VerticalLinkHint.None;
    }

    private bool TryTraverseVerticalLink(InputEventKey key)
    {
        var useKey = key.PhysicalKeycode == Key.E || key.IsActionPressed("ui_accept");
        if (!useKey)
        {
            return false;
        }

        if (!_world.TryUseVerticalLink())
        {
            return false;
        }

        RefreshPresentZs();
        SnapPlayerToActorCell();
        RefreshShellHud();
        MarkShellViewDirty();
        QueueRedraw();
        QueueDebugOverlayRedrawIfVisible();

        return true;
    }

    private bool TryApplyWasdSubTileStepOnce()
    {
        if (_shellPlayer is null)
        {
            return false;
        }

        if (_shellHud is not null && _shellHud.IsModalHudOpen)
        {
            return false;
        }

        if (!TryComputeWasdSubStepDelta(out var dSubX, out var dSubY))
        {
            return false;
        }

        if (!_world.TryStepSubTile(dSubX, dSubY))
        {
            LogStepBlockedDiagnostic(dSubX, dSubY);
            return false;
        }

        var floor = ActiveFloorSlice;
        _shellPlayer.Position =
            CellSubCenterWorld(_world.ActorX, _world.ActorY, _world.ActorSubX, _world.ActorSubY, floor);

        _lastSyncedCell = new Vector2I(_world.ActorX, _world.ActorY);
        _lastSyncedActorZ = _world.ActorZ;
        RefreshShellHud();
        MarkShellViewDirty();
        QueueRedraw();
        QueueDebugOverlayRedrawIfVisible();
        return true;
    }

    /// <summary>
    /// Throttled <see cref="GD.Print"/> for blocked WASD steps. Reports the rejected sub-cell + reason from
    /// <see cref="SubTileTraversal.DiagnoseUnwalkable"/> so movement bugs (mismatched spawn vs sub-noise water,
    /// blocked tiles, bounds) are visible in the Godot console.
    /// </summary>
    private void LogStepBlockedDiagnostic(int dSubX, int dSubY)
    {
        var now = Time.GetTicksMsec();
        if (now - _lastBlockedStepLogMs < BlockedStepLogIntervalMs)
        {
            return;
        }

        _lastBlockedStepLogMs = now;

        SubTileGrid.AddSubDelta(_world.ActorX, _world.ActorSubX, dSubX, out var nx, out var nsx);
        SubTileGrid.AddSubDelta(_world.ActorY, _world.ActorSubY, dSubY, out var ny, out var nsy);
        var reason = SubTileTraversal.DiagnoseUnwalkable(_world.Map, _world.ActorZ, nx, ny, nsx, nsy,
            _world.TerrainEvaluator) ?? "unknown";
        GD.Print(
            $"[GameRoot] WASD blocked at actor=({_world.ActorX},{_world.ActorY}).sub({_world.ActorSubX},{_world.ActorSubY})" +
            $" → ({nx},{ny}).sub({nsx},{nsy}) z={_world.ActorZ}: {reason}");
    }

    /// <summary>Core axes: +Y north. Opposing keys cancel; each axis clamped to -1..1 for <see cref="WorldState.TryStepSubTile"/>.</summary>
    private static bool TryComputeWasdSubStepDelta(out int dSubX, out int dSubY)
    {
        var lx = 0;
        if (Input.IsPhysicalKeyPressed(Key.A))
        {
            lx--;
        }

        if (Input.IsPhysicalKeyPressed(Key.D))
        {
            lx++;
        }

        var ly = 0;
        if (Input.IsPhysicalKeyPressed(Key.W))
        {
            ly++;
        }

        if (Input.IsPhysicalKeyPressed(Key.S))
        {
            ly--;
        }

        dSubX = Math.Clamp(lx, -1, 1);
        dSubY = Math.Clamp(ly, -1, 1);
        return dSubX != 0 || dSubY != 0;
    }

    private bool TryCycleFloor(InputEventKey key)
    {
        var delta = 0;
        if (key.PhysicalKeycode is Key.Bracketleft or Key.Pageup)
        {
            delta = -1;
        }
        else if (key.PhysicalKeycode is Key.Bracketright or Key.Pagedown)
        {
            delta = 1;
        }

        if (delta == 0)
        {
            return false;
        }

        RefreshPresentZs();
        if (_presentZs.Length == 0)
        {
            return false;
        }

        if (!_world.TryCyclePresentFloor(delta))
        {
            return false;
        }

        SnapPlayerToActorCell();
        RefreshShellHud();
        MarkShellViewDirty();
        QueueRedraw();
        QueueDebugOverlayRedrawIfVisible();

        return true;
    }

    private void RefreshPresentZs()
    {
        var list = _world.Map.PresentFloorIndices();
        _presentZs = new int[list.Count];
        for (var i = 0; i < list.Count; i++)
        {
            _presentZs[i] = list[i];
        }
    }

    private void BuildSampleTilesIfEmpty()
    {
        foreach (var z in _world.Map.PresentFloorIndices())
        {
            if (!_world.Map.TryGetFloor(z, out var slice) || slice is null || slice.HasAnyDefinedTile())
            {
                continue;
            }

            SampleWorldMapBootstrap.FillCheckerboard(slice, z);
        }
    }

    /// <summary>
    /// Seeds random blocked tiles, extra vertical links, and a sample path so F5 debug channels show spread-out data.
    /// RNG uses <see cref="DebugPlaceholderRngSeed"/> until map/run seeds exist.
    /// </summary>
    private void ApplyDebugPlaceholders()
    {
        var map = _world.Map;
        var rng = new Random(DebugPlaceholderRngSeed);
        var w = map.Width;
        var h = map.Height;

        var usedFrom = new HashSet<(int X, int Y, int Z)>();
        foreach (var l in map.VerticalLinks)
        {
            usedFrom.Add((l.FromX, l.FromY, l.FromZ));
        }

        bool TryPickFromZ0(out int x, out int y)
        {
            for (var t = 0; t < 12_000; t++)
            {
                x = map.MinX + rng.Next(w);
                y = map.MinY + rng.Next(h);
                if (x == map.MinX && y == map.MinY)
                {
                    continue;
                }

                if (usedFrom.Contains((x, y, 0)))
                {
                    continue;
                }

                usedFrom.Add((x, y, 0));
                return true;
            }

            x = y = -1;
            return false;
        }

        bool TryPickBothPair(out int x, out int y)
        {
            for (var t = 0; t < 12_000; t++)
            {
                x = map.MinX + rng.Next(w);
                y = map.MinY + rng.Next(h);
                if (x == map.MinX && y == map.MinY)
                {
                    continue;
                }

                if (usedFrom.Contains((x, y, 0)) || usedFrom.Contains((x, y, 1)))
                {
                    continue;
                }

                usedFrom.Add((x, y, 0));
                usedFrom.Add((x, y, 1));
                return true;
            }

            x = y = -1;
            return false;
        }

        for (var i = 0; i < 3; i++)
        {
            if (!TryPickBothPair(out var bx, out var by))
            {
                break;
            }

            map.AddVerticalLink(new VerticalLink
            {
                FromX = bx,
                FromY = by,
                FromZ = 0,
                ToX = bx,
                ToY = by,
                ToZ = 1,
                Kind = VerticalLinkKind.Stairs,
                OneWay = false,
            });
            map.AddVerticalLink(new VerticalLink
            {
                FromX = bx,
                FromY = by,
                FromZ = 1,
                ToX = bx,
                ToY = by,
                ToZ = 0,
                Kind = VerticalLinkKind.Stairs,
                OneWay = false,
            });
        }

        for (var i = 0; i < 6; i++)
        {
            if (!TryPickFromZ0(out var ox, out var oy))
            {
                break;
            }

            map.AddVerticalLink(new VerticalLink
            {
                FromX = ox,
                FromY = oy,
                FromZ = 0,
                ToX = ox,
                ToY = oy,
                ToZ = 1,
                Kind = VerticalLinkKind.Stairs,
                OneWay = true,
            });
        }

        for (var i = 0; i < 18; i++)
        {
            if (!TryPickFromZ0(out var sx, out var sy))
            {
                break;
            }

            map.AddVerticalLink(new VerticalLink
            {
                FromX = sx,
                FromY = sy,
                FromZ = 0,
                ToX = sx,
                ToY = sy,
                ToZ = 1,
                Kind = VerticalLinkKind.Stairs,
                OneWay = false,
            });
        }

        var reserved = new HashSet<(int X, int Y, int Z)>();
        foreach (var l in map.VerticalLinks)
        {
            reserved.Add((l.FromX, l.FromY, l.FromZ));
            reserved.Add((l.ToX, l.ToY, l.ToZ));
        }

        foreach (var z in map.PresentFloorIndices())
        {
            if (!map.TryGetFloor(z, out var slice) || slice is null)
            {
                continue;
            }

            var placed = 0;
            var targetBlocked = Mathf.Max(12, w * h / 6000);
            for (var attempts = 0; attempts < 25_000 && placed < targetBlocked; attempts++)
            {
                var x = map.MinX + rng.Next(w);
                var y = map.MinY + rng.Next(h);
                if (z == 0 && x == map.MinX && y == map.MinY)
                {
                    continue;
                }

                if (reserved.Contains((x, y, z)))
                {
                    continue;
                }

                var t = slice.Get(x, y);
                if ((t.Flags & TileFlags.Blocked) != 0)
                {
                    continue;
                }

                slice.Set(x, y, t with { Flags = (byte)(t.Flags | TileFlags.Blocked) });
                placed++;
            }
        }

        _debugPlaceholderPath.Clear();
        if (!map.TryGetFloor(0, out var floor0) || floor0 is null)
        {
            return;
        }

        var pathScratch = new List<Vector2I>();
        for (var attempts = 0; attempts < 30_000 && pathScratch.Count < 8; attempts++)
        {
            var px = map.MinX + rng.Next(w);
            var py = map.MinY + rng.Next(h);
            var t = floor0.Get(px, py);
            if (!TileTraversal.IsWalkable(t, _world.Map.TerrainConfig))
            {
                continue;
            }

            var candidate = new Vector2I(px, py);
            if (pathScratch.Contains(candidate))
            {
                continue;
            }

            pathScratch.Add(candidate);
        }

        pathScratch.Sort(static (a, b) => (a.X + a.Y).CompareTo(b.X + b.Y));
        _debugPlaceholderPath.AddRange(pathScratch);
    }

    private void RevalidateIntegrity()
    {
        var result = MapIntegrity.Validate(_world.Map);
        _integrityErrorCount = 0;
        _integrityWarningCount = 0;
        foreach (var issue in result.Issues)
        {
            if (issue.Severity == MapIntegritySeverity.Error)
            {
                _integrityErrorCount++;
            }
            else
            {
                _integrityWarningCount++;
            }
        }

        if (result.IsValid)
        {
            return;
        }

        foreach (var issue in result.Issues)
        {
            var prefix = issue.Severity == MapIntegritySeverity.Error ? "[integrity]" : "[integrity warn]";
            GD.PrintErr($"{prefix} {issue.Message}");
        }
    }

    private void RefreshShellHud()
    {
        if (_shellHud is null)
        {
            return;
        }

        var mapLine =
            $"World map {_world.Map.Width}×{_world.Map.Height} cells — WASD to move";
        _shellHud.SetBootText(
            $"{mapLine}\nWASD — hold to move (smooth repeat; release to stop)   |   Wheel / = - / keypad +/- — zoom   |   E / Enter — link   |   [ ] / PgUp/PgDn — floor   |   F5 — debug   |   Ctrl+Z — undo config   |   ESC — pause / Quit");
        _shellHud.SetRevisionReadout($"REV {ShellFeatureRevision}");
        _shellHud.SyncMapPresetUi(ShellEffectiveLandPercent, ShellEffectiveSeed,
            ShellEffectiveOriginPatchChebyshevRadius, ShellCanApplyLandPercentPreset);
    }

    /// <summary>One-shot diagnostics after cold bootstrap to explain blank vs cull vs terrain tint (see cold-start plan).</summary>
    private void LogColdStartViewDiagnostics(bool coldBoot)
    {
        if (!coldBoot)
        {
            return;
        }

        if (_camera2D is not null && _shellPlayer is not null)
        {
            _camera2D.Position = _shellPlayer.Position;
            _camera2D.Zoom = _zoom;
        }

        var floor = ActiveFloorSlice;
        var origin = GetGridOrigin(floor.Width, floor.Height);
        var visible = GetExpandedVisibleCullRect();
        GetVisibleGlobalCellBounds(floor, visible, out var minGx, out var maxGx, out var minGy, out var maxGy);

        var wx = SubTileTraversal.SubCellWorldX(_world.ActorX, _world.ActorSubX);
        var wy = SubTileTraversal.SubCellWorldY(_world.ActorY, _world.ActorSubY);
        var tile = floor.Get(_world.ActorX, _world.ActorY);
        var rgb = TerrainVisualColor.AtWorld(wx, wy, tile, _world.TerrainEvaluator, _world.Map.TerrainConfig);
        var cam = _camera2D is null
            ? "cam=(none)"
            : $"cam=({FormatWorldCoord(_camera2D.Position.X)},{FormatWorldCoord(_camera2D.Position.Y)})";

        GD.Print(
            $"[GameRoot] ColdStartView: visible=({FormatWorldCoord(visible.Position.X)},{FormatWorldCoord(visible.Position.Y)}) size=({FormatWorldCoord(visible.Size.X)}×{FormatWorldCoord(visible.Size.Y)}) " +
            $"cells=({minGx},{minGy})..({maxGx},{maxGy}) actorCell=({_world.ActorX},{_world.ActorY}) sub=({_world.ActorSubX},{_world.ActorSubY}) " +
            $"gridOrigin=({FormatWorldCoord(origin.X)},{FormatWorldCoord(origin.Y)}) {cam} zoom={FormatWorldCoord(_zoom.X)} " +
            $"terrainRgb=({FormatWorldCoord(rgb.R)},{FormatWorldCoord(rgb.G)},{FormatWorldCoord(rgb.B)})");
    }

    private void LogShellFeatureRevisionToFile()
    {
        try
        {
            var path = ProjectSettings.GlobalizePath(ShellRevisionLogPath);
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            using var sw = new StreamWriter(path, false, Encoding.UTF8);
            sw.WriteLine("# Shell Feature Revision Log");
            sw.WriteLine();
            sw.WriteLine($"Revision: {ShellFeatureRevision}");
            sw.WriteLine($"TimestampUtc: {DateTime.UtcNow:O}");
            sw.WriteLine();
            sw.WriteLine("Highlights:");
            foreach (var line in ShellFeatureChangelogLines)
            {
                sw.Write("- ");
                sw.WriteLine(line);
            }
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[GameRoot] Failed to write revision log file: {ex.Message}");
        }
    }

    private RuntimeConfigSnapshot CaptureRuntimeConfigSnapshot() => new(
        _activeRenderScale,
        _activeMaxFps,
        _activeVsyncMode);

    private void PushRuntimeConfigUndoSnapshot()
    {
        var s = CaptureRuntimeConfigSnapshot();
        ShellAppConfig.PushUndoSnapshot(new ShellAppConfig.RuntimeShellSettings(
            s.RenderScale,
            s.MaxFps,
            s.VsyncMode));
    }

    private bool TryUndoRuntimeConfigChange()
    {
        if (!ShellAppConfig.TryPopUndoSnapshot(out var s))
        {
            return false;
        }

        ApplyPerfPreset(s.RenderScale, s.MaxFps, s.VsyncMode, "undo", trackUndo: false, persist: false);
        PersistRuntimeShellSettings();
        return true;
    }

    private void PersistRuntimeShellSettings()
    {
        var err = ShellAppConfig.SaveRuntimeShellSettings(
            _activeRenderScale,
            _activeMaxFps,
            _activeVsyncMode);
        if (err != Error.Ok)
        {
            GD.PushWarning($"[GameRoot] Failed to persist runtime shell settings: {err}");
        }
    }

    private string IntegritySummaryBbcode()
    {
        if (_integrityErrorCount == 0 && _integrityWarningCount == 0)
        {
            return "[color=#6dcea0]OK[/color]";
        }

        if (_integrityErrorCount > 0)
        {
            return $"[color=#ff8b8b]{_integrityErrorCount} error(s), {_integrityWarningCount} warning(s)[/color]";
        }

        return $"[color=#e8c06a]{_integrityWarningCount} warning(s) only[/color]";
    }

    private string ActorLinkRoleSummary(int floorZ)
    {
        var hint = VerticalLinkHintAt(_world.ActorX, _world.ActorY, floorZ);
        return hint switch
        {
            VerticalLinkHint.Outgoing => "outgoing link",
            VerticalLinkHint.Reverse => "return (To of two-way)",
            VerticalLinkHint.Both => "outgoing + return",
            _ => "—",
        };
    }

    /// <summary>
    /// Pixel origin (top-left of the north-west tile rect) chosen so the whole <c>width × height</c> board
    /// is centered on <c>(0, 0)</c> in <c>GridMap</c> space; local row <c>ly = y - MinY</c> for north-up flip (see architecture.md).
    /// </summary>
    private Vector2 GetGridOrigin(int width, int height)
    {
        var cs = _cellSizePx;
        return new Vector2(-width * cs * 0.5f, -height * cs * 0.5f);
    }

    /// <summary>Negative corner so global tile (0,0) lies near the board center (even dimensions).</summary>
    private static (int MinX, int MinY) ProceduralMapMinCorner(int widthCells, int heightCells) =>
        (-(widthCells / 2), -(heightCells / 2));

    private float GridLineStrokeLocal(float baseLocalWidth)
    {
        var invZ = 1f / Mathf.Max(1e-4f, _zoom.X);
        invZ = Mathf.Clamp(invZ, 0.45f, 10f);
        return Mathf.Max(1f, baseLocalWidth * invZ);
    }

    private void DrawGridLines(FloorSlice floor, Vector2 gridOriginTopLeft, Rect2 visible)
    {
        var cs = _cellSizePx;
        var width = floor.Width;
        var height = floor.Height;
        var boardW = width * cs;
        var boardH = height * cs;
        var x0 = gridOriginTopLeft.X;
        var y0 = gridOriginTopLeft.Y;
        var x1 = x0 + boardW;
        var y1 = y0 + boardH;
        const float chunkBorderWidth = 1.55f;
        const float regularLineWidth = chunkBorderWidth * 0.5f;
        var chunkW = Math.Max(1, _shell.ChunkWidthCells);
        var chunkH = Math.Max(1, _shell.ChunkHeightCells);

        var minIx = Mathf.Clamp(Mathf.FloorToInt((visible.Position.X - x0) / cs), 0, width);
        var maxIx = Mathf.Clamp(Mathf.CeilToInt((visible.Position.X + visible.Size.X - x0) / cs), 0, width);
        for (var i = minIx; i <= maxIx; i++)
        {
            var x = CellRectGlobal(floor.MinX + i, floor.MinY, floor, gridOriginTopLeft).Position.X;
            var widthPx = (i % chunkW == 0) ? chunkBorderWidth : regularLineWidth;
            var from = new Vector2(x, y0);
            var to = new Vector2(x, y1);
            SnapVerticalLineToCanvasPixels(ref from, ref to);
            DrawLine(from, to, GridLineColor, GridLineStrokeLocal(widthPx), false);
        }

        var minJy = Mathf.Clamp(Mathf.FloorToInt((visible.Position.Y - y0) / cs), 0, height);
        var maxJy = Mathf.Clamp(Mathf.CeilToInt((visible.Position.Y + visible.Size.Y - y0) / cs), 0, height);
        for (var j = minJy; j <= maxJy; j++)
        {
            float y;
            if (j < height)
            {
                var gy = floor.MinY + height - 1 - j;
                y = CellRectGlobal(floor.MinX, gy, floor, gridOriginTopLeft).Position.Y;
            }
            else
            {
                var south = CellRectGlobal(floor.MinX, floor.MinY, floor, gridOriginTopLeft);
                y = south.Position.Y + south.Size.Y;
            }

            var widthPx = (j % chunkH == 0) ? chunkBorderWidth : regularLineWidth;
            var from = new Vector2(x0, y);
            var to = new Vector2(x1, y);
            SnapHorizontalLineToCanvasPixels(ref from, ref to);
            DrawLine(from, to, GridLineColor, GridLineStrokeLocal(widthPx), false);
        }
    }

    /// <summary>
    /// <see cref="CanvasItem.GetCanvasTransform"/> maps this node's local coordinates → canvas (viewport) pixels;
    /// round in canvas space so grid lines land on integer screen pixels at fractional zoom.
    /// </summary>
    private Vector2 LocalPointToCanvas(Vector2 localPt) =>
        GetCanvasTransform() * localPt;

    private Vector2 CanvasPointToLocal(Vector2 canvasPt) =>
        GetCanvasTransform().AffineInverse() * canvasPt;

    /// <summary>One-line hover readout: <c>[terrain] x, y</c> in grid world space (F2); false when off the board.</summary>
    public bool TryGetHoverTileReadout(out string line)
    {
        line = "—";
        if (_world is null)
        {
            return false;
        }

        var viewportMouse = GetViewport().GetMousePosition();
        var localPt = GetGlobalTransformWithCanvas().AffineInverse() * viewportMouse;
        var floor = ShellGetActiveFloorSlice();
        var origin = GetGridOrigin(floor.Width, floor.Height);
        var boardW = floor.Width * _cellSizePx;
        var boardH = floor.Height * _cellSizePx;
        if (localPt.X < origin.X || localPt.Y < origin.Y || localPt.X >= origin.X + boardW ||
            localPt.Y >= origin.Y + boardH)
        {
            return false;
        }

        if (!TryWorldToSubCell(localPt, floor, out var gx, out var gy, out var sx, out var sy))
        {
            return false;
        }

        var tile = floor.Get(gx, gy);
        var wxf = gx + (sx + 0.5f) / SubTileGrid.Resolution;
        var wyf = gy + (sy + 0.5f) / SubTileGrid.Resolution;
        var terrain = TerrainVisualColor.DescribeCategoryAtTileCenter(
            wxf,
            wyf,
            tile,
            _world.TerrainEvaluator,
            _world.Map.TerrainConfig);
        line = $"[{terrain}] {FormatWorldCoord(localPt.X)}, {FormatWorldCoord(localPt.Y)}";
        return true;
    }

    /// <summary>One-line player foot readout: <c>[terrain] x, y</c> in grid world space (F2).</summary>
    public bool TryGetPlayerFootReadout(out string line)
    {
        line = "—";
        if (_world is null || _shellPlayer is null)
        {
            return false;
        }

        var floor = ShellGetActiveFloorSlice();
        var pos = _shellPlayer.Position;
        if (!TryWorldToSubCell(pos, floor, out var gx, out var gy, out var sx, out var sy))
        {
            line = "[—] —";
            return false;
        }

        var tile = floor.Get(gx, gy);
        var wxf = gx + (sx + 0.5f) / SubTileGrid.Resolution;
        var wyf = gy + (sy + 0.5f) / SubTileGrid.Resolution;
        var terrain = TerrainVisualColor.DescribeCategoryAtTileCenter(
            wxf,
            wyf,
            tile,
            _world.TerrainEvaluator,
            _world.Map.TerrainConfig);
        line = $"[{terrain}] {FormatWorldCoord(pos.X)}, {FormatWorldCoord(pos.Y)}";
        return true;
    }

    private void SnapVerticalLineToCanvasPixels(ref Vector2 from, ref Vector2 to)
    {
        var c0 = LocalPointToCanvas(from);
        var c1 = LocalPointToCanvas(to);
        var rx = Mathf.Round((c0.X + c1.X) * 0.5f);
        c0.X = rx;
        c1.X = rx;
        from = CanvasPointToLocal(c0);
        to = CanvasPointToLocal(c1);
    }

    private void SnapHorizontalLineToCanvasPixels(ref Vector2 from, ref Vector2 to)
    {
        var c0 = LocalPointToCanvas(from);
        var c1 = LocalPointToCanvas(to);
        var ry = Mathf.Round((c0.Y + c1.Y) * 0.5f);
        c0.Y = ry;
        c1.Y = ry;
        from = CanvasPointToLocal(c0);
        to = CanvasPointToLocal(c1);
    }

    private Rect2 GetVisibleWorldRect()
    {
        if (_camera2D is null)
        {
            return GetViewportRect();
        }

        var half = GetViewport().GetVisibleRect().Size / _camera2D.Zoom / 2f;
        return new Rect2(_camera2D.Position - half, half * 2f);
    }

    /// <summary>Viewport rect grown slightly so tile <see cref="Rect2.Intersects"/> is stable at sub-pixel edges.</summary>
    private Rect2 GetExpandedVisibleCullRect()
    {
        var r = GetVisibleWorldRect();
        var pad = Mathf.Max(2f, _cellSizePx);
        return r.Grow(pad);
    }

    /// <summary>Bounding global cell indices for <paramref name="visible"/> using the same north-up mapping as <see cref="WorldToCellFloor"/>.</summary>
    private void GetVisibleGlobalCellBounds(FloorSlice floor, Rect2 visible, out int minGx, out int maxGx, out int minGy,
        out int maxGy)
    {
        var corners = new[]
        {
            visible.Position,
            new Vector2(visible.Position.X + visible.Size.X, visible.Position.Y),
            new Vector2(visible.Position.X, visible.Position.Y + visible.Size.Y),
            visible.Position + visible.Size
        };
        var c0 = WorldToCellFloor(corners[0], floor);
        minGx = maxGx = c0.X;
        minGy = maxGy = c0.Y;
        for (var i = 1; i < corners.Length; i++)
        {
            var c = WorldToCellFloor(corners[i], floor);
            if (c.X < minGx)
            {
                minGx = c.X;
            }

            if (c.X > maxGx)
            {
                maxGx = c.X;
            }

            if (c.Y < minGy)
            {
                minGy = c.Y;
            }

            if (c.Y > maxGy)
            {
                maxGy = c.Y;
            }
        }
    }

    private Vector2 CellCenterWorld(int cellX, int cellY, FloorSlice floor)
    {
        var origin = GetGridOrigin(floor.Width, floor.Height);
        var r = CellRectGlobal(cellX, cellY, floor, origin);
        return r.GetCenter();
    }

    /// <summary>Pixel position for sub-cell center; <paramref name="subY"/> increases toward Core +Y (north, top of cell).</summary>
    private Vector2 CellSubCenterWorld(int cellX, int cellY, int subX, int subY, FloorSlice floor)
    {
        var origin = GetGridOrigin(floor.Width, floor.Height);
        var r = CellRectGlobal(cellX, cellY, floor, origin);
        var fx = (subX + 0.5f) / SubTileGrid.Resolution;
        var northT = (subY + 0.5f) / SubTileGrid.Resolution;
        var px = r.Position.X + fx * r.Size.X;
        var py = r.Position.Y + (1f - northT) * r.Size.Y;
        return new Vector2(px, py);
    }

    private Vector2I WorldToCellFloor(Vector2 world, FloorSlice floor)
    {
        var origin = GetGridOrigin(floor.Width, floor.Height);
        var lx = Mathf.Clamp((int)Math.Floor((world.X - origin.X) / _cellSizePx), 0, floor.Width - 1);
        var row = (int)Math.Floor((world.Y - origin.Y) / _cellSizePx);
        var ly = floor.Height - 1 - Mathf.Clamp(row, 0, floor.Height - 1);
        return new Vector2I(floor.MinX + lx, floor.MinY + ly);
    }

    private bool IsFootWalkableWorld(Vector2 world, FloorSlice floor)
    {
        if (!TryWorldToSubCell(world, floor, out var gx, out var gy, out var sx, out var sy))
        {
            return false;
        }

        return SubTileTraversal.IsWalkable(_world.Map, floor.Z, gx, gy, sx, sy, _world.TerrainEvaluator);
    }

    private bool TryWorldToSubCell(Vector2 world, FloorSlice floor, out int gx, out int gy, out int subX,
        out int subY)
    {
        gx = gy = subX = subY = 0;
        var origin = GetGridOrigin(floor.Width, floor.Height);
        var boardW = floor.Width * _cellSizePx;
        var boardH = floor.Height * _cellSizePx;
        if (world.X < origin.X || world.Y < origin.Y || world.X >= origin.X + boardW ||
            world.Y >= origin.Y + boardH)
        {
            return false;
        }

        var c = WorldToCellFloor(world, floor);
        gx = c.X;
        gy = c.Y;
        var r = CellRectGlobal(gx, gy, floor, origin);
        var u = (world.X - r.Position.X) / Math.Max(1e-6f, r.Size.X);
        var fracSouth = (world.Y - r.Position.Y) / Math.Max(1e-6f, r.Size.Y);
        u = Mathf.Clamp(u, 0f, 0.999999f);
        fracSouth = Mathf.Clamp(fracSouth, 0f, 0.999999f);
        subX = Mathf.Clamp((int)Math.Floor(u * SubTileGrid.Resolution), 0, SubTileGrid.Resolution - 1);
        var northFrac = 1f - fracSouth;
        subY = Mathf.Clamp((int)Math.Floor((northFrac * SubTileGrid.Resolution) - 1e-5f), 0,
            SubTileGrid.Resolution - 1);
        return true;
    }

    private void MaybeNudgeStartupZoomForLargeFloor()
    {
        if (_camera2D is null)
        {
            return;
        }

        var floor = ActiveFloorSlice;
        if (Mathf.Max(floor.Width, floor.Height) <= StartupZoomNudgeFloorCellSpan)
        {
            return;
        }

        var z = Mathf.Clamp(_zoom.X - _shell.ZoomStep, _shell.ZoomMin, _shell.ZoomMax);
        if (z >= _zoom.X - 1e-5f)
        {
            return;
        }

        _zoom = new Vector2(z, z);
        _camera2D.Zoom = _zoom;
    }

    /// <summary>
    /// Prefer map center, but shells may place blocked tiles there (fallback map, debug RNG). Validates against
    /// <see cref="SubTileTraversal"/> so the spawn sub-cell + its 4 cardinal sub-neighbors are walkable and the
    /// surrounding 5x5 sub-tile window is mostly land; otherwise WASD would reject every step on a knife-edge
    /// land cell whose noise dips below the water threshold for nearby sub-cells.
    /// </summary>
    private bool TryFindWalkableSpawnNearCenter(FloorSlice floor, int centerGx, int centerGy, out int sx,
        out int sy)
    {
        var z = _world.ActorZ;
        var eval = _world.TerrainEvaluator;
        var terrain = _world.Map.TerrainConfig;

        // Prefer the largest tile-level landmass (4-connected), then Chebyshev rings from map center — matches
        // OriginWalkabilityPatch/stairs at center instead of stranding on a small island at geometric middle.
        if (LandmassSpawnSupport.TryFindSpawnChebyshevFromCenterOnLargestLandmass(
                floor, terrain, centerGx, centerGy,
                (gx, gy) => IsSubTileSpawnViable(_world.Map, z, gx, gy, eval),
                out sx, out sy))
        {
            return true;
        }

        if (LandmassSpawnSupport.TryFindSpawnChebyshevFromCenterOnLargestLandmass(
                floor, terrain, centerGx, centerGy,
                acceptSpawnCell: null,
                out sx, out sy))
        {
            GD.Print(
                $"[GameRoot] No sub-tile-viable spawn on largest landmass near ({centerGx},{centerGy}); " +
                $"using tile-walkable ({sx},{sy}) on that landmass. First WASD steps may be blocked by sub-tile water.");
            return true;
        }

        var maxR = Math.Min(512, Math.Max(floor.Width, floor.Height));
        var fallbackGx = int.MinValue;
        var fallbackGy = int.MinValue;

        for (var r = 0; r < maxR; r++)
        {
            for (var dy = -r; dy <= r; dy++)
            {
                for (var dx = -r; dx <= r; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r)
                    {
                        continue;
                    }

                    var gx = centerGx + dx;
                    var gy = centerGy + dy;
                    if (!floor.Contains(gx, gy))
                    {
                        continue;
                    }

                    if (!TileTraversal.IsWalkable(floor.Get(gx, gy), terrain))
                    {
                        continue;
                    }

                    if (fallbackGx == int.MinValue)
                    {
                        fallbackGx = gx;
                        fallbackGy = gy;
                    }

                    if (IsSubTileSpawnViable(_world.Map, z, gx, gy, eval))
                    {
                        sx = gx;
                        sy = gy;
                        return true;
                    }
                }
            }
        }

        if (fallbackGx != int.MinValue)
        {
            GD.Print(
                $"[GameRoot] No sub-tile-viable spawn near ({centerGx},{centerGy}); falling back to cell-walkable ({fallbackGx},{fallbackGy}). " +
                "First WASD steps may be blocked by sub-tile water around the spawn cell.");
            sx = fallbackGx;
            sy = fallbackGy;
            return true;
        }

        sx = sy = 0;
        return false;
    }

    /// <summary>
    /// Spawn cell qualifies when the spawn sub-cell, all 4 cardinal sub-neighbors, and at least 16 of 25 sub-cells
    /// in a 5x5 window centered on the tile center pass <see cref="SubTileTraversal.IsWalkable"/>.
    /// </summary>
    private static bool IsSubTileSpawnViable(WorldMap map, int z, int gx, int gy, ITerrainEvaluator eval)
    {
        var c = SubTileGrid.CenterSub;
        if (!SubTileTraversal.IsWalkable(map, z, gx, gy, c, c, eval))
        {
            return false;
        }

        if (!IsSubCellWalkableAcrossTiles(map, z, gx, gy, c + 1, c, eval) ||
            !IsSubCellWalkableAcrossTiles(map, z, gx, gy, c - 1, c, eval) ||
            !IsSubCellWalkableAcrossTiles(map, z, gx, gy, c, c + 1, eval) ||
            !IsSubCellWalkableAcrossTiles(map, z, gx, gy, c, c - 1, eval))
        {
            return false;
        }

        var walkableCount = 0;
        for (var dsy = -2; dsy <= 2; dsy++)
        {
            for (var dsx = -2; dsx <= 2; dsx++)
            {
                if (IsSubCellWalkableAcrossTiles(map, z, gx, gy, c + dsx, c + dsy, eval))
                {
                    walkableCount++;
                }
            }
        }

        return walkableCount >= 16;
    }

    /// <summary>
    /// Sub-cell walkability that handles the rare case where <c>(subDx, subDy)</c> lands outside <c>0..Resolution-1</c>
    /// (e.g. when probing the 5x5 window beyond the local tile); resolves to the neighboring tile via
    /// <see cref="SubTileGrid.AddSubDelta"/>.
    /// </summary>
    private static bool IsSubCellWalkableAcrossTiles(WorldMap map, int z, int gx, int gy, int subX, int subY,
        ITerrainEvaluator eval)
    {
        SubTileGrid.AddSubDelta(gx, 0, subX, out var nx, out var nsx);
        SubTileGrid.AddSubDelta(gy, 0, subY, out var ny, out var nsy);
        return SubTileTraversal.IsWalkable(map, z, nx, ny, nsx, nsy, eval);
    }

    private void SnapPlayerToActorCell()
    {
        if (_shellPlayer is null)
        {
            return;
        }

        var f = ActiveFloorSlice;
        _shellPlayer.Position =
            CellSubCenterWorld(_world.ActorX, _world.ActorY, _world.ActorSubX, _world.ActorSubY, f);
        _lastSyncedCell = new Vector2I(-1, -1);
        _lastSyncedActorZ = int.MinValue;
        SyncActorFromPlayerFoot(forceHud: true);
    }

    private void SyncActorFromPlayerFoot(bool forceHud = false)
    {
        if (_shellPlayer is null)
        {
            return;
        }

        var floor = ActiveFloorSlice;
        if (!TryWorldToSubCell(_shellPlayer.Position, floor, out var gx, out var gy, out var sx, out var sy))
        {
            return;
        }

        _world.SetActorCellFromShell(gx, gy, _world.ActorZ, sx, sy);
        var c = new Vector2I(gx, gy);
        var cellChanged = c != _lastSyncedCell || _world.ActorZ != _lastSyncedActorZ;

        if (forceHud || cellChanged)
        {
            _lastSyncedCell = c;
            _lastSyncedActorZ = _world.ActorZ;
            RefreshShellHud();
            QueueRedraw();
            QueueDebugOverlayRedrawIfVisible();
        }
    }

    private void QueueDebugOverlayRedrawIfVisible()
    {
        if (_debugGridOverlay?.Visible == true)
        {
            _debugGridOverlay.QueueRedraw();
        }
    }

    private void AdjustZoom(int direction)
    {
        if (_camera2D is null)
        {
            return;
        }

        var z = Mathf.Clamp(_zoom.X + direction * _shell.ZoomStep, _shell.ZoomMin, _shell.ZoomMax);
        _zoom = new Vector2(z, z);
        _camera2D.Zoom = _zoom;
        _hudReadoutAccumS = HudReadoutIntervalS;
        QueueRedraw();
        QueueDebugOverlayRedrawIfVisible();
    }

    /// <summary>Rounds to the nearest hundredth (two decimal places) for HUD display.</summary>
    private static string FormatWorldCoord(float v) => v.ToString("F2", CultureInfo.InvariantCulture);

    /// <summary>Axis-aligned rect for global cell <paramref name="globalX"/>, <paramref name="globalY"/>; north-up Y flip via local row (see architecture.md).</summary>
    private Rect2 CellRectGlobal(int globalX, int globalY, FloorSlice floor, Vector2 gridOriginTopLeft)
    {
        var lx = globalX - floor.MinX;
        var ly = globalY - floor.MinY;
        var px = gridOriginTopLeft.X + lx * _cellSizePx;
        var py = gridOriginTopLeft.Y + (floor.Height - 1 - ly) * _cellSizePx;
        return new Rect2(px, py, _cellSizePx, _cellSizePx);
    }

    private static void EnsureUiCancelBinding()
    {
        if (!InputMap.HasAction("ui_cancel"))
        {
            GD.PushWarning("[GameRoot] Missing InputMap action 'ui_cancel'; ESC pause toggle depends on it.");
            return;
        }

        foreach (var ev in InputMap.ActionGetEvents("ui_cancel"))
        {
            if (ev is InputEventKey key && key.Keycode == Key.Escape)
            {
                return;
            }
        }

        GD.PushWarning("[GameRoot] InputMap action 'ui_cancel' does not include Escape; add Escape mapping for reliable pause toggle.");
    }
}
