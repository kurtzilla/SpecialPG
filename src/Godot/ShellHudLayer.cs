#nullable enable
using Godot;
using SpecialPG;
using SpecialPG.Core.Maps;

/// <summary>
/// Root-level shell HUD + pause menu (ESC toggles). Quit lives at the top of the pause menu only.
/// </summary>
public partial class ShellHudLayer : Control
{
    private static readonly Color PresetBtnNormalColor = new(0.72f, 0.75f, 0.80f, 1f);
    private static readonly Color PresetBtnSelectedColor = new(0.45f, 1.0f, 0.55f, 1f);
    private const string NativeLabel = "Native 4K";
    private const string BalancedLabel = "Balanced";
    private const string ProbeLabel = "Probe";
    private const string SmoothLabel = "Smooth 60";
    private const float ResetPerfRenderScale = 1.0f;
    private const int ResetPerfMaxFps = 0;
    private const int ResetPerfVsyncMode = -1;

    private GameRoot? _gridRoot;
    private Label _bootLabel = null!;
    private Label _perfLabel = null!;
    private Label _fpsLabel = null!;
    private Label _floorLabel = null!;
    private Label _zoomLabel = null!;
    private Label _revisionLabel = null!;
    private Label _playerFootReadoutLabel = null!;
    private Control _pauseMenuRoot = null!;
    private Button _nativeBtn = null!;
    private Button _balancedBtn = null!;
    private Button _probeBtn = null!;
    private Button _smoothBtn = null!;
    private Button _perfToggleBtn = null!;
    private Button _perfResetBtn = null!;
    private Control _perfPresetContent = null!;
    private Button _mapGenBtn = null!;
    private Button _mapEditorBtn = null!;
    private MapWorkbenchPanel _mapWorkbench = null!;
    private Label _mapLandWaterLabel = null!;
    private HSlider _landPctSlider = null!;
    private SpinBox _mapSeedSpinBox = null!;
    private Button _randomSeedBtn = null!;
    private Label _startAreaPatchLabel = null!;
    private HSlider _startAreaPatchSlider = null!;
    private Button _applyMapLandBtn = null!;
    private Label _hoverTileReadoutLabel = null!;
    private double _hoverReadoutAccum;
    private const double HoverReadoutIntervalS = 0.25;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;

        _bootLabel = GetNode<Label>("BootLabel");
        _perfLabel = GetNode<Label>("RightHudColumn/PerfLabel");
        _fpsLabel = GetNode<Label>("RightHudColumn/FpsLabel");
        _floorLabel = GetNode<Label>("RightHudColumn/FloorLabel");
        _zoomLabel = GetNode<Label>("RightHudColumn/ZoomLabel");
        _revisionLabel = GetNode<Label>("RightHudColumn/RevisionLabel");
        _playerFootReadoutLabel =
            GetNode<Label>("RightHudColumn/PlayerFootPanel/Margin/PlayerFootReadoutLabel");
        _hoverTileReadoutLabel =
            GetNode<Label>("RightHudColumn/HoverTilePanel/Margin/HoverTileReadoutLabel");
        _pauseMenuRoot = GetNode<Control>("PauseMenuRoot");
        _gridRoot = GetTree().CurrentScene?.GetNodeOrNull<GameRoot>("GridMap");

        var quitBtn = GetNode<Button>("PauseMenuRoot/Center/MenuPanel/Margin/VBox/PauseQuitButton");
        var resumeBtn = GetNode<Button>("PauseMenuRoot/Center/MenuPanel/Margin/VBox/PauseResumeButton");
        _balancedBtn = GetNode<Button>("RightPresetStack/PresetPanelRoot/Margin/VBox/PerfPresetPanel/Margin/VBox/PerfPresetContent/PerfPresetBalancedBtn");
        _probeBtn = GetNode<Button>("RightPresetStack/PresetPanelRoot/Margin/VBox/PerfPresetPanel/Margin/VBox/PerfPresetContent/PerfPresetProbeBtn");
        _smoothBtn = GetNode<Button>("RightPresetStack/PresetPanelRoot/Margin/VBox/PerfPresetPanel/Margin/VBox/PerfPresetContent/PerfPresetSmoothBtn");
        _perfToggleBtn = GetNode<Button>("RightPresetStack/PresetPanelRoot/Margin/VBox/PerfPresetPanel/Margin/VBox/PerfHeaderRow/PerfToggleBtn");
        _perfResetBtn = GetNode<Button>("RightPresetStack/PresetPanelRoot/Margin/VBox/PerfPresetPanel/Margin/VBox/PerfHeaderRow/PerfResetBtn");
        _perfPresetContent = GetNode<Control>("RightPresetStack/PresetPanelRoot/Margin/VBox/PerfPresetPanel/Margin/VBox/PerfPresetContent");
        _nativeBtn = GetNode<Button>("RightPresetStack/PresetPanelRoot/Margin/VBox/PerfPresetPanel/Margin/VBox/PerfPresetContent/PerfPresetNativeBtn");
        _nativeBtn.Text = NativeLabel;
        _balancedBtn.Text = BalancedLabel;
        _probeBtn.Text = ProbeLabel;
        _smoothBtn.Text = SmoothLabel;
        quitBtn.Pressed += OnPauseQuitPressed;
        resumeBtn.Pressed += ClosePauseMenu;
        _mapGenBtn = GetNode<Button>("PauseMenuRoot/Center/MenuPanel/Margin/VBox/PauseMapGeneratorButton");
        _mapEditorBtn = GetNode<Button>("PauseMenuRoot/Center/MenuPanel/Margin/VBox/PauseMapEditorButton");
        _mapGenBtn.Pressed += OnMapGeneratorPressed;
        _mapEditorBtn.Pressed += OnMapEditorPressed;

