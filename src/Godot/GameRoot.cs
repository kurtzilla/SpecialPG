#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Godot;
using SpecialPG;
using SpecialPG.Core.Maps;
using CoreTileData = SpecialPG.Core.Maps.TileData;

/// <summary>
/// Shell entry: owns the scene tree branch that will drive rendering and input; reads Core types only via normal C# references.
/// Actor pose and move rules live in <see cref="WorldState"/>.
/// </summary>
public partial class GameRoot : Node2D
{
    private const string SampleMapPath = "res://maps/sample_twofloor.json";
    private const string ShellRevisionLogPath = "res://../../docs/shell-feature-revision-log.md";

    /// <summary>Bump when you add user-visible shell behavior; add a line to <see cref="ShellFeatureChangelogLines"/>.</summary>
    private const int ShellFeatureRevision = 17;

    /// <summary>First fog reveal after session start uses this multiplier on configured half-extents (linear per axis).</summary>
    private const int InitialFogRevealHalfExtentMultiplier = 1;

    private static readonly string[] ShellFeatureChangelogLines =
    {
        "config.ini shell tuning; Camera2D + continuous WASD; zoom (wheel, =/-, keypad); upper-right world XY (2 decimals).",
        "Grid: world origin; darker mid-grey lines; viewport culling; JSON defaults from config when no file.",
        "Debug placeholders: seeded scatter (blocked tiles, extra stairs, sample path for Paths toggle).",
        "F5 debug overlay: round toggles (upper-left); walkability / links / ray / paths.",
        "3D ray pick → GridPickResult (see architecture Interaction).",
        "Core WorldState + walkable tiles + vertical link traversal rules.",
        "HUD: live map stats, source, integrity.",
        "WorldMap JSON load; MapIntegrity errors reject bad files.",
        "Cold start at world (0,0); fog-of-war reveal (2× half-extents once); shell default map 2048×1024 cells; wheel zoom via unhandled input.",
        "Root ShellHudLayer: ESC pause menu (Quit first, Resume); HUD off GridMap CanvasLayer.",
        "Upper-right world XY uses fixed 2 decimal places (F2), not 2 significant figures.",
        "Upper-right FPS line above coords; public GameRoot.ShellFps (smoothed).",
        "Perf: ray-pick HUD only on cell change; physics QueueRedraw when move/zoom changes.",
        "Top-right stack: perf + FPS + coords + FLR + ZOM.",
        "HUD readouts throttled (~12Hz) to stay snappy without per-frame text churn.",
        "Config knobs: render_scale / max_fps / vsync_mode for quick perf profiling.",
        "Fog: GPU mask + shader overlay path (world-space texture) with CPU legacy toggle on F6.",
    };

    private static readonly Color GridLineColor = new(0.17f, 0.18f, 0.21f, 0.62f);

    private static readonly Color FogOverlayColor = new(0.04f, 0.05f, 0.08f, 0.90f);
    private Color _fogVisualColor = FogOverlayColor;
    private FogRenderMode _fogRenderMode = FogRenderMode.GpuMaskOverlay;
    private float _fogEdgeWidthCells = 1.0f;
    private float _fogEdgeSoftness = 1.6f;
    private int _fogEdgeSamples = 6;
    private int _fogMaskPixelsPerCell = 8;
    private int _fogVisualUpdateHz = 60;
    private float _fogRevealLerpSpeed = 10.0f;
    private float _fogBrushHardCoreRatio = 0.72f;
    private float _fogBrushFeatherExponent = 1.40f;

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
    private double _visualFogStampAccumS;
    private bool _fogFirstRevealApplied;

    private WorldState _world = null!;
    private int[] _presentZs = Array.Empty<int>();
    private DebugGridOverlay? _debugGridOverlay;
    private FogOverlayRenderer? _fogOverlayRenderer;
    private DebugChannelPanel? _debugChannelPanel;
    private string _mapSourceSummary = "";
    private int _integrityErrorCount;
    private int _integrityWarningCount;
    private bool _warnedEscMissingHud;

    private enum FogRenderMode
    {
        LegacyTileCpu,
        GpuMaskOverlay,
    }

    /// <summary>Dev-only polyline on Z=0 for the Paths debug channel (see <see cref="ApplyDebugPlaceholders"/>).</summary>
    private readonly List<Vector2I> _debugPlaceholderPath = new();

    private const int DebugPlaceholderRngSeed = unchecked((int)0xC0FFEE);
    private const double HudReadoutIntervalS = 1.0 / 12.0;
    private double _visualFogStampIntervalS = 1.0 / 60.0;

