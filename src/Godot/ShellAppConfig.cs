#nullable enable
using System;
using Godot;

namespace SpecialPG;

/// <summary>Loads <c>res://config.ini</c> (Godot <see cref="ConfigFile"/>). Falls back to defaults if missing or invalid.</summary>
public sealed class ShellAppConfig
{
    private const string Path = "res://config.ini";
    private const string UndoContext = "shell_runtime_config";
    private const int UndoCapacity = 128;

    public readonly record struct RuntimeShellSettings(
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

    public ShellAppConfig(
        float cellSizePx,
        int defaultMapWidthCells,
        int defaultMapHeightCells,
        int chunkWidthCells,
        int chunkHeightCells,
        float moveSpeedPxS,
        float zoomMin,
        float zoomMax,
        float zoomStep,
        int fogRevealHalfWidthCells,
        int fogRevealHalfHeightCells,
        float fogEdgeOpacity,
        bool fogEdgeEnabled,
        bool fogGpuEnabled,
        int fogMaskPixelsPerCell,
        int fogVisualUpdateHz,
        float fogRevealLerpSpeed,
        float fogBrushHardCoreRatio,
        float fogBrushFeatherExponent,
        float fogEdgeWidthCells,
        float fogEdgeSoftness,
        int fogEdgeSamples,
        float renderScale,
        int maxFps,
        int vsyncMode,
        bool startupUseJsonSample,
        int startupSeed,
        int startupLandPercent,
        bool fogSlidingMaskEnabled,
        bool largeMapMode,
        int fogMaskWindowCells,
        int fogMaskRecenterMarginCells)
    {
        CellSizePx = cellSizePx;
        DefaultMapWidthCells = defaultMapWidthCells;
        DefaultMapHeightCells = defaultMapHeightCells;
        ChunkWidthCells = chunkWidthCells;
        ChunkHeightCells = chunkHeightCells;
        MoveSpeedPxS = moveSpeedPxS;
        ZoomMin = zoomMin;
        ZoomMax = zoomMax;
        ZoomStep = zoomStep;
        FogRevealHalfWidthCells = fogRevealHalfWidthCells;
        FogRevealHalfHeightCells = fogRevealHalfHeightCells;
        FogEdgeOpacity = fogEdgeOpacity;
        FogEdgeEnabled = fogEdgeEnabled;
        FogGpuEnabled = fogGpuEnabled;
        FogMaskPixelsPerCell = fogMaskPixelsPerCell;
        FogVisualUpdateHz = fogVisualUpdateHz;
        FogRevealLerpSpeed = fogRevealLerpSpeed;
        FogBrushHardCoreRatio = fogBrushHardCoreRatio;
        FogBrushFeatherExponent = fogBrushFeatherExponent;
        FogEdgeWidthCells = fogEdgeWidthCells;
        FogEdgeSoftness = fogEdgeSoftness;
        FogEdgeSamples = fogEdgeSamples;
        RenderScale = renderScale;
        MaxFps = maxFps;
        VsyncMode = vsyncMode;
        StartupUseJsonSample = startupUseJsonSample;
        StartupSeed = startupSeed;
        StartupLandPercent = startupLandPercent;
        FogSlidingMaskEnabled = fogSlidingMaskEnabled;
        LargeMapMode = largeMapMode;
        FogMaskWindowCells = fogMaskWindowCells;
        FogMaskRecenterMarginCells = fogMaskRecenterMarginCells;
    }

    public float CellSizePx { get; }

    public int DefaultMapWidthCells { get; }

    public int DefaultMapHeightCells { get; }

    /// <summary>Horizontal map chunk size in cells (Factorio-style default 32).</summary>
    public int ChunkWidthCells { get; }

    /// <summary>Vertical map chunk size in cells (Factorio-style default 32).</summary>
    public int ChunkHeightCells { get; }

    public float MoveSpeedPxS { get; }

    public float ZoomMin { get; }

    public float ZoomMax { get; }

    public float ZoomStep { get; }

    public int FogRevealHalfWidthCells { get; }

    public int FogRevealHalfHeightCells { get; }

    /// <summary>Opacity for the one-tile fog edge band; 0=fully transparent, 1=fully opaque fog.</summary>
    public float FogEdgeOpacity { get; }

    /// <summary>When false, use baseline tile fog without hybrid edge feathering.</summary>
    public bool FogEdgeEnabled { get; }

    /// <summary>When true, fog is rendered by mask + shader overlay path.</summary>
    public bool FogGpuEnabled { get; }

    /// <summary>Mask resolution scalar in texels-per-cell for visual fog texture.</summary>
    public int FogMaskPixelsPerCell { get; }

    /// <summary>Frequency for visual fog stamping and interpolation updates.</summary>
    public int FogVisualUpdateHz { get; }

    /// <summary>Exponential speed factor for display-mask convergence toward target-mask.</summary>
    public float FogRevealLerpSpeed { get; }

    /// <summary>Inner reveal core ratio (0..1) that stamps fully clear before feathering.</summary>
    public float FogBrushHardCoreRatio { get; }

    /// <summary>Exponent used for brush feather alpha falloff near the reveal rim.</summary>
    public float FogBrushFeatherExponent { get; }

    /// <summary>Frontier feather width in tile units; 1.0 means one full tile.</summary>
    public float FogEdgeWidthCells { get; }

    /// <summary>Falloff exponent for edge alpha; larger = steeper fade near interior.</summary>
    public float FogEdgeSoftness { get; }

    /// <summary>Number of edge gradient bands per side (quality/perf knob).</summary>
    public int FogEdgeSamples { get; }

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

    /// <summary>When true, GPU fog may use a fixed-size sliding mask on very large maps (see fog_mask_window_cells).</summary>
    public bool FogSlidingMaskEnabled { get; }

    /// <summary>Enables large default map dimensions and large-map safeguards in the shell.</summary>
    public bool LargeMapMode { get; }

    /// <summary>Sliding fog mask window size in cells (per axis), capped by map size.</summary>
    public int FogMaskWindowCells { get; }

    /// <summary>Recenter sliding fog mask when the anchor comes within this many cells of the mask edge.</summary>
    public int FogMaskRecenterMarginCells { get; }

    public static ShellAppConfig LoadOrDefault()
    {
        const float defCell = 32f;
        const int defW = 2048;
        const int defH = 1024;
        const float defSpeed = 220f * 1.15f;
        const float defZMin = 0.35f;
        const float defZMax = 1.75f;
        const float defZStep = 0.085f;
        const int defFogHalfW = 12;
        const int defFogHalfH = 8;
        const float defFogEdgeOpacity = 0.90f;
        const bool defFogEdgeEnabled = true;
        const bool defFogGpuEnabled = true;
        const int defFogMaskPixelsPerCell = 2;
        const int defFogVisualUpdateHz = 20;
        const float defFogRevealLerpSpeed = 6.0f;
        const float defFogBrushHardCoreRatio = 0.72f;
        const float defFogBrushFeatherExponent = 1.40f;
        const float defFogEdgeWidthCells = 1.60f;
        const float defFogEdgeSoftness = 1.10f;
        const int defFogEdgeSamples = 2;
        const int defChunkW = 32;
        const int defChunkH = 32;
        const float defRenderScale = 1.0f;
        const int defMaxFps = 0;
        const int defVsyncMode = -1;
        const bool defStartupUseJsonSample = false;
        const int defStartupSeed = 1;
        const int defStartupLandPercent = 55;
        const bool defFogSlidingMaskEnabled = true;
        const bool defLargeMapMode = false;
        const int defFogMaskWindowCells = 256;
        const int defFogMaskRecenterMarginCells = 48;

        var cf = new ConfigFile();
        var err = cf.Load(Path);
        if (err != Error.Ok)
        {
            GD.Print($"[ShellAppConfig] {Path} not loaded ({err}); using defaults.");
            return new ShellAppConfig(defCell, defW, defH, defChunkW, defChunkH, defSpeed, defZMin, defZMax, defZStep,
                defFogHalfW, defFogHalfH, defFogEdgeOpacity, defFogEdgeEnabled, defFogGpuEnabled, defFogMaskPixelsPerCell,
                defFogVisualUpdateHz, defFogRevealLerpSpeed, defFogBrushHardCoreRatio, defFogBrushFeatherExponent,
                defFogEdgeWidthCells, defFogEdgeSoftness, defFogEdgeSamples, defRenderScale, defMaxFps, defVsyncMode,
                defStartupUseJsonSample, defStartupSeed, defStartupLandPercent, defFogSlidingMaskEnabled, defLargeMapMode,
                defFogMaskWindowCells, defFogMaskRecenterMarginCells);
        }

        float F(string key, float d) =>
            cf.HasSectionKey("shell", key) ? (float)cf.GetValue("shell", key).AsDouble() : d;

        int I(string key, int d) =>
            cf.HasSectionKey("shell", key) ? (int)cf.GetValue("shell", key).AsInt32() : d;
        bool B(string key, bool d) =>
            cf.HasSectionKey("shell", key) ? (bool)cf.GetValue("shell", key).AsBool() : d;

        var largeMapMode = B("large_map_mode", defLargeMapMode);
        var mapW = I("default_map_width_cells", largeMapMode ? 16384 : defW);
        var mapH = I("default_map_height_cells", largeMapMode ? 16384 : defH);

        return new ShellAppConfig(
            F("cell_size_px", defCell),
            mapW,
            mapH,
            I("chunk_width_cells", defChunkW),
            I("chunk_height_cells", defChunkH),
            F("move_speed_px_s", defSpeed),
            F("zoom_min", defZMin),
            F("zoom_max", defZMax),
            F("zoom_step", defZStep),
            I("fog_reveal_half_width_cells", defFogHalfW),
            I("fog_reveal_half_height_cells", defFogHalfH),
            Mathf.Clamp(F("fog_edge_opacity", defFogEdgeOpacity), 0f, 1f),
            B("fog_edge_enabled", defFogEdgeEnabled),
            B("fog_gpu_enabled", defFogGpuEnabled),
            Mathf.Clamp(I("fog_mask_pixels_per_cell", defFogMaskPixelsPerCell), 1, 32),
            Mathf.Clamp(I("fog_visual_update_hz", defFogVisualUpdateHz), 10, 240),
            Mathf.Clamp(F("fog_reveal_lerp_speed", defFogRevealLerpSpeed), 0.5f, 40.0f),
            Mathf.Clamp(F("fog_brush_hard_core_ratio", defFogBrushHardCoreRatio), 0.2f, 0.95f),
            Mathf.Clamp(F("fog_brush_feather_exp", defFogBrushFeatherExponent), 0.5f, 4.0f),
            Mathf.Clamp(F("fog_edge_width_cells", defFogEdgeWidthCells), 0.25f, 2.5f),
            Mathf.Clamp(F("fog_edge_softness", defFogEdgeSoftness), 0.5f, 4.0f),
            Mathf.Clamp(I("fog_edge_samples", defFogEdgeSamples), 2, 16),
            F("render_scale", defRenderScale),
            I("max_fps", defMaxFps),
            I("vsync_mode", defVsyncMode),
            B("startup_use_json_sample", defStartupUseJsonSample),
            I("startup_seed", defStartupSeed),
            Mathf.Clamp(I("startup_land_percent", defStartupLandPercent), 0, 100),
            B("fog_sliding_mask_enabled", defFogSlidingMaskEnabled),
            largeMapMode,
            Mathf.Clamp(I("fog_mask_window_cells", defFogMaskWindowCells), 32, 2048),
            Mathf.Clamp(I("fog_mask_recenter_margin_cells", defFogMaskRecenterMarginCells), 8, 1024));
    }

    public static Error SaveRuntimeShellSettings(
        float renderScale,
        int maxFps,
        int vsyncMode,
        bool fogGpuEnabled,
        float fogEdgeOpacity,
        float fogEdgeWidthCells,
        float fogEdgeSoftness,
        int fogEdgeSamples,
        int fogMaskPixelsPerCell,
        int fogVisualUpdateHz,
        float fogRevealLerpSpeed,
        float fogBrushHardCoreRatio,
        float fogBrushFeatherExponent)
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
        cf.SetValue("shell", "fog_gpu_enabled", fogGpuEnabled);
        // Keep legacy key in sync for backwards compatibility with existing toggles/presets.
        cf.SetValue("shell", "fog_edge_enabled", fogGpuEnabled);
        cf.SetValue("shell", "fog_edge_opacity", fogEdgeOpacity);
        cf.SetValue("shell", "fog_edge_width_cells", fogEdgeWidthCells);
        cf.SetValue("shell", "fog_edge_softness", fogEdgeSoftness);
        cf.SetValue("shell", "fog_edge_samples", fogEdgeSamples);
        cf.SetValue("shell", "fog_mask_pixels_per_cell", fogMaskPixelsPerCell);
        cf.SetValue("shell", "fog_visual_update_hz", fogVisualUpdateHz);
        cf.SetValue("shell", "fog_reveal_lerp_speed", fogRevealLerpSpeed);
        cf.SetValue("shell", "fog_brush_hard_core_ratio", fogBrushHardCoreRatio);
        cf.SetValue("shell", "fog_brush_feather_exp", fogBrushFeatherExponent);
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
