#nullable enable
using System;
using Godot;

namespace SpecialPG;

/// <summary>Loads <c>res://config.ini</c> (Godot <see cref="ConfigFile"/>). Falls back to defaults if missing or invalid.</summary>
public sealed class ShellAppConfig
{
    /// <summary>Clamp for <see cref="StartupOriginPatchChebyshevRadius"/> (Chebyshev cells around global origin).</summary>
    public const int MaxStartupOriginPatchChebyshevRadius = 8;

    /// <summary>Upper bound for <see cref="WasdStepsPerSecond"/> from <c>config.ini</c> (sub-tile steps per second).</summary>
    public const int MaxWasdStepsPerSecond = 1024;

    /// <summary>Upper bound for <see cref="WasdMaxSubStepsPerPhysicsFrame"/> (catch-up steps per physics tick).</summary>
    public const int MaxWasdSubStepsPerPhysicsFrame = 256;

    /// <summary>Upper bound for <c>max_land_bridge_cells</c> in <c>config.ini</c> (Manhattan steps along the origin-to-LCC bridge).</summary>
    public const int MaxLandBridgeCellsConfig = 1_000_000;

    public static float ClampWasdStepsPerSecond(float value) =>
        Mathf.Clamp(value, 1f, MaxWasdStepsPerSecond);

    public static int ClampWasdMaxSubStepsPerPhysicsFrame(int value) =>
        Mathf.Clamp(value, 1, MaxWasdSubStepsPerPhysicsFrame);

    private const string Path = "res://config.ini";
    private const string UndoContext = "shell_runtime_config";
    private const int UndoCapacity = 128;

    public readonly record struct RuntimeShellSettings(
        float RenderScale,
        int MaxFps,
        int VsyncMode);

    public ShellAppConfig(
        float cellSizePx,
        int defaultMapWidthCells,
        int defaultMapHeightCells,
        int chunkWidthCells,
        int chunkHeightCells,
        float wasdStepsPerSecond,
        int wasdMaxSubStepsPerPhysicsFrame,
        float zoomMin,
        float zoomMax,
        float zoomStep,
        float renderScale,
        int maxFps,
        int vsyncMode,
        bool startupUseJsonSample,
        int startupSeed,
        int startupLandPercent,
        int startupOriginPatchChebyshevRadius,
        bool largeMapMode,
        bool randomizeStartupSeed,
        bool profileShellDraw,
        int maxLandBridgeCells,
        bool terrainUseSprites,
        bool decorEnabled,
        bool terrainWaterAnimate,
        bool decorUseMultimesh,
        bool decorUse3d,
        string kenneyAssetsRoot)
    {
        CellSizePx = cellSizePx;
        DefaultMapWidthCells = defaultMapWidthCells;
        DefaultMapHeightCells = defaultMapHeightCells;
        ChunkWidthCells = chunkWidthCells;
        ChunkHeightCells = chunkHeightCells;
        WasdStepsPerSecond = wasdStepsPerSecond;
        WasdMaxSubStepsPerPhysicsFrame = wasdMaxSubStepsPerPhysicsFrame;
        ZoomMin = zoomMin;
        ZoomMax = zoomMax;
        ZoomStep = zoomStep;
        RenderScale = renderScale;
        MaxFps = maxFps;
        VsyncMode = vsyncMode;
        StartupUseJsonSample = startupUseJsonSample;
        StartupSeed = startupSeed;
        StartupLandPercent = startupLandPercent;
        StartupOriginPatchChebyshevRadius = startupOriginPatchChebyshevRadius;
        LargeMapMode = largeMapMode;
        RandomizeStartupSeed = randomizeStartupSeed;
        ProfileShellDraw = profileShellDraw;
        MaxLandBridgeCells = maxLandBridgeCells;
        TerrainUseSprites = terrainUseSprites;
        DecorEnabled = decorEnabled;
        TerrainWaterAnimate = terrainWaterAnimate;
        DecorUseMultimesh = decorUseMultimesh;
        DecorUse3d = decorUse3d;
        KenneyAssetsRoot = kenneyAssetsRoot;
    }

    public float CellSizePx { get; }

    public int DefaultMapWidthCells { get; }

    public int DefaultMapHeightCells { get; }

    /// <summary>Horizontal map chunk size in cells (Factorio-style default 32).</summary>
    public int ChunkWidthCells { get; }

    /// <summary>Vertical map chunk size in cells (Factorio-style default 32).</summary>
    public int ChunkHeightCells { get; }