    private readonly record struct RuntimeConfigSnapshot(
        float RenderScale,
        int MaxFps,
        int VsyncMode,
        bool FogGpuEnabled,
        float FogEdgeOpacity,
        float FogEdgeWidthCells,
        float FogEdgeSoftness,
        int FogEdgeSamples,
        int FogMaskPixelsPerCell,
        int FogVisualUpdateHz,
        float FogRevealLerpSpeed,
        float FogBrushHardCoreRatio,
        float FogBrushFeatherExponent);

    public int ShellMapWidth => _world.Map.Width;

    public int ShellMapHeight => _world.Map.Height;

    public int ShellMapMinX => _world.Map.MinX;

    public int ShellMapMinY => _world.Map.MinY;

    public int ShellActorZ => _world.ActorZ;

    public float ShellCellSizePixels => _cellSizePx;

    public float MoveSpeedPxS => _shell.MoveSpeedPxS;
    public int ShellFogEdgeSamples => _fogEdgeSamples;
    public float ShellFogEdgeSoftness => _fogEdgeSoftness;
    public float ShellFogEdgeWidthCells => _fogEdgeWidthCells;
    public int ShellFogVisualUpdateHz => _fogVisualUpdateHz;
    public float ShellFogRevealLerpSpeed => _fogRevealLerpSpeed;
    public float ShellFogBrushHardCoreRatio => _fogBrushHardCoreRatio;
    public float ShellFogBrushFeatherExponent => _fogBrushFeatherExponent;

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
        _fogVisualColor = new Color(FogOverlayColor.R, FogOverlayColor.G, FogOverlayColor.B,
            Mathf.Clamp(_shell.FogEdgeOpacity, 0f, 1f));
        _fogRenderMode = _shell.FogGpuEnabled ? FogRenderMode.GpuMaskOverlay : FogRenderMode.LegacyTileCpu;
        _fogEdgeWidthCells = _shell.FogEdgeWidthCells;
        _fogEdgeSoftness = _shell.FogEdgeSoftness;
        _fogEdgeSamples = _shell.FogEdgeSamples;
        _fogMaskPixelsPerCell = _shell.FogMaskPixelsPerCell;
        _fogVisualUpdateHz = _shell.FogVisualUpdateHz;
        _fogRevealLerpSpeed = _shell.FogRevealLerpSpeed;
        _fogBrushHardCoreRatio = _shell.FogBrushHardCoreRatio;
        _fogBrushFeatherExponent = _shell.FogBrushFeatherExponent;
        _visualFogStampIntervalS = 1.0 / Math.Max(1, _fogVisualUpdateHz);
        ApplyPerfPreset(_shell.RenderScale, _shell.MaxFps, _shell.VsyncMode, "config", trackUndo: false, persist: false);
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

