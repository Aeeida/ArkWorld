using Godot;
using System;
using Ark.Events;

namespace Ark.UI;

/// <summary>
/// 太空发射控制面板 — 火箭在发射台上准备发射时显示的 HUD。
///
/// 包含：
///   • 方向仪 (Navball) — 简化版方位显示
///   • 高度 / 速度 / 加速度 读数
///   • 燃油剩余百分比 + 消耗率
///   • 推重比实时变化
///   • 油门控制
///   • 分级按钮
///   • 发射 / 中止按钮
/// </summary>
public partial class LaunchControlPanel : CanvasLayer
{
    // ── 遥测标签 ──
    private Label?   _altitudeLabel;
    private Label?   _speedLabel;
    private Label?   _hSpeedLabel;
    private Label?   _accelLabel;
    private Label?   _twrLabel;
    private Label?   _fuelLabel;
    private Label?   _fuelRateLabel;
    private Label?   _phaseLabel;
    private Label?   _throttleLabel;
    private Label?   _dragLabel;

    // ── 方向仪（简化为数字航向 + 俯仰 + 滚转）──
    private Label?   _navballHeading;
    private Label?   _navballPitch;
    private Label?   _navballRoll;
    private Label?   _hoverLabel;

    // ── 操控提示 ──
    private Label?   _controlHintLabel;

    // ── 控制按钮 ──
    private Button?  _launchBtn;
    private Button?  _abortBtn;
    private Button?  _stageBtn;
    private HSlider? _throttleSlider;

    // ── 根容器 ──
    private PanelContainer? _root;

    // ── 状态缓存 ──
    private bool _isLaunched;

    /// <summary>发射按钮点击。</summary>
    public event Action? OnLaunchPressed;
    /// <summary>中止按钮点击。</summary>
    public event Action? OnAbortPressed;
    /// <summary>分级按钮点击。</summary>
    public event Action? OnStagePressed;
    /// <summary>油门变化 (0~1)。</summary>
    public event Action<float>? OnThrottleChanged;

    public override void _Ready()
    {
        Layer = 9;
        BuildUI();
        _root!.Visible = false;
    }

    public void Show()
    {
        _isLaunched = false;
        if (_root != null) _root.Visible = true;
        if (_launchBtn != null) { _launchBtn.Visible = true; _launchBtn.Disabled = false; }
        if (_abortBtn != null) _abortBtn.Visible = true;
        if (_throttleSlider != null) _throttleSlider.Value = 100;
    }

    public new void Hide()
    {
        if (_root != null) _root.Visible = false;
    }

    /// <summary>标记已发射 — 发射按钮变为灰色。</summary>
    public void MarkLaunched()
    {
        _isLaunched = true;
        if (_launchBtn != null) { _launchBtn.Text = "\u2705 已发射"; _launchBtn.Disabled = true; }
    }

