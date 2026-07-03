using Godot;
using System;
using Ark.Events;

namespace Ark.UI;

/// <summary>
/// 飞行报告面板 — 火箭着陆/坠毁后显示的任务总结。
///
/// 显示内容：
///   • 成功/失败状态（大标题 + 颜色区分）
///   • 飞行器名称
///   • 最大高度、最大速度
///   • 飞行时长
///   • 燃料消耗
///   • 级段使用
///   • 着陆/坠毁原因
///   • 撞击速度
///
/// 底部按钮：
///   • 返回生活模式
///   • 返回战斗模式
/// </summary>
public partial class FlightReportPanel : CanvasLayer
{
    private PanelContainer? _root;
    private Label?   _titleLabel;
    private Label?   _reportBody;
    private Button?  _btnLife;
    private Button?  _btnCombat;

    /// <summary>用户选择退出到哪个模式。</summary>
    public event Action<GameplayMode>? OnExitModeSelected;

    public override void _Ready()
    {
        Layer = 10;
        BuildUI();
        _root!.Visible = false;
    }

    /// <summary>显示飞行报告。</summary>
    public void ShowReport(FlightReport report)
    {
        if (_root == null || _titleLabel == null || _reportBody == null) return;

        // ── 标题 ──
        if (report.Success)
        {
            _titleLabel.Text = "\u2705 发射任务成功";
            _titleLabel.AddThemeColorOverride("font_color", new Color(0.3f, 1f, 0.4f));
        }
        else
        {
            _titleLabel.Text = "\u274c 发射任务失败";
            _titleLabel.AddThemeColorOverride("font_color", new Color(1f, 0.35f, 0.25f));
        }

        // ── 报告正文 ──
        string sep = "─────────────────────────────";
        string statusIcon = report.Success ? "\U0001f3c6" : "\U0001f4a5";

        _reportBody.Text =
            $"{statusIcon} 飞行器: {report.VesselName}\n" +
            $"{sep}\n" +
            $"\n" +
            $"\U0001f4cf 最大高度:  {report.FormatAltitude()}\n" +
            $"\u26a1 最大速度:  {report.MaxSpeed:F1} m/s\n" +
            $"\u23f1 飞行时长:  {report.FormatDuration()}\n" +
            $"\n" +
            $"\U0001f6e2 燃料消耗:  {report.FuelConsumed:F0} / {report.TotalFuelCapacity:F0} ({report.FuelUsedPercent:F1}%)\n" +
            $"\u2696 初始质量:  {report.InitialMass:F1}t → 终态: {report.FinalMass:F1}t\n" +
            $"\U0001f680 级段使用:  {report.StagesUsed} / {report.TotalStages}\n" +
            $"\n" +
            $"{sep}\n" +
            $"\n" +
            $"\U0001fa82 降落伞:   {(report.ParachuteDeployed ? "\u2705 已展开" : "\u274c 未展开")}\n" +
            $"\U0001f4a5 撞击速度:  {report.ImpactSpeed:F1} m/s\n" +
            $"\U0001f4cb 结果:     {report.EndReason}\n";

        _reportBody.AddThemeColorOverride("font_color",
            report.Success ? new Color(0.8f, 0.9f, 0.8f) : new Color(0.9f, 0.8f, 0.75f));

        _root.Visible = true;
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    public new void Hide()
    {
        if (_root != null) _root.Visible = false;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                          UI 构建
    // ═══════════════════════════════════════════════════════════════════════

    private void BuildUI()
    {
        _root = new PanelContainer { Name = "FlightReportRoot" };
        _root.AnchorLeft   = 0.5f;
        _root.AnchorRight  = 0.5f;
        _root.AnchorTop    = 0.5f;
        _root.AnchorBottom = 0.5f;
        _root.GrowHorizontal = Control.GrowDirection.Both;
        _root.GrowVertical   = Control.GrowDirection.Both;
        _root.OffsetLeft   = -280;
        _root.OffsetRight  =  280;
        _root.OffsetTop    = -240;
        _root.OffsetBottom =  240;
        _root.AddThemeStyleboxOverride("panel", MakePanelStyle());
        AddChild(_root);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 10);
        _root.AddChild(vbox);

        // ── 标题 ──
        _titleLabel = new Label
        {
            Text = "飞行报告",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _titleLabel.AddThemeFontSizeOverride("font_size", ArkTheme.ScaledFontSize(24));
        vbox.AddChild(_titleLabel);

        // ── 分割线 ──
        vbox.AddChild(new HSeparator());

        // ── 报告正文 ──
        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        vbox.AddChild(scroll);

        _reportBody = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        _reportBody.AddThemeFontSizeOverride("font_size", ArkTheme.ScaledFontSize(14));
        scroll.AddChild(_reportBody);

        // ── 分割线 ──
        vbox.AddChild(new HSeparator());

        // ── 退出模式选择说明 ──
        var exitHint = new Label
        {
            Text = "选择返回的模式:",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        exitHint.AddThemeFontSizeOverride("font_size", ArkTheme.ScaledFontSize(13));
        exitHint.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.7f));
        vbox.AddChild(exitHint);

        // ── 按钮栏 ──
        var btnBox = new HBoxContainer();
        btnBox.AddThemeConstantOverride("separation", 16);
        btnBox.Alignment = BoxContainer.AlignmentMode.Center;
        vbox.AddChild(btnBox);

        _btnLife = new Button
        {
            Text = "\U0001f3e0 返回生活模式",
            CustomMinimumSize = new Vector2(200, 44),
            FocusMode = Control.FocusModeEnum.None,
        };
        _btnLife.AddThemeFontSizeOverride("font_size", ArkTheme.ScaledFontSize(15));
        _btnLife.AddThemeStyleboxOverride("normal", MakeButtonStyle(new Color(0.15f, 0.4f, 0.2f)));
        _btnLife.AddThemeStyleboxOverride("hover",  MakeButtonStyle(new Color(0.2f, 0.55f, 0.3f)));
        _btnLife.Pressed += () =>
        {
            Hide();
            OnExitModeSelected?.Invoke(GameplayMode.Life);
        };
        btnBox.AddChild(_btnLife);

        _btnCombat = new Button
        {
            Text = "\u2694\ufe0f 返回战斗模式",
            CustomMinimumSize = new Vector2(200, 44),
            FocusMode = Control.FocusModeEnum.None,
        };
        _btnCombat.AddThemeFontSizeOverride("font_size", ArkTheme.ScaledFontSize(15));
        _btnCombat.AddThemeStyleboxOverride("normal", MakeButtonStyle(new Color(0.45f, 0.15f, 0.15f)));
        _btnCombat.AddThemeStyleboxOverride("hover",  MakeButtonStyle(new Color(0.6f, 0.2f, 0.2f)));
        _btnCombat.Pressed += () =>
        {
            Hide();
            OnExitModeSelected?.Invoke(GameplayMode.Combat);
        };
        btnBox.AddChild(_btnCombat);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                          样式
    // ═══════════════════════════════════════════════════════════════════════

    private static StyleBoxFlat MakePanelStyle() => new()
    {
        BgColor = new Color(0.06f, 0.06f, 0.1f, 0.96f),
        CornerRadiusTopLeft = 14, CornerRadiusTopRight = 14,
        CornerRadiusBottomLeft = 14, CornerRadiusBottomRight = 14,
        ContentMarginLeft = 24, ContentMarginRight = 24,
        ContentMarginTop = 16, ContentMarginBottom = 16,
        BorderWidthTop = 2, BorderWidthBottom = 2,
        BorderWidthLeft = 2, BorderWidthRight = 2,
        BorderColor = new Color(0.25f, 0.35f, 0.65f, 0.8f),
    };

    private static StyleBoxFlat MakeButtonStyle(Color bg) => new()
    {
        BgColor = bg,
        CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8,
        CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
        ContentMarginLeft = 12, ContentMarginRight = 12,
        ContentMarginTop = 6, ContentMarginBottom = 6,
    };
}