        _fogOverlayRenderer = GetNodeOrNull<FogOverlayRenderer>("FogOverlayRenderer");
        if (_fogOverlayRenderer is null)
        {
            GD.PushWarning("[GameRoot] Missing FogOverlayRenderer node; fallback CPU fog tile mode is forced.");
            _fogRenderMode = FogRenderMode.LegacyTileCpu;
        }
        else
        {
            _fogOverlayRenderer.Visible = _fogRenderMode == FogRenderMode.GpuMaskOverlay;
            _fogOverlayRenderer.ConfigureRevealSmoothing(_fogRevealLerpSpeed, _fogBrushHardCoreRatio,
                _fogBrushFeatherExponent, _fogVisualUpdateHz);
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

        var mapChain = new ChainedWorldMapSource(
            new JsonWorldMapSource(SampleMapPath),
            new FallbackSampleWorldMapSource(_shell));
        var map = mapChain.TryBuildWorldMap(out _mapSourceSummary, out var mapBuildError);
        if (map is null)
        {
            GD.PrintErr($"[GameRoot] Map chain failed unexpectedly: {mapBuildError}");
            map = SampleWorldMapBootstrap.CreateFallbackMap(_shell);
            _mapSourceSummary = "Emergency fallback after map source chain failure";
        }

        var floor0 = map.GetOrCreateFloor(0);
        var spawnGx = floor0.MinX + floor0.Width / 2;
        var spawnGy = floor0.MinY + floor0.Height / 2;
        _world = new WorldState(map, spawnGx, spawnGy, 0);

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
        var centerGx = floor0.MinX + floor0.Width / 2;
        var centerGy = floor0.MinY + floor0.Height / 2;
        if (!TryFindWalkableSpawnNearCenter(floor0, centerGx, centerGy, out var walkGx, out var walkGy))
        {
            GD.PrintErr("[GameRoot] No walkable cell found near map center; actor left at clamped spawn.");
        }
        else
        {
            _world.SetActorCellFromShell(walkGx, walkGy, _world.ActorZ);
        }

        if (_shellPlayer is not null)
        {
            SnapPlayerToActorCell();
        }
        ConfigureFogOverlayForActiveFloor();
        PrimeInitialFogVisual();
        RefreshShellHud();
        LogShellFeatureRevisionToFile();
        QueueRedraw();
        QueueDebugOverlayRedrawIfVisible();

        parent?.GetNodeOrNull<InteractionRay3D>("Interaction3D")?.RebuildPickGeometry();

        var floor = ActiveFloorSlice;
        var sample = floor.Get(1, 1);
        var activeCamera = GetViewport().GetCamera2D();
        var cameraName = activeCamera is null ? "(none)" : activeCamera.Name.ToString();
        GD.Print($"[GameRoot] Ready: map {_world.Map.Width}x{_world.Map.Height}, cell={_cellSizePx}px, floors=[{string.Join(",", _presentZs)}], ActorZ={_world.ActorZ}, source=\"{_mapSourceSummary}\", hudBound={_shellHud is not null}, activeCamera2D={cameraName}, fogMode={_fogRenderMode}, edgeWidth={_fogEdgeWidthCells:F2}, edgeSoftness={_fogEdgeSoftness:F2}, edgeSamples={_fogEdgeSamples}, maskPpc={_fogMaskPixelsPerCell}, sample TileKind={sample.TileKind}.");
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

    public void ApplyFogPreset(bool edgeEnabled, float edgeOpacity, float edgeWidthCells, float edgeSoftness, int edgeSamples,
        string presetName, bool trackUndo = true, bool persist = true)
    {
        if (trackUndo)
        {
            PushRuntimeConfigUndoSnapshot();
        }

        _fogRenderMode = edgeEnabled ? FogRenderMode.GpuMaskOverlay : FogRenderMode.LegacyTileCpu;
        _fogEdgeWidthCells = Mathf.Clamp(edgeWidthCells, 0.25f, 2.5f);
        _fogEdgeSoftness = Mathf.Clamp(edgeSoftness, 0.5f, 4.0f);
        _fogEdgeSamples = Mathf.Clamp(edgeSamples, 2, 16);
        _fogVisualColor = new Color(FogOverlayColor.R, FogOverlayColor.G, FogOverlayColor.B, Mathf.Clamp(edgeOpacity, 0f, 1f));
        _fogOverlayRenderer?.ConfigureStyle(_fogVisualColor, _fogEdgeWidthCells, _fogEdgeSoftness, _fogEdgeSamples,
            _fogRenderMode == FogRenderMode.GpuMaskOverlay);

        RefreshShellHud();
        QueueRedraw();
        if (persist)
        {
            PersistRuntimeShellSettings();
        }

        GD.Print(
            $"[GameRoot] Fog preset={presetName} mode={_fogRenderMode}, edge_opacity={_fogVisualColor.A:F2}, edge_width={_fogEdgeWidthCells:F2}, edge_softness={_fogEdgeSoftness:F2}, edge_samples={_fogEdgeSamples}.");
    }

    public void SetFogEdgeSamples(int edgeSamples, string sourceTag = "runtime", bool trackUndo = true, bool persist = true)
    {
        if (trackUndo)
        {
            PushRuntimeConfigUndoSnapshot();
        }

        _fogEdgeSamples = Mathf.Clamp(edgeSamples, 2, 16);
        _fogOverlayRenderer?.ConfigureStyle(_fogVisualColor, _fogEdgeWidthCells, _fogEdgeSoftness, _fogEdgeSamples,
            _fogRenderMode == FogRenderMode.GpuMaskOverlay);
        RefreshShellHud();
        QueueRedraw();
        if (persist)
        {
            PersistRuntimeShellSettings();
        }

        GD.Print($"[GameRoot] Fog edge samples updated ({sourceTag}) => {_fogEdgeSamples}.");
    }

    public void SetFogEdgeSoftness(float edgeSoftness, string sourceTag = "runtime", bool trackUndo = true, bool persist = true)
    {
        if (trackUndo)
        {
            PushRuntimeConfigUndoSnapshot();
        }

        _fogEdgeSoftness = Mathf.Clamp(edgeSoftness, 0.5f, 4.0f);
        _fogOverlayRenderer?.ConfigureStyle(_fogVisualColor, _fogEdgeWidthCells, _fogEdgeSoftness, _fogEdgeSamples,
            _fogRenderMode == FogRenderMode.GpuMaskOverlay);
        RefreshShellHud();
        QueueRedraw();
        if (persist)
        {
            PersistRuntimeShellSettings();
        }

        GD.Print($"[GameRoot] Fog edge softness updated ({sourceTag}) => {_fogEdgeSoftness:F2}.");
    }

    public void SetFogEdgeWidthCells(float edgeWidthCells, string sourceTag = "runtime", bool trackUndo = true, bool persist = true)
    {
        if (trackUndo)
        {
            PushRuntimeConfigUndoSnapshot();
        }

        _fogEdgeWidthCells = Mathf.Clamp(edgeWidthCells, 0.25f, 2.5f);
        _fogOverlayRenderer?.ConfigureStyle(_fogVisualColor, _fogEdgeWidthCells, _fogEdgeSoftness, _fogEdgeSamples,
            _fogRenderMode == FogRenderMode.GpuMaskOverlay);
        RefreshShellHud();
        QueueRedraw();
        if (persist)
        {
            PersistRuntimeShellSettings();
        }

        GD.Print($"[GameRoot] Fog edge width updated ({sourceTag}) => {_fogEdgeWidthCells:F2}.");
    }

    public void SetFogVisualUpdateHz(int hz, string sourceTag = "runtime", bool trackUndo = true, bool persist = true)
    {
        if (trackUndo)
        {
            PushRuntimeConfigUndoSnapshot();
        }

        _fogVisualUpdateHz = Mathf.Clamp(hz, 10, 240);
        _visualFogStampIntervalS = 1.0 / _fogVisualUpdateHz;
        RefreshShellHud();
        if (persist)
        {
            PersistRuntimeShellSettings();
        }

        GD.Print($"[GameRoot] Fog visual update hz updated ({sourceTag}) => {_fogVisualUpdateHz}.");
    }

    public void SetFogRevealLerpSpeed(float speed, string sourceTag = "runtime", bool trackUndo = true, bool persist = true)
    {
        if (trackUndo)
        {
            PushRuntimeConfigUndoSnapshot();
        }

        _fogRevealLerpSpeed = Mathf.Clamp(speed, 0.5f, 40.0f);
        _fogOverlayRenderer?.ConfigureRevealSmoothing(_fogRevealLerpSpeed, _fogBrushHardCoreRatio, _fogBrushFeatherExponent,
            _fogVisualUpdateHz);
        RefreshShellHud();
        if (persist)
        {
            PersistRuntimeShellSettings();
        }

        GD.Print($"[GameRoot] Fog reveal lerp speed updated ({sourceTag}) => {_fogRevealLerpSpeed:F2}.");
    }

    public void SetFogBrushHardCoreRatio(float ratio, string sourceTag = "runtime", bool trackUndo = true, bool persist = true)
    {
        if (trackUndo)
        {
            PushRuntimeConfigUndoSnapshot();
        }

        _fogBrushHardCoreRatio = Mathf.Clamp(ratio, 0.2f, 0.95f);
        _fogOverlayRenderer?.ConfigureRevealSmoothing(_fogRevealLerpSpeed, _fogBrushHardCoreRatio, _fogBrushFeatherExponent,
            _fogVisualUpdateHz);
        RefreshShellHud();
        if (persist)
        {
            PersistRuntimeShellSettings();
        }

        GD.Print($"[GameRoot] Fog brush hard-core ratio updated ({sourceTag}) => {_fogBrushHardCoreRatio:F2}.");
    }

    public void SetFogBrushFeatherExponent(float exponent, string sourceTag = "runtime", bool trackUndo = true,
        bool persist = true)
    {
        if (trackUndo)
        {
            PushRuntimeConfigUndoSnapshot();
        }

        _fogBrushFeatherExponent = Mathf.Clamp(exponent, 0.5f, 4.0f);
        _fogOverlayRenderer?.ConfigureRevealSmoothing(_fogRevealLerpSpeed, _fogBrushHardCoreRatio, _fogBrushFeatherExponent,
            _fogVisualUpdateHz);
        RefreshShellHud();
        if (persist)
        {
            PersistRuntimeShellSettings();
        }

        GD.Print($"[GameRoot] Fog brush feather exponent updated ({sourceTag}) => {_fogBrushFeatherExponent:F2}.");
    }

    public void SetFogSmoothingSettings(int visualUpdateHz, float revealLerpSpeed, float brushHardCoreRatio,
        float brushFeatherExponent, string sourceTag = "runtime", bool trackUndo = true, bool persist = true)
    {
        if (trackUndo)
        {
            PushRuntimeConfigUndoSnapshot();
        }

        _fogVisualUpdateHz = Mathf.Clamp(visualUpdateHz, 10, 240);
        _visualFogStampIntervalS = 1.0 / _fogVisualUpdateHz;
        _fogRevealLerpSpeed = Mathf.Clamp(revealLerpSpeed, 0.5f, 40.0f);
        _fogBrushHardCoreRatio = Mathf.Clamp(brushHardCoreRatio, 0.2f, 0.95f);
        _fogBrushFeatherExponent = Mathf.Clamp(brushFeatherExponent, 0.5f, 4.0f);
        _fogOverlayRenderer?.ConfigureRevealSmoothing(_fogRevealLerpSpeed, _fogBrushHardCoreRatio, _fogBrushFeatherExponent,
            _fogVisualUpdateHz);
        RefreshShellHud();
        if (persist)
        {
            PersistRuntimeShellSettings();
        }

        GD.Print(
            $"[GameRoot] Fog smoothing updated ({sourceTag}) => hz={_fogVisualUpdateHz}, lerp={_fogRevealLerpSpeed:F2}, core={_fogBrushHardCoreRatio:F2}, feather={_fogBrushFeatherExponent:F2}.");
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

        var p = _camera2D.GlobalPosition;
        _shellHud.SetPlayerPositionText($"{FormatWorldCoord(p.X)}, {FormatWorldCoord(p.Y)}");
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

        const float moveEpsSq = 1e-6f;
        var pos = _shellPlayer.Position;
        var moved = float.IsNaN(_lastPhysicsRedrawPlayerPos.X)
                    || (pos - _lastPhysicsRedrawPlayerPos).LengthSquared() > moveEpsSq;
        var zoomed = float.IsNaN(_lastPhysicsRedrawZoom.X) || _lastPhysicsRedrawZoom != _zoom;
        if (!moved && !zoomed)
        {
            return;
        }

        _lastPhysicsRedrawPlayerPos = pos;
        _lastPhysicsRedrawZoom = _zoom;
        _fogOverlayRenderer?.AdvanceRevealAnimation(delta);
        StampVisualFogFromPlayer(delta);
        QueueRedraw();
        QueueDebugOverlayRedrawIfVisible();
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

        if (key.PhysicalKeycode == Key.F6)
        {
            PushRuntimeConfigUndoSnapshot();
            _fogRenderMode = _fogRenderMode == FogRenderMode.GpuMaskOverlay
                ? FogRenderMode.LegacyTileCpu
                : FogRenderMode.GpuMaskOverlay;
            _fogOverlayRenderer?.ConfigureStyle(_fogVisualColor, _fogEdgeWidthCells, _fogEdgeSoftness, _fogEdgeSamples,
                _fogRenderMode == FogRenderMode.GpuMaskOverlay);
            PersistRuntimeShellSettings();
            GD.Print($"[GameRoot] Fog mode switched to {_fogRenderMode}.");
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

        var floor = ActiveFloorSlice;
        var origin = GetGridOrigin(floor.Width, floor.Height);
        var visible = GetExpandedVisibleCullRect();
        GetVisibleGlobalCellBounds(floor, visible, out var minGx, out var maxGx, out var minGy, out var maxGy);

        for (var gy = minGy; gy <= maxGy; gy++)
        {
            for (var gx = minGx; gx <= maxGx; gx++)
            {
                var rect = CellRectGlobal(gx, gy, floor, origin);
                if (!rect.Intersects(visible))
                {
                    continue;
                }

                var tile = floor.Get(gx, gy);
                DrawRect(rect, TileColor(tile.TileKind), true);
            }
        }

        DrawGridLines(origin, floor.Width, floor.Height, visible);
        DrawFogOverlay(origin, floor, visible, minGx, maxGx, minGy, maxGy);
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
        ConfigureFogOverlayForActiveFloor();
        RefreshShellHud();
        MarkShellViewDirty();
        QueueRedraw();
        QueueDebugOverlayRedrawIfVisible();

        return true;
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
        ConfigureFogOverlayForActiveFloor();
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

                slice.Set(x, y, new CoreTileData
                {
                    TileKind = t.TileKind,
                    Flags = (byte)(t.Flags | TileFlags.Blocked),
                    Variant = t.Variant,
                });
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
            if (!TileTraversal.IsWalkable(t))
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

        _shellHud.SetBootText(
            "WASD — move (continuous)   |   Wheel / = - / keypad +/- — zoom   |   E / Enter — link   |   [ ] / PgUp/PgDn — floor   |   F5 — debug   |   F6 — fog mode   |   Ctrl+Z — undo config   |   ESC — pause / Quit");
        _shellHud.SetRevisionReadout($"REV {ShellFeatureRevision}");
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
        _activeVsyncMode,
        _fogRenderMode == FogRenderMode.GpuMaskOverlay,
        _fogVisualColor.A,
        _fogEdgeWidthCells,
        _fogEdgeSoftness,
        _fogEdgeSamples,
        _fogMaskPixelsPerCell,
        _fogVisualUpdateHz,
        _fogRevealLerpSpeed,
        _fogBrushHardCoreRatio,
        _fogBrushFeatherExponent);

    private void PushRuntimeConfigUndoSnapshot()
    {
        var s = CaptureRuntimeConfigSnapshot();
        ShellAppConfig.PushUndoSnapshot(new ShellAppConfig.RuntimeShellSettings(
            s.RenderScale,
            s.MaxFps,
            s.VsyncMode,
            s.FogGpuEnabled,
            s.FogEdgeOpacity,
            s.FogEdgeWidthCells,
            s.FogEdgeSoftness,
            s.FogEdgeSamples,
            s.FogMaskPixelsPerCell,
            s.FogVisualUpdateHz,
            s.FogRevealLerpSpeed,
            s.FogBrushHardCoreRatio,
            s.FogBrushFeatherExponent));
    }

    private bool TryUndoRuntimeConfigChange()
    {
        if (!ShellAppConfig.TryPopUndoSnapshot(out var s))
        {
            return false;
        }

        ApplyPerfPreset(s.RenderScale, s.MaxFps, s.VsyncMode, "undo", trackUndo: false, persist: false);
        ApplyFogPreset(s.FogGpuEnabled, s.FogEdgeOpacity, s.FogEdgeWidthCells, s.FogEdgeSoftness, s.FogEdgeSamples,
            "undo", trackUndo: false, persist: false);
        _fogMaskPixelsPerCell = Mathf.Clamp(s.FogMaskPixelsPerCell, 1, 32);
        _fogVisualUpdateHz = Mathf.Clamp(s.FogVisualUpdateHz, 10, 240);
        _visualFogStampIntervalS = 1.0 / _fogVisualUpdateHz;
        _fogRevealLerpSpeed = Mathf.Clamp(s.FogRevealLerpSpeed, 0.5f, 40.0f);
        _fogBrushHardCoreRatio = Mathf.Clamp(s.FogBrushHardCoreRatio, 0.2f, 0.95f);
        _fogBrushFeatherExponent = Mathf.Clamp(s.FogBrushFeatherExponent, 0.5f, 4.0f);
        _fogOverlayRenderer?.ConfigureRevealSmoothing(_fogRevealLerpSpeed, _fogBrushHardCoreRatio, _fogBrushFeatherExponent,
            _fogVisualUpdateHz);
        PersistRuntimeShellSettings();
        return true;
    }

    private void PersistRuntimeShellSettings()
    {
        var err = ShellAppConfig.SaveRuntimeShellSettings(
            _activeRenderScale,
            _activeMaxFps,
            _activeVsyncMode,
            _fogRenderMode == FogRenderMode.GpuMaskOverlay,
            _fogVisualColor.A,
            _fogEdgeWidthCells,
            _fogEdgeSoftness,
            _fogEdgeSamples,
            _fogMaskPixelsPerCell,
            _fogVisualUpdateHz,
            _fogRevealLerpSpeed,
            _fogBrushHardCoreRatio,
            _fogBrushFeatherExponent);
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

    private string FogModeLabel() => _fogRenderMode switch
    {
        FogRenderMode.LegacyTileCpu => "legacy tile cpu",
        _ => "gpu mask overlay",
    };

    /// <summary>
    /// Pixel origin (top-left of the north-west tile rect) chosen so the whole <c>width × height</c> board
    /// is centered on <c>(0, 0)</c> in <c>GridMap</c> space; local row <c>ly = y - MinY</c> for north-up flip (see architecture.md).
    /// </summary>
    private Vector2 GetGridOrigin(int width, int height)
    {
        var cs = _cellSizePx;
        return new Vector2(-width * cs * 0.5f, -height * cs * 0.5f);
    }

    private void DrawGridLines(Vector2 gridOriginTopLeft, int width, int height, Rect2 visible)
    {
        var cs = _cellSizePx;
        var boardW = width * cs;
        var boardH = height * cs;
        var x0 = gridOriginTopLeft.X;
        var y0 = gridOriginTopLeft.Y;
        var x1 = x0 + boardW;
        var y1 = y0 + boardH;
        const float chunkBorderWidth = 0.85f;
        const float regularLineWidth = chunkBorderWidth * 0.5f;
        var chunkW = Math.Max(1, _shell.ChunkWidthCells);
        var chunkH = Math.Max(1, _shell.ChunkHeightCells);

        var minIx = Mathf.Clamp(Mathf.FloorToInt((visible.Position.X - x0) / cs), 0, width);
        var maxIx = Mathf.Clamp(Mathf.CeilToInt((visible.Position.X + visible.Size.X - x0) / cs), 0, width);
        for (var i = minIx; i <= maxIx; i++)
        {
            var x = x0 + i * cs;
            var widthPx = (i % chunkW == 0) ? chunkBorderWidth : regularLineWidth;
            DrawLine(new Vector2(x, y0), new Vector2(x, y1), GridLineColor, widthPx, true);
        }

        var minJy = Mathf.Clamp(Mathf.FloorToInt((visible.Position.Y - y0) / cs), 0, height);
        var maxJy = Mathf.Clamp(Mathf.CeilToInt((visible.Position.Y + visible.Size.Y - y0) / cs), 0, height);
        for (var j = minJy; j <= maxJy; j++)
        {
            var y = y0 + j * cs;
            var widthPx = (j % chunkH == 0) ? chunkBorderWidth : regularLineWidth;
            DrawLine(new Vector2(x0, y), new Vector2(x1, y), GridLineColor, widthPx, true);
        }
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
        var origin = GetGridOrigin(floor.Width, floor.Height);
        var boardW = floor.Width * _cellSizePx;
        var boardH = floor.Height * _cellSizePx;
        if (world.X < origin.X || world.Y < origin.Y || world.X >= origin.X + boardW ||
            world.Y >= origin.Y + boardH)
        {
            return false;
        }

        var c = WorldToCellFloor(world, floor);
        var t = floor.Get(c.X, c.Y);
        return TileTraversal.IsWalkable(t);
    }

    private void ApplyFogAroundActor()
    {
        var isInitialReveal = !_fogFirstRevealApplied;
        var mult = isInitialReveal ? InitialFogRevealHalfExtentMultiplier : 1;
        _fogFirstRevealApplied = true;
        var halfW = Math.Max(0, _shell.FogRevealHalfWidthCells) * mult;
        var halfH = Math.Max(0, _shell.FogRevealHalfHeightCells) * mult;
        var radius = Math.Max(halfW, halfH);
        var slice = ActiveFloorSlice;
        _world.Fog.ApplyCircle(0, slice.Z, _world.ActorX, _world.ActorY, radius, slice.MinX, slice.MinY,
            slice.Width, slice.Height);
        _fogOverlayRenderer?.EnsureFloorMask(slice.Z, slice.MinX, slice.MinY, slice.Width, slice.Height);
        _fogOverlayRenderer?.StampRevealCircle(slice.Z, _world.ActorX, _world.ActorY, radius);
        if (isInitialReveal)
        {
            // Spawn should start with visible reveal immediately, not after interpolation catches up.
            _fogOverlayRenderer?.SnapDisplayToTarget(slice.Z);
        }
    }

    private void PrimeInitialFogVisual()
    {
        if (_fogOverlayRenderer is null || _fogRenderMode != FogRenderMode.GpuMaskOverlay)
        {
            return;
        }

        var slice = ActiveFloorSlice;
        var radius = Math.Max(_shell.FogRevealHalfWidthCells, _shell.FogRevealHalfHeightCells);
        _fogOverlayRenderer.EnsureFloorMask(slice.Z, slice.MinX, slice.MinY, slice.Width, slice.Height);
        _fogOverlayRenderer.SetActiveFloor(slice.Z);
        _fogOverlayRenderer.StampRevealCircle(slice.Z, _world.ActorX, _world.ActorY, radius);
        _fogOverlayRenderer.SnapDisplayToTarget(slice.Z);
        _fogOverlayRenderer.QueueRedraw();
    }

    private void StampVisualFogFromPlayer(double delta)
    {
        if (_shellPlayer is null || _fogOverlayRenderer is null || _fogRenderMode != FogRenderMode.GpuMaskOverlay)
        {
            return;
        }

        _visualFogStampAccumS += delta;
        if (_visualFogStampAccumS < _visualFogStampIntervalS)
        {
            return;
        }

        _visualFogStampAccumS = 0.0;
        var floor = ActiveFloorSlice;
        var origin = GetGridOrigin(floor.Width, floor.Height);
        var localX = (_shellPlayer.Position.X - origin.X) / _cellSizePx;
        var rowY = (_shellPlayer.Position.Y - origin.Y) / _cellSizePx;
        var localY = floor.Height - 1f - rowY;
        var gx = floor.MinX + localX;
        var gy = floor.MinY + localY;
        var radius = Math.Max(_shell.FogRevealHalfWidthCells, _shell.FogRevealHalfHeightCells);
        _fogOverlayRenderer.EnsureFloorMask(floor.Z, floor.MinX, floor.MinY, floor.Width, floor.Height);
        _fogOverlayRenderer.StampRevealCircleAtGlobal(floor.Z, gx, gy, radius);
    }

    private void DrawFogOverlay(Vector2 gridOriginTopLeft, FloorSlice floor, Rect2 visible, int minGx, int maxGx,
        int minGy, int maxGy)
    {
        if (_fogRenderMode == FogRenderMode.GpuMaskOverlay)
        {
            return;
        }

        for (var gy = minGy; gy <= maxGy; gy++)
        {
            for (var gx = minGx; gx <= maxGx; gx++)
            {
                if (_world.Fog.IsRevealed(0, floor.Z, gx, gy, floor.MinX, floor.MinY, floor.Width, floor.Height))
                {
                    continue;
                }

                var rect = CellRectGlobal(gx, gy, floor, gridOriginTopLeft);
                if (!rect.Intersects(visible))
                {
                    continue;
                }

                DrawRect(rect, _fogVisualColor, true);
            }
        }
    }

    private void ConfigureFogOverlayForActiveFloor()
    {
        if (_fogOverlayRenderer is null)
        {
            return;
        }

        var floor = ActiveFloorSlice;
        var origin = GetGridOrigin(floor.Width, floor.Height);
        _fogOverlayRenderer.ConfigureBoard(origin, floor.Width, floor.Height, _cellSizePx, _fogMaskPixelsPerCell);
        _fogOverlayRenderer.EnsureFloorMask(floor.Z, floor.MinX, floor.MinY, floor.Width, floor.Height);
        _fogOverlayRenderer.SetActiveFloor(floor.Z);
        _fogOverlayRenderer.ConfigureStyle(_fogVisualColor, _fogEdgeWidthCells, _fogEdgeSoftness, _fogEdgeSamples,
            _fogRenderMode == FogRenderMode.GpuMaskOverlay);
    }

    private bool IsRevealedCell(FloorSlice floor, int x, int y)
    {
        if (!floor.Contains(x, y))
        {
            return false;
        }

        return _world.Fog.IsRevealed(0, floor.Z, x, y, floor.MinX, floor.MinY, floor.Width, floor.Height);
    }

    /// <summary>Prefer map center, but shells may place blocked tiles there (fallback map, debug RNG).</summary>
    private static bool TryFindWalkableSpawnNearCenter(FloorSlice floor, int centerGx, int centerGy, out int sx,
        out int sy)
    {
        var maxR = Math.Max(floor.Width, floor.Height);
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

                    if (TileTraversal.IsWalkable(floor.Get(gx, gy)))
                    {
                        sx = gx;
                        sy = gy;
                        return true;
                    }
                }
            }
        }

        sx = sy = 0;
        return false;
    }

    private void SnapPlayerToActorCell()
    {
        if (_shellPlayer is null)
        {
            return;
        }

        var f = ActiveFloorSlice;
        _shellPlayer.Position = CellCenterWorld(_world.ActorX, _world.ActorY, f);
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
        var c = WorldToCellFloor(_shellPlayer.Position, floor);
        _world.SetActorCellFromShell(c.X, c.Y, _world.ActorZ);
        var cellChanged = c != _lastSyncedCell || _world.ActorZ != _lastSyncedActorZ;
        if (cellChanged)
        {
            ApplyFogAroundActor();
        }

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

    private static Color TileColor(ushort tileKind)
    {
        return tileKind switch
        {
            1 => new Color(0.22f, 0.31f, 0.42f),
            2 => new Color(0.26f, 0.36f, 0.48f),
            _ => new Color(0.30f, 0.40f, 0.50f)
        };
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