    /// <summary>Discrete WASD sub-tile steps per second while keys are held (<see cref="GameRoot.TickWasdDiscreteMovement"/>).</summary>
    public float WasdStepsPerSecond { get; }

    /// <summary>Upper bound on discrete WASD sub-tile steps applied in one <see cref="Godot.Node._PhysicsProcess"/> tick; config clamped to 1..<see cref="MaxWasdSubStepsPerPhysicsFrame"/> (long-frame catch-up).</summary>
    public int WasdMaxSubStepsPerPhysicsFrame { get; }

    /// <summary>
    /// Max Manhattan length of the procedural <see cref="Core.Maps.LandmassBridgeToLargestComponent"/> path from global (0,0) to the largest landmass;
    /// <c>0</c> means unlimited (legacy behavior).
    /// </summary>
    public int MaxLandBridgeCells { get; }

    public float ZoomMin { get; }

    public float ZoomMax { get; }

    public float ZoomStep { get; }

    /// <summary>Window content scale factor; 1.0 = native internal resolution.</summary>
    public float RenderScale { get; }

    /// <summary>Godot engine FPS cap; 0 means uncapped.</summary>
    public int MaxFps { get; }

    /// <summary>DisplayServer VSync mode override; -1 keeps project/default behavior.</summary>
    public int VsyncMode { get; }

    /// <summary>When true, cold start uses <c>res://maps/sample_twofloor.json</c> (then fallback) instead of procedural.</summary>
    public bool StartupUseJsonSample { get; }

    /// <summary>Seed for procedural cold start (<see cref="MapGenerationParameters"/>).</summary>
    public int StartupSeed { get; }

    /// <summary>Land percent 0–100 for procedural cold start (water = 100 − land).</summary>
    public int StartupLandPercent { get; }

    /// <summary>
    /// Chebyshev radius (in cells) for guaranteed flat land around global (0,0); see Core <c>OriginWalkabilityPatch</c>.
    /// </summary>
    public int StartupOriginPatchChebyshevRadius { get; }

    /// <summary>Enables large default map dimensions and large-map safeguards in the shell.</summary>
    public bool LargeMapMode { get; }

    /// <summary>
    /// When true, procedural cold start picks a fresh session seed each launch (not written to config);
    /// set false and use <see cref="StartupSeed"/> for reproducible maps.
    /// </summary>
    public bool RandomizeStartupSeed { get; }

    /// <summary>When true, <see cref="GameRoot"/> logs rolling average <c>_Draw</c> time (also env SPECIALPG_PROFILE_SHELL_DRAW).</summary>
    public bool ProfileShellDraw { get; }

    /// <summary>When true and the terrain atlas loads, <see cref="GameRoot"/> bakes atlas sprites instead of flat <see cref="Core.Maps.TerrainVisualColor"/> fills.</summary>
    public bool TerrainUseSprites { get; }

    /// <summary>When true and the decor atlas loads, <see cref="GameRoot"/> scatters decor sprites on land-like tiles.</summary>
    public bool DecorEnabled { get; }

    /// <summary>When true, water tiles animate via staggered atlas variant frames (chunk rebuild on frame tick).</summary>
    public bool TerrainWaterAnimate { get; }

    /// <summary>When true, decor uses <see cref="MultiMeshInstance2D"/> instead of pooled <see cref="Sprite2D"/> nodes.</summary>
    public bool DecorUseMultimesh { get; }

    /// <summary>When true, spawn Kenney GLB props on the 3D pick plane at decor scatter cells.</summary>
    public bool DecorUse3d { get; }

    /// <summary>Machine-local Kenney bundle root (diagnostics / docs); pack scripts prefer <c>KENNEY_ASSETS_ROOT</c> env.</summary>
    public string KenneyAssetsRoot { get; }

