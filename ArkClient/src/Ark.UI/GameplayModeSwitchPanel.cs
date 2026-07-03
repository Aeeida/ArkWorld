using Godot;
using System;
using Ark.Events;

namespace Ark.UI;

public partial class GameplayModeSwitchPanel : CanvasLayer
{
    private const int BtnWidth  = 90;
    private const int BtnHeight = 34;
    private const int Spacing   = 6;
    private const int Margin    = 12;

    private PanelContainer? _panel;
    private Button? _btnLife;
    private Button? _btnCombat;
    private Label?  _spaceIndicator;
    private Label?  _modeLabel;

    private GameplayMode _currentMode = GameplayMode.Combat;

    public event Action<GameplayMode>? OnModeSelected;
    /// <summary>用户从太空/火箭设计模式强制退出时触发。</summary>
    public event Action? OnForceExitSpaceMode;
    public GameplayMode CurrentMode => _currentMode;

    public override void _Ready()
    {
        Layer = 9;
        BuildUI();
        UpdateVisuals();
    }

    public void SetMode(GameplayMode mode)
    {
        _currentMode = mode;
        UpdateVisuals();
    }

    private void BuildUI()
    {
        _panel = new PanelContainer { Name = "ModeSwitchPanel" };
        _panel.AnchorLeft   = 0.5f;
        _panel.AnchorRight  = 0.5f;
        _panel.AnchorTop    = 0f;
        _panel.AnchorBottom = 0f;
        _panel.GrowHorizontal = Control.GrowDirection.Both;
        _panel.GrowVertical   = Control.GrowDirection.End;
        int totalWidth = 2 * BtnWidth + Spacing + 2 * Margin;
        int totalHeight = BtnHeight + 42 + 2 * Margin;
        _panel.OffsetLeft   = -(totalWidth / 2);
        _panel.OffsetRight  =  (totalWidth / 2);
        _panel.OffsetTop    = Margin;
        _panel.OffsetBottom = Margin + totalHeight;
        _panel.AddThemeStyleboxOverride("panel", MakePanelStyle());
        _panel.MouseFilter = Control.MouseFilterEnum.Ignore;
        AddChild(_panel);
        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 4);
        _panel.AddChild(vbox);
        var btnBox = new HBoxContainer();
        btnBox.AddThemeConstantOverride("separation", Spacing);
        btnBox.Alignment = BoxContainer.AlignmentMode.Center;
        vbox.AddChild(btnBox);
        _btnLife   = MakeButton("\U0001f3e0 \u751f\u6d3b", GameplayMode.Life);
        _btnCombat = MakeButton("\u2694\ufe0f \u6218\u6597", GameplayMode.Combat);
        btnBox.AddChild(_btnLife);
        btnBox.AddChild(_btnCombat);
        _spaceIndicator = new Label
        {
            Text = "\U0001f680 \u592a\u7a7a\u6a21\u5f0f",
            HorizontalAlignment = HorizontalAlignment.Center,
            Visible = false
        };
        _spaceIndicator.AddThemeFontSizeOverride("font_size", ArkTheme.ScaledFontSize(11));
        _spaceIndicator.AddThemeColorOverride("font_color", new Color(0.4f, 0.6f, 1f));
        vbox.AddChild(_spaceIndicator);
        _modeLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Text = "\u5f53\u524d\uff1a\u6218\u6597\u6a21\u5f0f"
        };
        _modeLabel.AddThemeFontSizeOverride("font_size", ArkTheme.ScaledFontSize(11));
        _modeLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
        vbox.AddChild(_modeLabel);
    }

    private Button MakeButton(string label, GameplayMode mode)
    {
        var btn = new Button
        {
            Text = label,
            CustomMinimumSize = new Vector2(BtnWidth, BtnHeight),
            FocusMode = Control.FocusModeEnum.None,
        };
        btn.Pressed += () => OnButtonPressed(mode);
        return btn;
    }

    private void OnButtonPressed(GameplayMode mode)
    {
        if (mode == _currentMode) return;
        // 太空模式下点击按钮 → 强制退出太空模式再切换
        if (_currentMode == GameplayMode.Space)
        {
            OnForceExitSpaceMode?.Invoke();
        }
        _currentMode = mode;
        UpdateVisuals();
        OnModeSelected?.Invoke(mode);
    }

    private void UpdateVisuals()
    {
        bool inSpace = _currentMode == GameplayMode.Space;
        // 太空模式下按钮不禁用 — 允许玩家点击强制退出
        StyleButton(_btnLife,   GameplayMode.Life);
        StyleButton(_btnCombat, GameplayMode.Combat);
        if (_spaceIndicator != null) _spaceIndicator.Visible = inSpace;
        StyleButton(_btnLife,   GameplayMode.Life);
        StyleButton(_btnCombat, GameplayMode.Combat);
        if (_modeLabel != null)
        {
            _modeLabel.Text = _currentMode switch
            {
                GameplayMode.Life   => "\u5f53\u524d\uff1a\u751f\u6d3b\u6a21\u5f0f",
                GameplayMode.Combat => "\u5f53\u524d\uff1a\u6218\u6597\u6a21\u5f0f",
                GameplayMode.Space  => "\u5f53\u524d\uff1a\u592a\u7a7a\u6a21\u5f0f",
                _ => "\u5f53\u524d\uff1a\u672a\u77e5"
            };
            _modeLabel.AddThemeColorOverride("font_color", _currentMode switch
            {
                GameplayMode.Life   => new Color(0.4f, 0.8f, 0.4f),
                GameplayMode.Combat => new Color(0.9f, 0.4f, 0.4f),
                GameplayMode.Space  => new Color(0.4f, 0.6f, 1f),
                _ => new Color(0.7f, 0.7f, 0.7f)
            });
        }
    }

    private void StyleButton(Button? btn, GameplayMode mode)
    {
        if (btn == null) return;
        bool active = mode == _currentMode;
        var (activeColor, normalColor) = mode switch
        {
            GameplayMode.Life   => (new Color(0.2f, 0.7f, 0.3f), new Color(0.15f, 0.35f, 0.18f)),
            GameplayMode.Combat => (new Color(0.8f, 0.2f, 0.2f), new Color(0.4f, 0.15f, 0.15f)),
            _ => (Colors.White, Colors.Gray)
        };
        var style = new StyleBoxFlat
        {
            BgColor = active ? activeColor : normalColor,
            CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
            ContentMarginLeft = 8, ContentMarginRight = 8,
            ContentMarginTop = 4, ContentMarginBottom = 4,
        };
        if (active) { style.BorderWidthBottom = 3; style.BorderColor = Colors.White; }
        btn.AddThemeStyleboxOverride("normal",  style);
        btn.AddThemeStyleboxOverride("pressed", style);
        var hover = (StyleBoxFlat)style.Duplicate();
        hover.BgColor = active ? activeColor : normalColor.Lightened(0.2f);
        btn.AddThemeStyleboxOverride("hover", hover);
        var disabled = (StyleBoxFlat)style.Duplicate();
        disabled.BgColor = normalColor.Darkened(0.3f);
        btn.AddThemeStyleboxOverride("disabled", disabled);
        btn.AddThemeColorOverride("font_color", active ? Colors.White : new Color(0.8f, 0.8f, 0.8f));
        btn.AddThemeFontSizeOverride("font_size", 14);
    }

    private static StyleBoxFlat MakePanelStyle() => new()
    {
        BgColor = new Color(0.08f, 0.08f, 0.12f, 0.85f),
        CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8,
        CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
        ContentMarginLeft = Margin, ContentMarginRight = Margin,
        ContentMarginTop = 8, ContentMarginBottom = 8,
        BorderWidthTop = 1, BorderWidthBottom = 1,
        BorderWidthLeft = 1, BorderWidthRight = 1,
        BorderColor = new Color(0.3f, 0.3f, 0.4f, 0.6f),
    };
}
