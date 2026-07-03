using Godot;

namespace Ark.UI;

public partial class SeatWeaponPanel : CanvasLayer
{
    private Label _title = null!;
    private ProgressBar _heatBar = null!;
    private ProgressBar _reloadBar = null!;
    private ProgressBar _maintenanceBar = null!;
    private Label _statusLabel = null!;

    public override void _Ready()
    {
        Layer = 8;

        var panel = new PanelContainer();
        panel.AnchorLeft = 1f;
        panel.AnchorRight = 1f;
        panel.AnchorTop = 1f;
        panel.AnchorBottom = 1f;
        panel.GrowHorizontal = Control.GrowDirection.Begin;
        panel.GrowVertical = Control.GrowDirection.Begin;
        panel.OffsetLeft = -320;
        panel.OffsetRight = -16;
        panel.OffsetTop = -170;
        panel.OffsetBottom = -20;
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.04f, 0.05f, 0.08f, 0.78f),
            BorderColor = new Color(0.42f, 0.62f, 0.95f, 0.8f),
            BorderWidthBottom = 2,
            BorderWidthTop = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            ContentMarginLeft = 10,
            ContentMarginRight = 10,
            ContentMarginTop = 8,
            ContentMarginBottom = 8,
        });
        AddChild(panel);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 6);
        panel.AddChild(root);

        _title = MakeLabel(15, new Color(0.86f, 0.92f, 1f));
        root.AddChild(_title);

        _heatBar = MakeBar(new Color(0.94f, 0.46f, 0.18f), new Color(0.22f, 0.1f, 0.06f));
        root.AddChild(_heatBar);

        _reloadBar = MakeBar(new Color(0.32f, 0.82f, 0.58f), new Color(0.08f, 0.18f, 0.12f));
        root.AddChild(_reloadBar);

        _maintenanceBar = MakeBar(new Color(0.46f, 0.72f, 0.98f), new Color(0.08f, 0.12f, 0.18f));
        root.AddChild(_maintenanceBar);

        _statusLabel = MakeLabel(12, new Color(0.78f, 0.84f, 0.92f));
        root.AddChild(_statusLabel);

        Visible = false;
    }

    public void UpdatePanel(string title, float heat, float reloadNormalized, float cycleRemaining, float reloadRemaining, float maintenanceLevel, float maintenanceRemaining, float operationProgress, float skillScalar, byte repairStep, byte repairStepCount, byte materialUnits, byte faultCode)
    {
        Visible = true;
        _title.Text = title;
        _heatBar.Value = heat * 100f;
        _reloadBar.Value = reloadNormalized * 100f;
        _maintenanceBar.Value = maintenanceLevel * 100f;
        string stageText = repairStepCount > 0 ? $"  |  Step {repairStep}/{repairStepCount}  |  Prog {operationProgress:P0}" : string.Empty;
        string materialText = materialUnits > 0 ? $"  |  Mat {materialUnits}" : string.Empty;
        string skillText = skillScalar > 0f ? $"  |  Skill x{skillScalar:F2}" : string.Empty;
        _statusLabel.Text = faultCode switch
        {
            2 => $"状态: 过热恢复  |  Cycle {cycleRemaining:F2}s{stageText}{materialText}{skillText}",
            1 => $"状态: 卡壳排故  |  Reload {reloadRemaining:F2}s{stageText}{materialText}{skillText}",
            3 => $"状态: 供弹故障  |  Cycle {cycleRemaining:F2}s{stageText}{materialText}{skillText}",
            4 => $"状态: 对准故障  |  Cycle {cycleRemaining:F2}s{stageText}{materialText}{skillText}",
            _ when maintenanceRemaining > 0f => $"状态: 武器维护 {maintenanceRemaining:F2}s{stageText}{materialText}{skillText}",
            _ when reloadRemaining > 0f => $"状态: 装填动画 {reloadRemaining:F2}s",
            _ => $"状态: 就绪  |  Cycle {cycleRemaining:F2}s",
        };
    }

    public void HidePanel()
    {
        Visible = false;
    }

    private static Label MakeLabel(int fontSize, Color color)
    {
        var label = new Label();
        label.AddThemeFontSizeOverride("font_size", ArkTheme.ScaledFontSize(fontSize));
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private static ProgressBar MakeBar(Color fill, Color background)
    {
        var bar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 100,
            Value = 0,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(260, 16),
        };
        bar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = fill, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4, CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4 });
        bar.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = background, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4, CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4 });
        return bar;
    }
}
