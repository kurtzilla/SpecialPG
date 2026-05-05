#nullable enable
using Godot;
using SpecialPG;
using SpecialPG.Core.Maps;
using GodotMathf = Godot.Mathf;

/// <summary>
/// Modal map generator / editor shell (shared terrain controls). v1: procedural preview + full commit.
/// </summary>
public partial class MapWorkbenchPanel : Control
{
    private GameRoot? _gameRoot;
    private MapWorkbenchMode _mode;

    private PanelContainer? _modalPanel;

    private Label _titleLabel = null!;
    private HSlider _landSlider = null!;
    private Label _waterValueLabel = null!;
    private SpinBox _seedSpin = null!;
    private TextureRect _previewRect = null!;
    private Button _previewBtn = null!;
    private Button _applyBtn = null!;
    private Button _closeBtn = null!;
    private Label _hintLabel = null!;

    public void Configure(GameRoot gameRoot) => _gameRoot = gameRoot;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsPreset(LayoutPreset.FullRect);
        GrowHorizontal = GrowDirection.Both;
        GrowVertical = GrowDirection.Both;

        var dimmer = new ColorRect
        {
            Color = new Color(0.04f, 0.05f, 0.08f, 0.72f),
            MouseFilter = MouseFilterEnum.Stop,
        };
        dimmer.SetAnchorsPreset(LayoutPreset.FullRect);
        dimmer.GrowHorizontal = GrowDirection.Both;
        dimmer.GrowVertical = GrowDirection.Both;
        AddChild(dimmer);

        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        center.GrowHorizontal = GrowDirection.Both;
        center.GrowVertical = GrowDirection.Both;
        center.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(center);

