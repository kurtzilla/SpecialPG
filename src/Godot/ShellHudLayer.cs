#nullable enable
using Godot;
using SpecialPG;

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
    private const string FogCinematicLabel = "Cinematic";
    private const string FogBalancedLabel = "Balanced";
    private const string FogPerformanceLabel = "Performance";
    private const float ResetPerfRenderScale = 1.0f;
    private const int ResetPerfMaxFps = 0;
    private const int ResetPerfVsyncMode = -1;
    private const bool ResetFogEdgeEnabled = true;
    private const float ResetFogEdgeOpacity = 0.90f;
    private const float ResetFogEdgeWidth = 1.60f;
    private const float ResetFogEdgeSoftness = 1.10f;
    private const int ResetFogEdgeSamples = 2;
    private const int ResetFogVisualUpdateHz = 20;
    private const float ResetFogRevealLerpSpeed = 6.0f;
    private const float ResetFogBrushHardCoreRatio = 0.72f;
    private const float ResetFogBrushFeatherExponent = 1.40f;

    private GameRoot? _gridRoot;
    private Label _bootLabel = null!;
    private Label _perfLabel = null!;
    private Label _fpsLabel = null!;
    private Label _floorLabel = null!;
    private Label _zoomLabel = null!;
    private Label _revisionLabel = null!;
    private Label _playerPosLabel = null!;
    private Control _pauseMenuRoot = null!;
    private Button _nativeBtn = null!;
    private Button _balancedBtn = null!;
    private Button _probeBtn = null!;
    private Button _smoothBtn = null!;
    private Button _fogCinematicBtn = null!;
    private Button _fogBalancedBtn = null!;
    private Button _fogPerformanceBtn = null!;
    private Button _perfToggleBtn = null!;
    private Button _fogToggleBtn = null!;
    private Button _perfResetBtn = null!;
    private Button _fogResetBtn = null!;
    private Control _perfPresetContent = null!;
    private Control _fogPresetContent = null!;
    private Label _fogSamplesLabel = null!;
    private HSlider _fogSamplesSlider = null!;
    private Label _fogSoftnessLabel = null!;
    private HSlider _fogSoftnessSlider = null!;
    private Label _fogEdgeWidthLabel = null!;
    private HSlider _fogEdgeWidthSlider = null!;
    private Label _fogVisualHzLabel = null!;
    private HSlider _fogVisualHzSlider = null!;
    private Label _fogRevealLerpLabel = null!;
    private HSlider _fogRevealLerpSlider = null!;
    private Label _fogBrushCoreLabel = null!;
    private HSlider _fogBrushCoreSlider = null!;
    private Label _fogBrushFeatherLabel = null!;
    private HSlider _fogBrushFeatherSlider = null!;
    private Button _mapGenBtn = null!;
    private Button _mapEditorBtn = null!;
    private MapWorkbenchPanel _mapWorkbench = null!;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;

        _bootLabel = GetNode<Label>("BootLabel");
        _perfLabel = GetNode<Label>("RightHudColumn/PerfLabel");
        _fpsLabel = GetNode<Label>("RightHudColumn/FpsLabel");
        _floorLabel = GetNode<Label>("RightHudColumn/FloorLabel");
        _zoomLabel = GetNode<Label>("RightHudColumn/ZoomLabel");
        _revisionLabel = GetNode<Label>("RightHudColumn/RevisionLabel");
        _playerPosLabel = GetNode<Label>("RightHudColumn/PlayerPositionLabel");
        _pauseMenuRoot = GetNode<Control>("PauseMenuRoot");
        _gridRoot = GetTree().CurrentScene?.GetNodeOrNull<GameRoot>("GridMap");

        var quitBtn = GetNode<Button>("PauseMenuRoot/Center/MenuPanel/Margin/VBox/PauseQuitButton");
        var resumeBtn = GetNode<Button>("PauseMenuRoot/Center/MenuPanel/Margin/VBox/PauseResumeButton");
        _balancedBtn = GetNode<Button>("PresetPanelRoot/Margin/VBox/PerfPresetPanel/Margin/VBox/PerfPresetContent/PerfPresetBalancedBtn");
        _probeBtn = GetNode<Button>("PresetPanelRoot/Margin/VBox/PerfPresetPanel/Margin/VBox/PerfPresetContent/PerfPresetProbeBtn");
        _smoothBtn = GetNode<Button>("PresetPanelRoot/Margin/VBox/PerfPresetPanel/Margin/VBox/PerfPresetContent/PerfPresetSmoothBtn");
        _perfToggleBtn = GetNode<Button>("PresetPanelRoot/Margin/VBox/PerfPresetPanel/Margin/VBox/PerfHeaderRow/PerfToggleBtn");
        _perfResetBtn = GetNode<Button>("PresetPanelRoot/Margin/VBox/PerfPresetPanel/Margin/VBox/PerfHeaderRow/PerfResetBtn");
        _perfPresetContent = GetNode<Control>("PresetPanelRoot/Margin/VBox/PerfPresetPanel/Margin/VBox/PerfPresetContent");
        _fogCinematicBtn = GetNode<Button>("PresetPanelRoot/Margin/VBox/FogPresetPanel/Margin/VBox/FogPresetContent/FogPresetCinematicBtn");
        _fogBalancedBtn = GetNode<Button>("PresetPanelRoot/Margin/VBox/FogPresetPanel/Margin/VBox/FogPresetContent/FogPresetBalancedBtn");
        _fogPerformanceBtn = GetNode<Button>("PresetPanelRoot/Margin/VBox/FogPresetPanel/Margin/VBox/FogPresetContent/FogPresetPerfBtn");
        _fogToggleBtn = GetNode<Button>("PresetPanelRoot/Margin/VBox/FogPresetPanel/Margin/VBox/FogHeaderRow/FogToggleBtn");
        _fogResetBtn = GetNode<Button>("PresetPanelRoot/Margin/VBox/FogPresetPanel/Margin/VBox/FogHeaderRow/FogResetBtn");
        _fogPresetContent = GetNode<Control>("PresetPanelRoot/Margin/VBox/FogPresetPanel/Margin/VBox/FogPresetContent");
        _fogSamplesLabel = GetNode<Label>("PresetPanelRoot/Margin/VBox/FogPresetPanel/Margin/VBox/FogPresetContent/FogSamplesLabel");
        _fogSamplesSlider = GetNode<HSlider>("PresetPanelRoot/Margin/VBox/FogPresetPanel/Margin/VBox/FogPresetContent/FogSamplesSlider");
        _fogSoftnessLabel = GetNode<Label>("PresetPanelRoot/Margin/VBox/FogPresetPanel/Margin/VBox/FogPresetContent/FogSoftnessLabel");
        _fogSoftnessSlider = GetNode<HSlider>("PresetPanelRoot/Margin/VBox/FogPresetPanel/Margin/VBox/FogPresetContent/FogSoftnessSlider");
        _fogEdgeWidthLabel = GetNode<Label>("PresetPanelRoot/Margin/VBox/FogPresetPanel/Margin/VBox/FogPresetContent/FogEdgeWidthLabel");
        _fogEdgeWidthSlider = GetNode<HSlider>("PresetPanelRoot/Margin/VBox/FogPresetPanel/Margin/VBox/FogPresetContent/FogEdgeWidthSlider");
        _fogVisualHzLabel = GetNode<Label>("PresetPanelRoot/Margin/VBox/FogPresetPanel/Margin/VBox/FogPresetContent/FogVisualHzLabel");
        _fogVisualHzSlider = GetNode<HSlider>("PresetPanelRoot/Margin/VBox/FogPresetPanel/Margin/VBox/FogPresetContent/FogVisualHzSlider");
        _fogRevealLerpLabel = GetNode<Label>("PresetPanelRoot/Margin/VBox/FogPresetPanel/Margin/VBox/FogPresetContent/FogRevealLerpLabel");
        _fogRevealLerpSlider = GetNode<HSlider>("PresetPanelRoot/Margin/VBox/FogPresetPanel/Margin/VBox/FogPresetContent/FogRevealLerpSlider");
        _fogBrushCoreLabel = GetNode<Label>("PresetPanelRoot/Margin/VBox/FogPresetPanel/Margin/VBox/FogPresetContent/FogBrushCoreLabel");
        _fogBrushCoreSlider = GetNode<HSlider>("PresetPanelRoot/Margin/VBox/FogPresetPanel/Margin/VBox/FogPresetContent/FogBrushCoreSlider");
        _fogBrushFeatherLabel = GetNode<Label>("PresetPanelRoot/Margin/VBox/FogPresetPanel/Margin/VBox/FogPresetContent/FogBrushFeatherLabel");
        _fogBrushFeatherSlider = GetNode<HSlider>("PresetPanelRoot/Margin/VBox/FogPresetPanel/Margin/VBox/FogPresetContent/FogBrushFeatherSlider");
        _nativeBtn = GetNode<Button>("PresetPanelRoot/Margin/VBox/PerfPresetPanel/Margin/VBox/PerfPresetContent/PerfPresetNativeBtn");
        _nativeBtn.Text = NativeLabel;
        _balancedBtn.Text = BalancedLabel;
        _probeBtn.Text = ProbeLabel;
        _smoothBtn.Text = SmoothLabel;
        _fogCinematicBtn.Text = FogCinematicLabel;
        _fogBalancedBtn.Text = FogBalancedLabel;
        _fogPerformanceBtn.Text = FogPerformanceLabel;
        quitBtn.Pressed += OnPauseQuitPressed;
        resumeBtn.Pressed += ClosePauseMenu;
        _mapGenBtn = GetNode<Button>("PauseMenuRoot/Center/MenuPanel/Margin/VBox/PauseMapGeneratorButton");
        _mapEditorBtn = GetNode<Button>("PauseMenuRoot/Center/MenuPanel/Margin/VBox/PauseMapEditorButton");
        _mapGenBtn.Pressed += OnMapGeneratorPressed;
        _mapEditorBtn.Pressed += OnMapEditorPressed;

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
        _fogCinematicBtn.Pressed += () =>
        {
            _gridRoot?.ApplyFogPreset(true, 0.88f, 1.90f, 1.00f, 4, "cinematic");
            SetSelectedFogPresetButton(_fogCinematicBtn);
        };
        _fogBalancedBtn.Pressed += () =>
        {
            _gridRoot?.ApplyFogPreset(true, 0.90f, 1.60f, 1.10f, 2, "balanced");
            SetSelectedFogPresetButton(_fogBalancedBtn);
        };
        _fogPerformanceBtn.Pressed += () =>
        {
            _gridRoot?.ApplyFogPreset(true, 0.90f, 1.35f, 1.20f, 2, "performance");
            SetSelectedFogPresetButton(_fogPerformanceBtn);
            UpdateFogSamplesUi((int)Mathf.Round(_fogSamplesSlider.Value));
            UpdateFogSoftnessUi((float)_fogSoftnessSlider.Value);
        };
        _fogSamplesSlider.ValueChanged += OnFogSamplesSliderChanged;
        _fogSoftnessSlider.ValueChanged += OnFogSoftnessSliderChanged;
        _fogEdgeWidthSlider.ValueChanged += OnFogEdgeWidthSliderChanged;
        _fogVisualHzSlider.ValueChanged += OnFogVisualHzSliderChanged;
        _fogRevealLerpSlider.ValueChanged += OnFogRevealLerpSliderChanged;
        _fogBrushCoreSlider.ValueChanged += OnFogBrushCoreSliderChanged;
        _fogBrushFeatherSlider.ValueChanged += OnFogBrushFeatherSliderChanged;
        _perfToggleBtn.Pressed += TogglePerfPanel;
        _fogToggleBtn.Pressed += ToggleFogPanel;
        _perfResetBtn.Pressed += ResetPerfPanelDefaults;
        _fogResetBtn.Pressed += ResetFogPanelDefaults;

        _pauseMenuRoot.Visible = false;
        _pauseMenuRoot.MouseFilter = MouseFilterEnum.Ignore;
        _perfPresetContent.Visible = true;
        _fogPresetContent.Visible = true;
        RefreshPanelToggleGlyphs();
        SetSelectedPresetButton(_nativeBtn);
        SetSelectedFogPresetButton(_fogBalancedBtn);
        var startingSamples = _gridRoot?.ShellFogEdgeSamples ?? (int)Mathf.Round(_fogSamplesSlider.Value);
        _fogSamplesSlider.SetValueNoSignal(startingSamples);
        UpdateFogSamplesUi(startingSamples);
        var startingSoftness = _gridRoot?.ShellFogEdgeSoftness ?? (float)_fogSoftnessSlider.Value;
        _fogSoftnessSlider.SetValueNoSignal(startingSoftness);
        UpdateFogSoftnessUi(startingSoftness);
        var startingEdgeWidth = _gridRoot?.ShellFogEdgeWidthCells ?? (float)_fogEdgeWidthSlider.Value;
        _fogEdgeWidthSlider.SetValueNoSignal(startingEdgeWidth);
        UpdateFogEdgeWidthUi(startingEdgeWidth);
        var startingVisualHz = _gridRoot?.ShellFogVisualUpdateHz ?? (int)Mathf.Round(_fogVisualHzSlider.Value);
        _fogVisualHzSlider.SetValueNoSignal(startingVisualHz);
        UpdateFogVisualHzUi(startingVisualHz);
        var startingRevealLerp = _gridRoot?.ShellFogRevealLerpSpeed ?? (float)_fogRevealLerpSlider.Value;
        _fogRevealLerpSlider.SetValueNoSignal(startingRevealLerp);
        UpdateFogRevealLerpUi(startingRevealLerp);
        var startingBrushCore = _gridRoot?.ShellFogBrushHardCoreRatio ?? (float)_fogBrushCoreSlider.Value;
        _fogBrushCoreSlider.SetValueNoSignal(startingBrushCore);
        UpdateFogBrushCoreUi(startingBrushCore);
        var startingBrushFeather = _gridRoot?.ShellFogBrushFeatherExponent ?? (float)_fogBrushFeatherSlider.Value;
        _fogBrushFeatherSlider.SetValueNoSignal(startingBrushFeather);
        UpdateFogBrushFeatherUi(startingBrushFeather);

        Callable.From(EnsureShellHudFillsViewport).CallDeferred();
        GetViewport().SizeChanged += OnViewportSizeChanged;
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

    private void SetSelectedFogPresetButton(Button selected)
    {
        _fogCinematicBtn.SelfModulate = PresetBtnNormalColor;
        _fogBalancedBtn.SelfModulate = PresetBtnNormalColor;
        _fogPerformanceBtn.SelfModulate = PresetBtnNormalColor;
        selected.SelfModulate = PresetBtnSelectedColor;
    }

    private void OnFogSamplesSliderChanged(double value)
    {
        var samples = Mathf.Clamp((int)Mathf.Round(value), 2, 16);
        _fogSamplesSlider.SetValueNoSignal(samples);
        _gridRoot?.SetFogEdgeSamples(samples, "slider");
        UpdateFogSamplesUi(samples);
    }

    private void UpdateFogSamplesUi(int samples)
    {
        _fogSamplesLabel.Text = $"Edge samples: {samples}";
    }

    private void OnFogSoftnessSliderChanged(double value)
    {
        var softness = Mathf.Clamp((float)value, 0.5f, 4.0f);
        _fogSoftnessSlider.SetValueNoSignal(softness);
        _gridRoot?.SetFogEdgeSoftness(softness, "slider");
        UpdateFogSoftnessUi(softness);
    }

    private void UpdateFogSoftnessUi(float softness)
    {
        _fogSoftnessLabel.Text = $"Edge softness: {softness:F2}";
    }

    private void OnFogEdgeWidthSliderChanged(double value)
    {
        var width = Mathf.Clamp((float)value, 0.25f, 2.5f);
        _fogEdgeWidthSlider.SetValueNoSignal(width);
        _gridRoot?.SetFogEdgeWidthCells(width, "slider");
        UpdateFogEdgeWidthUi(width);
    }

    private void UpdateFogEdgeWidthUi(float width)
    {
        _fogEdgeWidthLabel.Text = $"Edge radius: {width:F2}";
    }

    private void OnFogVisualHzSliderChanged(double value)
    {
        var hz = Mathf.Clamp((int)Mathf.Round(value), 10, 240);
        _fogVisualHzSlider.SetValueNoSignal(hz);
        _gridRoot?.SetFogVisualUpdateHz(hz, "slider");
        UpdateFogVisualHzUi(hz);
    }

    private void UpdateFogVisualHzUi(int hz)
    {
        _fogVisualHzLabel.Text = $"Visual update: {hz} Hz";
    }

    private void OnFogRevealLerpSliderChanged(double value)
    {
        var speed = Mathf.Clamp((float)value, 0.5f, 40.0f);
        _fogRevealLerpSlider.SetValueNoSignal(speed);
        _gridRoot?.SetFogRevealLerpSpeed(speed, "slider");
        UpdateFogRevealLerpUi(speed);
    }

    private void UpdateFogRevealLerpUi(float speed)
    {
        _fogRevealLerpLabel.Text = $"Reveal lerp: {speed:F2}";
    }

    private void OnFogBrushCoreSliderChanged(double value)
    {
        var ratio = Mathf.Clamp((float)value, 0.2f, 0.95f);
        _fogBrushCoreSlider.SetValueNoSignal(ratio);
        _gridRoot?.SetFogBrushHardCoreRatio(ratio, "slider");
        UpdateFogBrushCoreUi(ratio);
    }

    private void UpdateFogBrushCoreUi(float ratio)
    {
        _fogBrushCoreLabel.Text = $"Brush core: {ratio:F2}";
    }

    private void OnFogBrushFeatherSliderChanged(double value)
    {
        var exponent = Mathf.Clamp((float)value, 0.5f, 4.0f);
        _fogBrushFeatherSlider.SetValueNoSignal(exponent);
        _gridRoot?.SetFogBrushFeatherExponent(exponent, "slider");
        UpdateFogBrushFeatherUi(exponent);
    }

    private void UpdateFogBrushFeatherUi(float exponent)
    {
        _fogBrushFeatherLabel.Text = $"Brush feather: {exponent:F2}";
    }

    private void TogglePerfPanel()
    {
        _perfPresetContent.Visible = !_perfPresetContent.Visible;
        RefreshPanelToggleGlyphs();
    }

    private void ToggleFogPanel()
    {
        _fogPresetContent.Visible = !_fogPresetContent.Visible;
        RefreshPanelToggleGlyphs();
    }

    private void RefreshPanelToggleGlyphs()
    {
        _perfToggleBtn.Text = _perfPresetContent.Visible ? "▼" : "▶";
        _fogToggleBtn.Text = _fogPresetContent.Visible ? "▼" : "▶";
    }

    private void ResetPerfPanelDefaults()
    {
        _gridRoot?.ApplyPerfPreset(ResetPerfRenderScale, ResetPerfMaxFps, ResetPerfVsyncMode, "reset");
        SetSelectedPresetButton(_nativeBtn);
    }

    private void ResetFogPanelDefaults()
    {
        _gridRoot?.ApplyFogPreset(ResetFogEdgeEnabled, ResetFogEdgeOpacity, ResetFogEdgeWidth, ResetFogEdgeSoftness,
            ResetFogEdgeSamples, "reset");
        _fogSamplesSlider.SetValueNoSignal(ResetFogEdgeSamples);
        _fogSoftnessSlider.SetValueNoSignal(ResetFogEdgeSoftness);
        _fogEdgeWidthSlider.SetValueNoSignal(ResetFogEdgeWidth);
        _fogVisualHzSlider.SetValueNoSignal(ResetFogVisualUpdateHz);
        _fogRevealLerpSlider.SetValueNoSignal(ResetFogRevealLerpSpeed);
        _fogBrushCoreSlider.SetValueNoSignal(ResetFogBrushHardCoreRatio);
        _fogBrushFeatherSlider.SetValueNoSignal(ResetFogBrushFeatherExponent);
        _gridRoot?.SetFogSmoothingSettings(ResetFogVisualUpdateHz, ResetFogRevealLerpSpeed, ResetFogBrushHardCoreRatio,
            ResetFogBrushFeatherExponent, "reset");
        UpdateFogSamplesUi(ResetFogEdgeSamples);
        UpdateFogSoftnessUi(ResetFogEdgeSoftness);
        UpdateFogEdgeWidthUi(ResetFogEdgeWidth);
        UpdateFogVisualHzUi(ResetFogVisualUpdateHz);
        UpdateFogRevealLerpUi(ResetFogRevealLerpSpeed);
        UpdateFogBrushCoreUi(ResetFogBrushHardCoreRatio);
        UpdateFogBrushFeatherUi(ResetFogBrushFeatherExponent);
    }

    public void SetBootText(string text) => _bootLabel.Text = text;

    public void SetPerfReadout(string text) => _perfLabel.Text = text;

    public void SetFpsReadout(string text) => _fpsLabel.Text = text;

    public void SetFloorReadout(string text) => _floorLabel.Text = text;

    public void SetZoomReadout(string text) => _zoomLabel.Text = text;

    public void SetRevisionReadout(string text) => _revisionLabel.Text = text;

    public void SetPlayerPositionText(string text) => _playerPosLabel.Text = text;

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