        _mapLandWaterLabel = GetNode<Label>("RightPresetStack/PresetPanelRoot/Margin/VBox/MapPresetPanel/Margin/VBox/MapLandWaterLabel");
        _landPctSlider = GetNode<HSlider>("RightPresetStack/PresetPanelRoot/Margin/VBox/MapPresetPanel/Margin/VBox/LandPercentSlider");
        _startAreaPatchLabel = GetNode<Label>("RightPresetStack/PresetPanelRoot/Margin/VBox/MapPresetPanel/Margin/VBox/StartAreaPatchLabel");
        _startAreaPatchSlider = GetNode<HSlider>("RightPresetStack/PresetPanelRoot/Margin/VBox/MapPresetPanel/Margin/VBox/StartAreaPatchSlider");
        _mapSeedSpinBox = GetNode<SpinBox>("RightPresetStack/PresetPanelRoot/Margin/VBox/MapPresetPanel/Margin/VBox/MapSeedRow/MapSeedSpinBox");
        _randomSeedBtn = GetNode<Button>("RightPresetStack/PresetPanelRoot/Margin/VBox/MapPresetPanel/Margin/VBox/MapSeedRow/RandomSeedBtn");
        _applyMapLandBtn = GetNode<Button>("RightPresetStack/PresetPanelRoot/Margin/VBox/MapPresetPanel/Margin/VBox/ApplyMapLandBtn");
        _landPctSlider.ValueChanged += OnLandPercentSliderChanged;
        _startAreaPatchSlider.ValueChanged += OnStartAreaPatchSliderChanged;
        _applyMapLandBtn.Pressed += OnApplyMapLandPressed;
        _randomSeedBtn.Pressed += OnRandomSeedPressed;
        PopulateTerrainLegend();

        _mapWorkbench = new MapWorkbenchPanel();
        AddChild(_mapWorkbench);
        if (_gridRoot is not null)
            _mapWorkbench.Configure(_gridRoot);
        _nativeBtn.Pressed += () =>
        {
            _gridRoot?.ApplyPerfPreset(1.0f, 0, -1, "native");
            SetSelectedPresetButton(_nativeBtn);
        };
        _balancedBtn.Pressed += () =>
        {
            _gridRoot?.ApplyPerfPreset(0.85f, 120, 0, "balanced");
            SetSelectedPresetButton(_balancedBtn);
        };
        _probeBtn.Pressed += () =>
        {
            _gridRoot?.ApplyPerfPreset(0.70f, 0, 0, "probe");
            SetSelectedPresetButton(_probeBtn);
        };
        _smoothBtn.Pressed += () =>
        {
            _gridRoot?.ApplyPerfPreset(1.0f, 60, 1, "smooth");
            SetSelectedPresetButton(_smoothBtn);
        };
        _perfToggleBtn.Pressed += TogglePerfPanel;
        _perfResetBtn.Pressed += ResetPerfPanelDefaults;

        _pauseMenuRoot.Visible = false;
        _pauseMenuRoot.MouseFilter = MouseFilterEnum.Ignore;
        // Keep header visible; collapse body so the map reads like a game (perf via ▶ toggle).
        _perfPresetContent.Visible = false;
        RefreshPanelToggleGlyphs();
        SetSelectedPresetButton(_nativeBtn);

        Callable.From(EnsureShellHudFillsViewport).CallDeferred();
        Callable.From(DeferredSyncMapPresetFromGameRoot).CallDeferred();
        GetViewport().SizeChanged += OnViewportSizeChanged;
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        _hoverReadoutAccum += delta;
        if (_hoverReadoutAccum < HoverReadoutIntervalS)
        {
            return;
        }

        _hoverReadoutAccum = 0;
        _gridRoot ??= GetTree().CurrentScene?.GetNodeOrNull<GameRoot>("GridMap");
        if (_gridRoot is null)
        {
            return;
        }