        var panel = new PanelContainer();
        _modalPanel = panel;
        center.AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 16);
        margin.AddThemeConstantOverride("margin_top", 16);
        margin.AddThemeConstantOverride("margin_right", 16);
        margin.AddThemeConstantOverride("margin_bottom", 16);
        panel.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 10);
        margin.AddChild(vbox);

        _titleLabel = new Label();
        _titleLabel.AddThemeFontSizeOverride("font_size", 18);
        vbox.AddChild(_titleLabel);

        _hintLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Text = "Adjust land vs water, preview a small map, then apply to start play on a full-size world (config dimensions).",
        };
        vbox.AddChild(_hintLabel);

        var landRow = new HBoxContainer();
        landRow.AddThemeConstantOverride("separation", 12);
        vbox.AddChild(landRow);
        landRow.AddChild(new Label { Text = "Land %", CustomMinimumSize = new Vector2(72, 0) });
        _landSlider = new HSlider { MinValue = 0, MaxValue = 100, Step = 1, Value = 55, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        landRow.AddChild(_landSlider);
        _waterValueLabel = new Label { Text = "Water 45%", CustomMinimumSize = new Vector2(100, 0) };
        landRow.AddChild(_waterValueLabel);
        _landSlider.ValueChanged += OnLandSliderChanged;

        var seedRow = new HBoxContainer();
        seedRow.AddThemeConstantOverride("separation", 12);
        vbox.AddChild(seedRow);
        seedRow.AddChild(new Label { Text = "Seed", CustomMinimumSize = new Vector2(72, 0) });
        _seedSpin = new SpinBox
        {
            MinValue = int.MinValue,
            MaxValue = int.MaxValue,
            Step = 1,
            Value = 1,
            Rounded = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        seedRow.AddChild(_seedSpin);

        _previewRect = new TextureRect
        {
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        vbox.AddChild(_previewRect);

        var btnRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        btnRow.ClipContents = false;
        btnRow.AddThemeConstantOverride("separation", 8);
        vbox.AddChild(btnRow);
        _previewBtn = new Button { Text = "Preview" };
        _applyBtn = new Button { Text = "Apply to game" };
        _closeBtn = new Button { Text = "Close" };
        btnRow.AddChild(_previewBtn);
        btnRow.AddChild(_applyBtn);
        btnRow.AddChild(_closeBtn);

        _previewBtn.Pressed += OnPreviewPressed;
        _applyBtn.Pressed += OnApplyPressed;
        _closeBtn.Pressed += OnClosePressed;

        UpdateWaterLabel();
    }

    /// <summary>Match <see cref="ShellHudLayer.EnsureShellHudFillsViewport"/> so full-screen overlay gets real size.</summary>
    private void EnsureWorkbenchFillsViewport()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        OffsetLeft = 0f;
        OffsetTop = 0f;
        OffsetRight = 0f;
        OffsetBottom = 0f;
        Position = Vector2.Zero;
        Scale = Vector2.One;
        Size = GetViewport().GetVisibleRect().Size;
        ApplyModalSizing();
    }

    private void ApplyModalSizing()
    {
        var vr = GetViewport().GetVisibleRect();
        var vw = vr.Size.X;
        var vh = vr.Size.Y;
        var panelW = GodotMathf.Clamp(vw * 0.88f, 720f, 1120f);
        var panelH = GodotMathf.Clamp(vh * 0.82f, 620f, 900f);
        if (_modalPanel is not null)
            _modalPanel.CustomMinimumSize = new Vector2(panelW, panelH);

        var previewW = GodotMathf.Clamp(panelW - 96f, 480f, 1040f);
        var previewH = GodotMathf.Clamp(panelH * 0.44f, 340f, 560f);
        _previewRect.CustomMinimumSize = new Vector2(previewW, previewH);
    }

    /// <summary>Called from <see cref="ShellHudLayer.TryConsumeEscForMapWorkbench"/> when ESC must close before pause toggles.</summary>
    public void CloseWorkbench()
    {
        if (!Visible)
        {
            return;
        }

        Visible = false;
        GetTree().Paused = false;
    }

    public void Open(MapWorkbenchMode mode)
    {
        _mode = mode;
        _gameRoot ??= GetTree()?.CurrentScene?.GetNodeOrNull<GameRoot>("GridMap");
        Visible = true;
        MoveToFront();
        GetTree().Paused = true;
        Callable.From(EnsureWorkbenchFillsViewport).CallDeferred();

        _titleLabel.Text = mode == MapWorkbenchMode.GenerateNewGame
            ? "New game — map generator"
            : "Map editor — generation settings";

        if (_gameRoot is null)
        {
            _hintLabel.Text = "Game root not found.";
            return;
        }

        if (mode == MapWorkbenchMode.EditCurrentMap &&
            _gameRoot.ShellCommittedGenerationParameters is { } committed)
        {
            _landSlider.Value = committed.LandPercent;
            _seedSpin.Value = committed.Seed;
        }
        else
        {
            _landSlider.Value = 55;
            _seedSpin.Value = (int)(GD.Randi() % 2147483647);
        }

        UpdateWaterLabel();

        if (mode == MapWorkbenchMode.EditCurrentMap)
        {
            var floor0 = _gameRoot.ShellWorldMap.GetOrCreateFloor(0);
            _previewRect.Texture = MapPreviewRasterizer.RasterizeFloor(floor0, _gameRoot.ShellWorldMap.TerrainConfig);
        }
        else
        {
            OnPreviewPressed();
        }
    }

    private void OnLandSliderChanged(double value)
    {
        UpdateWaterLabel();
    }

    private void UpdateWaterLabel()
    {
        var land = (int)GodotMathf.Clamp(_landSlider.Value, 0, 100);
        _waterValueLabel.Text = $"Water {100 - land}%";
    }

    private MapGenerationParameters BuildParametersFromUi()
    {
        var seed = (int)_seedSpin.Value;
        var land = (int)GodotMathf.Clamp(_landSlider.Value, 0, 100);
        return MapGenerationParameters.Create(seed, land);
    }

    private void OnPreviewPressed()
    {
        if (_gameRoot is null)
        {
            return;
        }

        var p = BuildParametersFromUi();
        var previewMap = ProceduralWorldMapGenerator.BuildBoundedWorld(128, 128, 32, 32, p);
        WaterTerrainRules.ApplyMinimumWaterBlobSizeTwoByTwo(previewMap);
        _previewRect.Texture =
            MapPreviewRasterizer.RasterizeFloor(previewMap.GetOrCreateFloor(0), previewMap.TerrainConfig);
    }

    private void OnApplyPressed()
    {
        if (_gameRoot is null)
        {
            return;
        }

        var p = BuildParametersFromUi();
        var map = ProceduralWorldMapGenerator.BuildBoundedWorld(
            _gameRoot.ShellDefaultMapWidthCells,
            _gameRoot.ShellDefaultMapHeightCells,
            _gameRoot.ShellChunkWidthCells,
            _gameRoot.ShellChunkHeightCells,
            p);

        _gameRoot.ApplyMapFromWorkbench(map, p);
        CloseWorkbench();
    }

    private void OnClosePressed() => CloseWorkbench();
}