    public static ShellAppConfig LoadOrDefault()
    {
        const float defCell = 64f;
        // Human-scale cold start (stress tests: raise in config.ini or enable large_map_mode).
        const int defW = 256;
        const int defH = 256;
        const float defWasdStepsPerSecond = 42f;
        const int defWasdMaxSubStepsPerPhysicsFrame = 16;
        const float defZMin = 0.35f;
        const float defZMax = 1.75f;
        const float defZStep = 0.085f;
        const int defChunkW = 32;
        const int defChunkH = 32;
        const float defRenderScale = 1.0f;
        const int defMaxFps = 0;
        const int defVsyncMode = -1;
        const bool defStartupUseJsonSample = false;
        const int defStartupSeed = 1;
        const int defStartupLandPercent = 55;
        const int defStartupOriginPatchRadius = 2;
        const bool defLargeMapMode = false;
        const bool defRandomizeStartupSeed = true;
        const bool defProfileShellDraw = false;
        const int defMaxLandBridgeCells = 0;
        const bool defTerrainUseSprites = false;
        const bool defDecorEnabled = true;
        const bool defTerrainWaterAnimate = false;
        const bool defDecorUseMultimesh = false;
        const bool defDecorUse3d = false;
        const string defKenneyAssetsRoot = @"D:\source\KenneyAssets";

        var cf = new ConfigFile();
        var err = cf.Load(Path);
        if (err != Error.Ok)
        {
            GD.Print($"[ShellAppConfig] {Path} not loaded ({err}); using defaults.");
            return new ShellAppConfig(defCell, defW, defH, defChunkW, defChunkH, defWasdStepsPerSecond, defWasdMaxSubStepsPerPhysicsFrame, defZMin, defZMax, defZStep,
                defRenderScale, defMaxFps, defVsyncMode,
                defStartupUseJsonSample, defStartupSeed, defStartupLandPercent, defStartupOriginPatchRadius,
                defLargeMapMode, defRandomizeStartupSeed, defProfileShellDraw, defMaxLandBridgeCells,
                defTerrainUseSprites, defDecorEnabled, defTerrainWaterAnimate, defDecorUseMultimesh,
                defDecorUse3d, defKenneyAssetsRoot);
        }

        string S(string key, string d) =>
            cf.HasSectionKey("shell", key) ? cf.GetValue("shell", key).AsString() : d;

        float F(string key, float d) =>
            cf.HasSectionKey("shell", key) ? (float)cf.GetValue("shell", key).AsDouble() : d;

        int I(string key, int d) =>
            cf.HasSectionKey("shell", key) ? (int)cf.GetValue("shell", key).AsInt32() : d;
        bool B(string key, bool d) =>
            cf.HasSectionKey("shell", key) ? (bool)cf.GetValue("shell", key).AsBool() : d;

        var largeMapMode = B("large_map_mode", defLargeMapMode);
        var mapW = I("default_map_width_cells", largeMapMode ? 16384 : defW);
        var mapH = I("default_map_height_cells", largeMapMode ? 16384 : defH);

        var rawWasdSteps = F("wasd_steps_per_second", defWasdStepsPerSecond);
        var wasdSteps = ClampWasdStepsPerSecond(rawWasdSteps);
        if (!Mathf.IsEqualApprox(wasdSteps, rawWasdSteps))
        {
            GD.PushWarning(
                $"[ShellAppConfig] wasd_steps_per_second was {rawWasdSteps}; clamped to {wasdSteps} (allowed 1..{MaxWasdStepsPerSecond}).");
        }

        var rawWasdMaxSub = I("wasd_max_sub_steps_per_physics_frame", defWasdMaxSubStepsPerPhysicsFrame);
        var wasdMaxSubSteps = ClampWasdMaxSubStepsPerPhysicsFrame(rawWasdMaxSub);
        if (wasdMaxSubSteps != rawWasdMaxSub)
        {
            GD.PushWarning(
                $"[ShellAppConfig] wasd_max_sub_steps_per_physics_frame was {rawWasdMaxSub}; clamped to {wasdMaxSubSteps} (allowed 1..{MaxWasdSubStepsPerPhysicsFrame}).");
        }

        var rawMaxBridge = I("max_land_bridge_cells", defMaxLandBridgeCells);
        var maxLandBridgeCells = Mathf.Clamp(rawMaxBridge, 0, MaxLandBridgeCellsConfig);
        if (maxLandBridgeCells != rawMaxBridge)
        {
            GD.PushWarning(
                $"[ShellAppConfig] max_land_bridge_cells was {rawMaxBridge}; clamped to {maxLandBridgeCells} (allowed 0..{MaxLandBridgeCellsConfig}; 0 = unlimited).");
        }

        return new ShellAppConfig(
            F("cell_size_px", defCell),
            mapW,
            mapH,
            I("chunk_width_cells", defChunkW),
            I("chunk_height_cells", defChunkH),
            wasdSteps,
            wasdMaxSubSteps,
            F("zoom_min", defZMin),
            F("zoom_max", defZMax),
            F("zoom_step", defZStep),
            F("render_scale", defRenderScale),
            I("max_fps", defMaxFps),
            I("vsync_mode", defVsyncMode),
            B("startup_use_json_sample", defStartupUseJsonSample),
            I("startup_seed", defStartupSeed),
            Mathf.Clamp(I("startup_land_percent", defStartupLandPercent), 0, 100),
            Mathf.Clamp(I("startup_origin_patch_chebyshev_radius", defStartupOriginPatchRadius), 0,
                MaxStartupOriginPatchChebyshevRadius),
            largeMapMode,
            B("randomize_startup_seed", defRandomizeStartupSeed),
            B("profile_shell_draw", defProfileShellDraw),
            maxLandBridgeCells,
            B("terrain_use_sprites", defTerrainUseSprites),
            B("decor_enabled", defDecorEnabled),
            B("terrain_water_animate", defTerrainWaterAnimate),
            B("decor_use_multimesh", defDecorUseMultimesh),
            B("decor_use_3d", defDecorUse3d),
            S("kenney_assets_root", defKenneyAssetsRoot));
    }