    /// <summary>每帧更新遥测数据。</summary>
    public void UpdateTelemetry(TelemetryData d)
    {
        if (_altitudeLabel != null)
        {
            string altUnit = d.Altitude >= 1000 ? $"{d.Altitude / 1000f:F1} km" : $"{d.Altitude:F0} m";
            _altitudeLabel.Text = $"\U0001f4cf 高度: {altUnit}";
        }
        if (_speedLabel != null) _speedLabel.Text = $"\u26a1 垂直: {d.Velocity:F1}  3D: {d.Speed3D:F1} m/s";
        if (_hSpeedLabel != null) _hSpeedLabel.Text = $"\u27a1 水平: {d.HorizontalSpeed:F1} m/s";
        if (_accelLabel != null) _accelLabel.Text = $"\U0001f4c8 加速度: {d.Acceleration:F1} m/s\u00b2";
        if (_twrLabel != null)
        {
            _twrLabel.Text = $"\u2696 TWR: {d.TWR:F2}";
            _twrLabel.AddThemeColorOverride("font_color",
                d.TWR >= 1.0f ? new Color(0.4f, 1f, 0.4f) : new Color(1f, 0.4f, 0.4f));
        }
        if (_fuelLabel != null)
        {
            _fuelLabel.Text = $"\U0001f6e2 燃料: {d.FuelPercent:F1}%";
            _fuelLabel.AddThemeColorOverride("font_color",
                d.FuelPercent > 25f ? new Color(0.5f, 0.9f, 0.5f)
                : d.FuelPercent > 5f ? new Color(1f, 0.8f, 0.3f)
                : new Color(1f, 0.3f, 0.3f));
        }
        if (_fuelRateLabel != null) _fuelRateLabel.Text = $"\U0001f4a7 消耗: {d.FuelBurnRate:F1}/s";
        if (_dragLabel != null) _dragLabel.Text = $"\U0001f4a8 阻力: {d.DragForce:F0}N";
        if (_navballHeading != null) _navballHeading.Text = $"偏航: {d.Heading:F0}\u00b0";
        if (_navballPitch != null) _navballPitch.Text = $"俯仰: {d.Pitch:F1}\u00b0";
        if (_navballRoll != null) _navballRoll.Text = $"滚转: {d.Roll:F1}\u00b0";
        if (_hoverLabel != null)
        {
            if (d.EngineCutoff)
            {
                _hoverLabel.Text = "\u274c 引擎关闭";
                _hoverLabel.AddThemeColorOverride("font_color", new Color(1f, 0.3f, 0.3f));
            }
            else if (d.HoverMode)
            {
                _hoverLabel.Text = "\U0001f6e9 悬停中";
                _hoverLabel.AddThemeColorOverride("font_color", new Color(0.3f, 1f, 1f));
            }
            else
            {
                _hoverLabel.Text = $"油门: {d.Throttle * 100f:F0}%";
                _hoverLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.8f));
            }
        }
        if (_phaseLabel != null) _phaseLabel.Text = $"\U0001f680 {d.PhaseName}";

        // 同步 UI 滑块（键盘改油门时反映到滑块）
        if (_throttleSlider != null && !_throttleSlider.HasFocus())
            _throttleSlider.SetValueNoSignal(d.Throttle * 100f);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                          UI 构建
    // ═══════════════════════════════════════════════════════════════════════

    private void BuildUI()
    {
        // 右侧纵向面板
        _root = new PanelContainer { Name = "LaunchControlRoot" };
        _root.AnchorLeft   = 1f;
        _root.AnchorRight  = 1f;
        _root.AnchorTop    = 0f;
        _root.AnchorBottom = 1f;
        _root.GrowHorizontal = Control.GrowDirection.Begin;
        _root.GrowVertical   = Control.GrowDirection.End;
        _root.OffsetLeft   = -320;
        _root.OffsetRight  = -8;
        _root.OffsetTop    = 8;
        _root.OffsetBottom = -8;
        _root.AddThemeStyleboxOverride("panel", MakeStyle());
        _root.MouseFilter = Control.MouseFilterEnum.Ignore;
        AddChild(_root);

        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        _root.AddChild(scroll);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(vbox);

        // ── 飞行阶段 ──
        _phaseLabel = MakeLabel("\U0001f680 待发射", 16, new Color(0.5f, 0.8f, 1f));
        vbox.AddChild(_phaseLabel);

        vbox.AddChild(MakeSep());

        // ── 遥测数据（纵向排列）──
        _altitudeLabel = MakeLabel("\U0001f4cf 高度: 0 m", 13, new Color(0.8f, 0.8f, 0.8f));
        vbox.AddChild(_altitudeLabel);

        _speedLabel = MakeLabel("\u26a1 垂直: 0  3D: 0 m/s", 13, new Color(0.8f, 0.8f, 0.8f));
        vbox.AddChild(_speedLabel);

        _hSpeedLabel = MakeLabel("\u27a1 水平: 0 m/s", 12, new Color(0.7f, 0.7f, 0.7f));
        vbox.AddChild(_hSpeedLabel);

        _accelLabel = MakeLabel("\U0001f4c8 加速度: 0 m/s\u00b2", 12, new Color(0.7f, 0.7f, 0.7f));
        vbox.AddChild(_accelLabel);

        _twrLabel = MakeLabel("\u2696 TWR: 0", 13, new Color(0.4f, 1f, 0.4f));
        vbox.AddChild(_twrLabel);

        _fuelLabel = MakeLabel("\U0001f6e2 燃料: 100%", 13, new Color(0.5f, 0.9f, 0.5f));
        vbox.AddChild(_fuelLabel);

        _fuelRateLabel = MakeLabel("\U0001f4a7 消耗: 0/s", 11, new Color(0.6f, 0.6f, 0.6f));
        vbox.AddChild(_fuelRateLabel);

        _dragLabel = MakeLabel("\U0001f4a8 阻力: 0N", 11, new Color(0.6f, 0.6f, 0.6f));
        vbox.AddChild(_dragLabel);

        vbox.AddChild(MakeSep());

        // ── 方向仪 + 姿态 ──
        var navTitle = new Label { Text = "\U0001f9ed 方向仪" };
        navTitle.AddThemeFontSizeOverride("font_size", ArkTheme.ScaledFontSize(14));
        navTitle.AddThemeColorOverride("font_color", new Color(0.6f, 0.8f, 1f));
        vbox.AddChild(navTitle);

        _navballHeading = new Label { Text = "偏航: 0\u00b0" };
        _navballHeading.AddThemeFontSizeOverride("font_size", ArkTheme.ScaledFontSize(13));
        _navballHeading.AddThemeColorOverride("font_color", new Color(0.3f, 1f, 0.3f));
        vbox.AddChild(_navballHeading);

        _navballPitch = new Label { Text = "俯仰: 90\u00b0" };
        _navballPitch.AddThemeFontSizeOverride("font_size", ArkTheme.ScaledFontSize(13));
        _navballPitch.AddThemeColorOverride("font_color", new Color(0.3f, 0.8f, 1f));
        vbox.AddChild(_navballPitch);

        _navballRoll = new Label { Text = "滚转: 0\u00b0" };
        _navballRoll.AddThemeFontSizeOverride("font_size", ArkTheme.ScaledFontSize(13));
        _navballRoll.AddThemeColorOverride("font_color", new Color(0.8f, 0.7f, 0.3f));
        vbox.AddChild(_navballRoll);

        _hoverLabel = new Label { Text = "油门: 100%" };
        _hoverLabel.AddThemeFontSizeOverride("font_size", ArkTheme.ScaledFontSize(13));
        _hoverLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.8f));
        vbox.AddChild(_hoverLabel);

        vbox.AddChild(MakeSep());

        // ── 控制区 ──
        _throttleLabel = MakeLabel("油门滑块:", 12, new Color(0.8f, 0.8f, 0.8f));
        vbox.AddChild(_throttleLabel);

        _throttleSlider = new HSlider
        {
            MinValue = 0, MaxValue = 100, Value = 100, Step = 1,
            CustomMinimumSize = new Vector2(0, 20),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        _throttleSlider.ValueChanged += v =>
        {
            OnThrottleChanged?.Invoke((float)v / 100f);
        };
        vbox.AddChild(_throttleSlider);

        // 发射按钮
        _launchBtn = new Button
        {
            Text = "\U0001f680 发 射",
            CustomMinimumSize = new Vector2(0, 50),
            FocusMode = Control.FocusModeEnum.None,
        };
        _launchBtn.AddThemeFontSizeOverride("font_size", ArkTheme.ScaledFontSize(18));
        _launchBtn.AddThemeStyleboxOverride("normal", MakeButtonStyle(new Color(0.2f, 0.6f, 0.2f)));
        _launchBtn.AddThemeStyleboxOverride("hover", MakeButtonStyle(new Color(0.3f, 0.8f, 0.3f)));
        _launchBtn.Pressed += () => OnLaunchPressed?.Invoke();
        vbox.AddChild(_launchBtn);

        // 分级按钮
        _stageBtn = new Button
        {
            Text = "\u2702 分级",
            CustomMinimumSize = new Vector2(0, 40),
            FocusMode = Control.FocusModeEnum.None,
        };
        _stageBtn.AddThemeFontSizeOverride("font_size", ArkTheme.ScaledFontSize(14));
        _stageBtn.Pressed += () => OnStagePressed?.Invoke();
        vbox.AddChild(_stageBtn);

        // 中止按钮
        _abortBtn = new Button
        {
            Text = "\u26d4 紧急中止",
            CustomMinimumSize = new Vector2(0, 40),
            FocusMode = Control.FocusModeEnum.None,
        };
        _abortBtn.AddThemeFontSizeOverride("font_size", ArkTheme.ScaledFontSize(14));
        _abortBtn.AddThemeStyleboxOverride("normal", MakeButtonStyle(new Color(0.7f, 0.15f, 0.15f)));
        _abortBtn.AddThemeStyleboxOverride("hover", MakeButtonStyle(new Color(0.9f, 0.2f, 0.2f)));
        _abortBtn.Pressed += () => OnAbortPressed?.Invoke();
        vbox.AddChild(_abortBtn);

        vbox.AddChild(MakeSep());

        // ── 操控提示 ──
        _controlHintLabel = MakeLabel(
            "W/S=俯仰  A/D=偏航\nQ/E=滚转  Shift/Ctrl=推力±\nX=关引擎  H=悬停",
            10, new Color(0.5f, 0.5f, 0.6f));
        vbox.AddChild(_controlHintLabel);
    }

    private static Label MakeLabel(string text, int fontSize, Color color)
    {
        var lbl = new Label { Text = text };
        lbl.AddThemeFontSizeOverride("font_size", ArkTheme.ScaledFontSize(fontSize));
        lbl.AddThemeColorOverride("font_color", color);
        return lbl;
    }

    private static HSeparator MakeSep()
    {
        var sep = new HSeparator();
        sep.AddThemeColorOverride("color", new Color(0.2f, 0.3f, 0.5f, 0.5f));
        return sep;
    }

    private static StyleBoxFlat MakeStyle() => new()
    {
        BgColor = new Color(0.04f, 0.04f, 0.08f, 0.9f),
        CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10,
        CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10,
        ContentMarginLeft = 16, ContentMarginRight = 16,
        ContentMarginTop = 10, ContentMarginBottom = 10,
        BorderWidthTop = 2, BorderWidthBottom = 2,
        BorderWidthLeft = 2, BorderWidthRight = 2,
        BorderColor = new Color(0.2f, 0.5f, 0.8f, 0.7f),
    };

    private static StyleBoxFlat MakeButtonStyle(Color bg) => new()
    {
        BgColor = bg,
        CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6,
        CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
        ContentMarginLeft = 8, ContentMarginRight = 8,
        ContentMarginTop = 4, ContentMarginBottom = 4,
    };
}