        _gridRoot.TryGetPlayerFootReadout(out var footLine);
        _playerFootReadoutLabel.Text = footLine;

        _gridRoot.TryGetHoverTileReadout(out var hoverLine);
        _hoverTileReadoutLabel.Text = hoverLine;
    }

    /// <summary>Keep land %%, seed, and origin-patch controls aligned with <see cref="GameRoot"/> after bootstrap / map replace.</summary>
    public void SyncMapPresetUi(int landPercent, int seed, int originPatchChebyshevRadius, bool canEdit)
    {
        var lp = Mathf.Clamp(landPercent, 0, 100);
        var originR = Mathf.Clamp(originPatchChebyshevRadius, 0, ShellAppConfig.MaxStartupOriginPatchChebyshevRadius);
        _landPctSlider.Value = lp;
        _mapSeedSpinBox.Value = seed;
        _startAreaPatchSlider.Value = originR;
        UpdateMapLandWaterLabel((int)lp);
        UpdateStartAreaPatchLabel(originR);

        _applyMapLandBtn.Disabled = !canEdit;
        _randomSeedBtn.Disabled = !canEdit;
        _mapSeedSpinBox.Editable = canEdit;
        _startAreaPatchSlider.MouseFilter = canEdit ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
        _landPctSlider.MouseFilter = canEdit ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
        _mapLandWaterLabel.Modulate = canEdit ? Colors.White : new Color(0.65f, 0.65f, 0.68f);
        _startAreaPatchLabel.Modulate = canEdit ? Colors.White : new Color(0.65f, 0.65f, 0.68f);
    }

    private void DeferredSyncMapPresetFromGameRoot()
    {
        _gridRoot ??= GetTree().CurrentScene?.GetNodeOrNull<GameRoot>("GridMap");
        if (_gridRoot is null)
        {
            return;
        }

        SyncMapPresetUi(_gridRoot.ShellEffectiveLandPercent, _gridRoot.ShellEffectiveSeed,
            _gridRoot.ShellEffectiveOriginPatchChebyshevRadius, _gridRoot.ShellCanApplyLandPercentPreset);
    }

    private void PopulateTerrainLegend()
    {
        var rows = GetNode<VBoxContainer>("RightPresetStack/TerrainLegendPanel/Margin/VBox/LegendRows");
        foreach (var child in rows.GetChildren())
        {
            child.QueueFree();
        }

        foreach (var (label, rgb) in TerrainVisualColor.LegendSwatches)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            var swatch = new ColorRect
            {
                CustomMinimumSize = new Vector2(16, 16),
                Color = new Color(rgb.R, rgb.G, rgb.B),
            };
            var lbl = new Label
            {
                Text = label,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            lbl.AddThemeFontSizeOverride("font_size", 13);
            lbl.AddThemeColorOverride("font_color", new Color(0.78f, 0.82f, 0.88f));
            row.AddChild(swatch);
            row.AddChild(lbl);
            rows.AddChild(row);
        }
    }

    private void OnLandPercentSliderChanged(double value)
    {
        UpdateMapLandWaterLabel(Mathf.RoundToInt((float)value));
    }

    private void OnStartAreaPatchSliderChanged(double value)
    {
        UpdateStartAreaPatchLabel(Mathf.RoundToInt((float)value));
    }

    private void UpdateStartAreaPatchLabel(int radiusCells)
    {
        radiusCells = Mathf.Clamp(radiusCells, 0, ShellAppConfig.MaxStartupOriginPatchChebyshevRadius);
        _startAreaPatchLabel.Text =
            $"Flat land patch radius {radiusCells} (map center + global 0,0)";
    }

    private void UpdateMapLandWaterLabel(int landPct)
    {
        landPct = Mathf.Clamp(landPct, 0, 100);
        _mapLandWaterLabel.Text = $"Land {landPct}% — Water {100 - landPct}%";
    }

    private void OnApplyMapLandPressed()
    {
        _gridRoot ??= GetTree().CurrentScene?.GetNodeOrNull<GameRoot>("GridMap");
        if (_gridRoot is null)
        {
            return;
        }

        _gridRoot.ApplyProceduralPresetFromHud(Mathf.RoundToInt((float)_landPctSlider.Value),
            (int)_mapSeedSpinBox.Value,
            Mathf.RoundToInt((float)_startAreaPatchSlider.Value));
    }

    private void OnRandomSeedPressed()
    {
        _gridRoot ??= GetTree().CurrentScene?.GetNodeOrNull<GameRoot>("GridMap");
        if (_gridRoot is null || !_gridRoot.ShellCanApplyLandPercentPreset)
        {
            return;
        }

        var next = (int)(GD.Randi() % 2147483647);
        _mapSeedSpinBox.Value = next;
        _gridRoot.ApplyProceduralPresetFromHud(Mathf.RoundToInt((float)_landPctSlider.Value), next,
            Mathf.RoundToInt((float)_startAreaPatchSlider.Value));
    }

    /// <summary>CanvasLayer children need explicit full-viewport rect so anchored children get real width (RichText layout).</summary>
    private void EnsureShellHudFillsViewport()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        OffsetLeft = 0f;
        OffsetTop = 0f;
        OffsetRight = 0f;
        OffsetBottom = 0f;
        Position = Vector2.Zero;
        Scale = Vector2.One;
        Size = GetViewport().GetVisibleRect().Size;
    }

    private void OnViewportSizeChanged() => Callable.From(EnsureShellHudFillsViewport).CallDeferred();

    private void SetSelectedPresetButton(Button selected)
    {
        _nativeBtn.SelfModulate = PresetBtnNormalColor;
        _balancedBtn.SelfModulate = PresetBtnNormalColor;
        _probeBtn.SelfModulate = PresetBtnNormalColor;
        _smoothBtn.SelfModulate = PresetBtnNormalColor;
        selected.SelfModulate = PresetBtnSelectedColor;
    }

    private void TogglePerfPanel()
    {
        _perfPresetContent.Visible = !_perfPresetContent.Visible;
        RefreshPanelToggleGlyphs();
    }

    private void RefreshPanelToggleGlyphs()
    {
        _perfToggleBtn.Text = _perfPresetContent.Visible ? "▼" : "▶";
    }

    private void ResetPerfPanelDefaults()
    {
        _gridRoot?.ApplyPerfPreset(ResetPerfRenderScale, ResetPerfMaxFps, ResetPerfVsyncMode, "reset");
        SetSelectedPresetButton(_nativeBtn);
    }

    public void SetBootText(string text) => _bootLabel.Text = text;

    public void SetPerfReadout(string text) => _perfLabel.Text = text;

    public void SetFpsReadout(string text) => _fpsLabel.Text = text;

    public void SetFloorReadout(string text) => _floorLabel.Text = text;

    public void SetZoomReadout(string text) => _zoomLabel.Text = text;

    public void SetRevisionReadout(string text) => _revisionLabel.Text = text;

    /// <summary>When true, board-level game input (e.g. discrete sub-steps) should be ignored.</summary>
    public bool IsModalHudOpen => _pauseMenuRoot.Visible || _mapWorkbench.Visible;

    /// <summary>Returns true if ESC closed the map workbench (do not toggle pause).</summary>
    public bool TryConsumeEscForMapWorkbench()
    {
        if (!_mapWorkbench.Visible)
        {
            return false;
        }

        _mapWorkbench.CloseWorkbench();
        return true;
    }

    /// <summary>Called from <see cref="GameRoot._UnhandledInput"/> so ESC is ordered with other shell keys.</summary>
    public void TogglePauseMenuFromEsc()
    {
        if (_pauseMenuRoot.Visible)
        {
            ClosePauseMenu();
        }
        else
        {
            OpenPauseMenu();
        }
    }

    private void OpenPauseMenu()
    {
        _pauseMenuRoot.Visible = true;
        _pauseMenuRoot.MouseFilter = MouseFilterEnum.Stop;
        RefreshMapMenuButtons();
        var resume = GetNode<Button>("PauseMenuRoot/Center/MenuPanel/Margin/VBox/PauseResumeButton");
        resume.GrabFocus();
    }

    private void RefreshMapMenuButtons()
    {
        _gridRoot ??= GetTree().CurrentScene?.GetNodeOrNull<GameRoot>("GridMap");
        var canEdit = _gridRoot?.ShellCanOpenMapEditor ?? false;
        _mapEditorBtn.Disabled = !canEdit;
    }

    private void OnMapGeneratorPressed()
    {
        _gridRoot ??= GetTree().CurrentScene?.GetNodeOrNull<GameRoot>("GridMap");
        if (_gridRoot is null)
        {
            return;
        }

        _mapWorkbench.Configure(_gridRoot);
        ClosePauseMenu();
        _mapWorkbench.Open(MapWorkbenchMode.GenerateNewGame);
    }

    private void OnMapEditorPressed()
    {
        _gridRoot ??= GetTree().CurrentScene?.GetNodeOrNull<GameRoot>("GridMap");
        if (_gridRoot is null || !_gridRoot.ShellCanOpenMapEditor)
        {
            return;
        }

        _mapWorkbench.Configure(_gridRoot);
        ClosePauseMenu();
        _mapWorkbench.Open(MapWorkbenchMode.EditCurrentMap);
    }

    private void ClosePauseMenu()
    {
        _pauseMenuRoot.Visible = false;
        _pauseMenuRoot.MouseFilter = MouseFilterEnum.Ignore;
    }

    private void OnPauseQuitPressed() => GetTree().Quit();
}