    public static Error SaveRuntimeShellSettings(
        float renderScale,
        int maxFps,
        int vsyncMode)
    {
        var cf = new ConfigFile();
        var loadErr = cf.Load(Path);
        if (loadErr != Error.Ok && loadErr != Error.FileNotFound)
        {
            return loadErr;
        }

        cf.SetValue("shell", "render_scale", renderScale);
        cf.SetValue("shell", "max_fps", maxFps);
        cf.SetValue("shell", "vsync_mode", vsyncMode);
        return cf.Save(Path);
    }

    /// <summary>Writes <c>startup_land_percent</c> under <c>[shell]</c>; leaves other keys untouched.</summary>
    public static Error PersistStartupLandPercent(int landPercent)
    {
        landPercent = Mathf.Clamp(landPercent, 0, 100);
        var cf = new ConfigFile();
        var loadErr = cf.Load(Path);
        if (loadErr != Error.Ok && loadErr != Error.FileNotFound)
        {
            return loadErr;
        }

        cf.SetValue("shell", "startup_land_percent", landPercent);
        return cf.Save(Path);
    }

    /// <summary>Writes <c>startup_seed</c> under <c>[shell]</c>; leaves other keys untouched.</summary>
    public static Error PersistStartupSeed(int seed)
    {
        var cf = new ConfigFile();
        var loadErr = cf.Load(Path);
        if (loadErr != Error.Ok && loadErr != Error.FileNotFound)
        {
            return loadErr;
        }

        cf.SetValue("shell", "startup_seed", seed);
        return cf.Save(Path);
    }

    /// <summary>Writes <c>startup_origin_patch_chebyshev_radius</c> under <c>[shell]</c>.</summary>
    public static Error PersistStartupOriginPatchChebyshevRadius(int radiusCells)
    {
        radiusCells = Mathf.Clamp(radiusCells, 0, MaxStartupOriginPatchChebyshevRadius);
        var cf = new ConfigFile();
        var loadErr = cf.Load(Path);
        if (loadErr != Error.Ok && loadErr != Error.FileNotFound)
        {
            return loadErr;
        }

        cf.SetValue("shell", "startup_origin_patch_chebyshev_radius", radiusCells);
        return cf.Save(Path);
    }

    /// <summary>Writes <c>wasd_steps_per_second</c> and <c>wasd_max_sub_steps_per_physics_frame</c> under <c>[shell]</c>.</summary>
    public static Error PersistWasdMovementSettings(float wasdStepsPerSecond, int wasdMaxSubStepsPerPhysicsFrame)
    {
        var steps = ClampWasdStepsPerSecond(wasdStepsPerSecond);
        var maxSub = ClampWasdMaxSubStepsPerPhysicsFrame(wasdMaxSubStepsPerPhysicsFrame);
        var cf = new ConfigFile();
        var loadErr = cf.Load(Path);
        if (loadErr != Error.Ok && loadErr != Error.FileNotFound)
        {
            return loadErr;
        }

        cf.SetValue("shell", "wasd_steps_per_second", steps);
        cf.SetValue("shell", "wasd_max_sub_steps_per_physics_frame", maxSub);
        return cf.Save(Path);
    }

    public static void PushUndoSnapshot(RuntimeShellSettings snapshot)
    {
        UndoJournal.Push(UndoContext, snapshot, UndoCapacity);
    }

    public static bool TryPopUndoSnapshot(out RuntimeShellSettings snapshot)
        => UndoJournal.TryPop(UndoContext, out snapshot);

    public static IDisposable SuspendUndoTracking() => UndoJournal.Suspend(UndoContext);
}
