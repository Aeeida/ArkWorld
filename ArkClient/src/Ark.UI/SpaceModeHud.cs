using Godot;
using System;
using System.Collections.Generic;
using Ark.Shared.Data;

namespace Ark.UI;

/// <summary>
/// 太空模式 HUD — 火箭发射 / 飞船控制 / 军团指挥。
///
/// 布局（参考 EVE Online / Star Citizen）：
///   中心：瞄准准星 + 3D 球形雷达概览
///   左右：船体护盾弧（前后左右）+ 能量/电容条 + 速度/推力
///   底部：模块栏（F1-F8 武器+防御模块）
///   顶部：导航指示（跃迁/自动驾驶）+ 锁定目标列表
///   军团指挥叠加：舰队组成窗口 + 广播/命令
///
/// 风格：未来主义科幻（全息 HUD、霓虹蓝/绿线条）。
/// </summary>
public partial class SpaceModeHud : CanvasLayer
{
    // ─── 颜色主题（科幻霓虹蓝绿） ───
    private static readonly Color PanelBg      = new(0.02f, 0.04f, 0.08f, 0.75f);
    private static readonly Color NeonBlue     = new(0.2f, 0.6f, 1f);
    private static readonly Color NeonGreen    = new(0.1f, 0.9f, 0.5f);
    private static readonly Color NeonCyan     = new(0.2f, 0.85f, 0.9f);
    private static readonly Color WarningRed   = new(1f, 0.25f, 0.2f);
    private static readonly Color TextWhite    = new(0.88f, 0.92f, 0.96f);
    private static readonly Color DimText      = new(0.4f, 0.5f, 0.6f);
    private static readonly Color SlotBg       = new(0.04f, 0.08f, 0.14f, 0.9f);
    private static readonly Color SlotBorder   = new(0.15f, 0.4f, 0.6f, 0.7f);

    // ─── 船体状态 ───
    private Label? _shipNameLabel;
    private ProgressBar? _hullBar;
    private ProgressBar? _shieldFrontBar;
    private ProgressBar? _shieldRearBar;
    private ProgressBar? _shieldLeftBar;
    private ProgressBar? _shieldRightBar;
    private ProgressBar? _capacitorBar;
    private Label? _speedLabel;

    // ─── 模块栏 ───
    private HBoxContainer? _moduleBar;
    private readonly List<Button> _moduleSlots = [];
    private readonly List<string> _moduleAbilityIds = [];

    // ─── 辅助模块栏（指挥技能） ───
    private HBoxContainer? _commandBar;
    private readonly List<Button> _commandSlots = [];
    private readonly List<string> _commandAbilityIds = [];

    // ─── 导航 ───
    private Label? _navLabel;
    private Label? _autopilotLabel;

    // ─── 锁定目标列表 ───
    private VBoxContainer? _lockTargets;

    // ─── 军团指挥覆盖 ───
    private PanelContainer? _fleetPanel;
    private Label? _fleetNameLabel;
    private Label? _fleetMemberCount;
    private VBoxContainer? _fleetMemberList;
    private bool _fleetCommandMode;

    public event Action<int, string>? OnModuleActivated;
    public event Action<int, string>? OnCommandActivated;

    public override void _Ready()
    {
        Layer = 8;
        BuildShipStatus();
        BuildModuleBar();
        BuildCommandBar();
        BuildNavigation();
        BuildLockTargets();
        BuildFleetOverlay();
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                   左侧：船体/护盾/电容
    // ═══════════════════════════════════════════════════════════════════════

    private void BuildShipStatus()
    {
        var panel = new PanelContainer();
        panel.AnchorLeft = 0f; panel.AnchorRight = 0f;
        panel.AnchorTop = 0.3f; panel.AnchorBottom = 0.3f;
        panel.GrowVertical = Control.GrowDirection.End;
        panel.OffsetLeft = 8; panel.OffsetRight = 200;
        panel.OffsetTop = 0; panel.OffsetBottom = 280;
        panel.AddThemeStyleboxOverride("panel", MakeHoloPanel());
        AddChild(panel);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 4);
        panel.AddChild(vbox);

        _shipNameLabel = MakeLabel("🚀 飞船", 13, NeonCyan);
        vbox.AddChild(_shipNameLabel);

        // 船体
        vbox.AddChild(MakeLabel("船体 Hull", 9, DimText));
        _hullBar = MakeBar(NeonGreen, new Color(0.05f, 0.15f, 0.08f), 170, 14);
        vbox.AddChild(_hullBar);

        // 护盾四向
        vbox.AddChild(MakeLabel("护盾 Shield", 9, DimText));

        var shieldGrid = new GridContainer { Columns = 2 };
        shieldGrid.AddThemeConstantOverride("h_separation", 4);
        shieldGrid.AddThemeConstantOverride("v_separation", 2);
        vbox.AddChild(shieldGrid);

        shieldGrid.AddChild(MakeLabel("前", 8, DimText));
        _shieldFrontBar = MakeBar(NeonBlue, new Color(0.05f, 0.08f, 0.18f), 80, 10);
        shieldGrid.AddChild(_shieldFrontBar);

        shieldGrid.AddChild(MakeLabel("后", 8, DimText));
        _shieldRearBar = MakeBar(NeonBlue, new Color(0.05f, 0.08f, 0.18f), 80, 10);
        shieldGrid.AddChild(_shieldRearBar);

        shieldGrid.AddChild(MakeLabel("左", 8, DimText));
        _shieldLeftBar = MakeBar(NeonBlue, new Color(0.05f, 0.08f, 0.18f), 80, 10);
        shieldGrid.AddChild(_shieldLeftBar);

        shieldGrid.AddChild(MakeLabel("右", 8, DimText));
        _shieldRightBar = MakeBar(NeonBlue, new Color(0.05f, 0.08f, 0.18f), 80, 10);
        shieldGrid.AddChild(_shieldRightBar);

        // 电容
        vbox.AddChild(MakeLabel("电容 Capacitor", 9, DimText));
        _capacitorBar = MakeBar(new Color(0.9f, 0.75f, 0.2f), new Color(0.2f, 0.18f, 0.05f), 170, 12);
        vbox.AddChild(_capacitorBar);

        // 速度
        _speedLabel = MakeLabel("速度: 0 m/s", 12, TextWhite);
        vbox.AddChild(_speedLabel);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                   底部：模块栏（F1-F8）
    // ═══════════════════════════════════════════════════════════════════════

    private void BuildModuleBar()
    {
        int slotSize = 50;
        int count = 8;
        int spacing = 4;

        var barPanel = new PanelContainer();
        barPanel.AnchorLeft = 0.5f; barPanel.AnchorRight = 0.5f;
        barPanel.AnchorTop = 1f; barPanel.AnchorBottom = 1f;
        barPanel.GrowHorizontal = Control.GrowDirection.Both;
        barPanel.GrowVertical = Control.GrowDirection.Begin;
        int totalW = count * slotSize + (count - 1) * spacing + 16;
        barPanel.OffsetLeft = -(totalW / 2); barPanel.OffsetRight = (totalW / 2);
        barPanel.OffsetTop = -80; barPanel.OffsetBottom = -8;
        barPanel.AddThemeStyleboxOverride("panel", MakeHoloPanel());
        AddChild(barPanel);

        _moduleBar = new HBoxContainer();
        _moduleBar.AddThemeConstantOverride("separation", spacing);
        _moduleBar.Alignment = BoxContainer.AlignmentMode.Center;
        barPanel.AddChild(_moduleBar);

        for (int i = 0; i < count; i++)
        {
            var btn = MakeSlotButton(slotSize, $"F{i + 1}", "—");
            _moduleBar.AddChild(btn);
            _moduleSlots.Add(btn);
            _moduleAbilityIds.Add("");
            int idx = i;
            btn.Pressed += () =>
            {
                if (idx < _moduleAbilityIds.Count && !string.IsNullOrEmpty(_moduleAbilityIds[idx]))
                    OnModuleActivated?.Invoke(idx, _moduleAbilityIds[idx]);
            };
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //               底部下方：指挥技能栏（Shift+F1-F5）
    // ═══════════════════════════════════════════════════════════════════════

    private void BuildCommandBar()
    {
        int slotSize = 46;
        int count = 5;
        int spacing = 4;

        var barPanel = new PanelContainer();
        barPanel.AnchorLeft = 0.5f; barPanel.AnchorRight = 0.5f;
        barPanel.AnchorTop = 1f; barPanel.AnchorBottom = 1f;
        barPanel.GrowHorizontal = Control.GrowDirection.Both;
        barPanel.GrowVertical = Control.GrowDirection.Begin;
        int totalW = count * slotSize + (count - 1) * spacing + 12;
        barPanel.OffsetLeft = -(totalW / 2); barPanel.OffsetRight = (totalW / 2);
        barPanel.OffsetTop = -130; barPanel.OffsetBottom = -86;
        barPanel.AddThemeStyleboxOverride("panel", MakeHoloPanel());
        AddChild(barPanel);

        _commandBar = new HBoxContainer();
        _commandBar.AddThemeConstantOverride("separation", spacing);
        _commandBar.Alignment = BoxContainer.AlignmentMode.Center;
        barPanel.AddChild(_commandBar);

        for (int i = 0; i < count; i++)
        {
            var btn = MakeSlotButton(slotSize, $"S+F{i + 1}", "—");
            _commandBar.AddChild(btn);
            _commandSlots.Add(btn);
            _commandAbilityIds.Add("");
            int idx = i;
            btn.Pressed += () =>
            {
                if (idx < _commandAbilityIds.Count && !string.IsNullOrEmpty(_commandAbilityIds[idx]))
                    OnCommandActivated?.Invoke(idx, _commandAbilityIds[idx]);
            };
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                   顶部：导航指示
    // ═══════════════════════════════════════════════════════════════════════

    private void BuildNavigation()
    {
        var panel = new PanelContainer();
        panel.AnchorLeft = 0.5f; panel.AnchorRight = 0.5f;
        panel.AnchorTop = 0f; panel.AnchorBottom = 0f;
        panel.GrowHorizontal = Control.GrowDirection.Both;
        panel.OffsetLeft = -140; panel.OffsetRight = 140;
        panel.OffsetTop = 8; panel.OffsetBottom = 50;
        panel.AddThemeStyleboxOverride("panel", MakeHoloPanel());
        AddChild(panel);

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 12);
        hbox.Alignment = BoxContainer.AlignmentMode.Center;
        panel.AddChild(hbox);

        _navLabel = MakeLabel("🌌 待机", 12, NeonCyan);
        hbox.AddChild(_navLabel);

        _autopilotLabel = MakeLabel("自动驾驶: 关闭", 10, DimText);
        hbox.AddChild(_autopilotLabel);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                   右侧：锁定目标列表
    // ═══════════════════════════════════════════════════════════════════════

    private void BuildLockTargets()
    {
        var panel = new PanelContainer();
        panel.AnchorLeft = 1f; panel.AnchorRight = 1f;
        panel.AnchorTop = 0.15f; panel.AnchorBottom = 0.15f;
        panel.GrowVertical = Control.GrowDirection.End;
        panel.OffsetLeft = -190; panel.OffsetRight = -8;
        panel.OffsetTop = 0; panel.OffsetBottom = 200;
        panel.AddThemeStyleboxOverride("panel", MakeHoloPanel());
        AddChild(panel);

        _lockTargets = new VBoxContainer();
        _lockTargets.AddThemeConstantOverride("separation", 3);
        panel.AddChild(_lockTargets);

        _lockTargets.AddChild(MakeLabel("🎯 锁定目标", 11, NeonBlue));
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                   军团指挥覆盖
    // ═══════════════════════════════════════════════════════════════════════

    private void BuildFleetOverlay()
    {
        _fleetPanel = new PanelContainer();
        _fleetPanel.AnchorLeft = 0f; _fleetPanel.AnchorRight = 0f;
        _fleetPanel.AnchorTop = 0f; _fleetPanel.AnchorBottom = 0f;
        _fleetPanel.GrowVertical = Control.GrowDirection.End;
        _fleetPanel.OffsetLeft = 8; _fleetPanel.OffsetRight = 220;
        _fleetPanel.OffsetTop = 8; _fleetPanel.OffsetBottom = 240;
        _fleetPanel.AddThemeStyleboxOverride("panel", MakeHoloPanel());
        _fleetPanel.Visible = false;
        AddChild(_fleetPanel);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 4);
        _fleetPanel.AddChild(vbox);

        _fleetNameLabel = MakeLabel("⚓ 舰队", 12, NeonCyan);
        vbox.AddChild(_fleetNameLabel);

        _fleetMemberCount = MakeLabel("成员: 0", 10, DimText);
        vbox.AddChild(_fleetMemberCount);

        _fleetMemberList = new VBoxContainer();
        _fleetMemberList.AddThemeConstantOverride("separation", 2);
        vbox.AddChild(_fleetMemberList);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                          公开 API
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>更新飞船状态。</summary>
    public void UpdateShipStatus(string shipName, float hull, float maxHull,
        float shieldF, float shieldR, float shieldL, float shieldRi, float shieldMax,
        float cap, float maxCap, float speed, float maxSpeed)
    {
        if (_shipNameLabel is not null) _shipNameLabel.Text = $"🚀 {shipName}";
        SetBar(_hullBar, hull, maxHull);
        SetBar(_shieldFrontBar, shieldF, shieldMax);
        SetBar(_shieldRearBar, shieldR, shieldMax);
        SetBar(_shieldLeftBar, shieldL, shieldMax);
        SetBar(_shieldRightBar, shieldRi, shieldMax);
        SetBar(_capacitorBar, cap, maxCap);
        if (_speedLabel is not null) _speedLabel.Text = $"速度: {speed:F0} / {maxSpeed:F0} m/s";
    }

    /// <summary>设置模块栏。</summary>
    public void SetModuleBar(IReadOnlyList<ClientActionBarSlot> slots, ClientAbilityDefRegistry? registry)
    {
        for (int i = 0; i < _moduleSlots.Count && i < slots.Count; i++)
        {
            var slot = slots[i];
            var def = registry?.Get(slot.AbilityId);
            _moduleSlots[i].Text = def is not null ? $"{def.Value.Icon}\n{slot.Hotkey}" : $"—\n{slot.Hotkey}";
            _moduleSlots[i].TooltipText = def?.Description ?? "";
            if (i < _moduleAbilityIds.Count) _moduleAbilityIds[i] = slot.AbilityId;
            else _moduleAbilityIds.Add(slot.AbilityId);
        }
    }

    /// <summary>设置指挥栏。</summary>
    public void SetCommandBar(IReadOnlyList<ClientActionBarSlot> slots, ClientAbilityDefRegistry? registry)
    {
        for (int i = 0; i < _commandSlots.Count && i < slots.Count; i++)
        {
            var slot = slots[i];
            var def = registry?.Get(slot.AbilityId);
            _commandSlots[i].Text = def is not null ? $"{def.Value.Icon}\n{slot.Hotkey}" : $"—\n{slot.Hotkey}";
            _commandSlots[i].TooltipText = def?.Description ?? "";
            if (i < _commandAbilityIds.Count) _commandAbilityIds[i] = slot.AbilityId;
            else _commandAbilityIds.Add(slot.AbilityId);
        }
    }

    /// <summary>更新导航状态。</summary>
    public void UpdateNavigation(string status, bool autopilot)
    {
        if (_navLabel is not null) _navLabel.Text = $"🌌 {status}";
        if (_autopilotLabel is not null) _autopilotLabel.Text = autopilot ? "自动驾驶: 开启" : "自动驾驶: 关闭";
    }

    /// <summary>添加锁定目标。</summary>
    public void AddLockedTarget(string name, float distance, float hpPercent)
    {
        if (_lockTargets is null) return;
        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 4);

        var nameLabel = MakeLabel(name, 10, WarningRed);
        hbox.AddChild(nameLabel);

        var distLabel = MakeLabel($"{distance:F0}km", 9, DimText);
        hbox.AddChild(distLabel);

        var hpLabel = MakeLabel($"{hpPercent:F0}%", 9, hpPercent > 50 ? NeonGreen : WarningRed);
        hbox.AddChild(hpLabel);

        _lockTargets.AddChild(hbox);
    }

    /// <summary>清除锁定目标列表。</summary>
    public void ClearLockedTargets()
    {
        if (_lockTargets is null) return;
        // 保留第一个标题
        while (_lockTargets.GetChildCount() > 1)
            _lockTargets.GetChild(_lockTargets.GetChildCount() - 1).QueueFree();
    }

    /// <summary>切换军团指挥模式。</summary>
    public void SetFleetCommandMode(bool active)
    {
        _fleetCommandMode = active;
        if (_fleetPanel is not null) _fleetPanel.Visible = active;
    }

    /// <summary>更新舰队信息。</summary>
    public void UpdateFleetInfo(string fleetName, int memberCount)
    {
        if (_fleetNameLabel is not null) _fleetNameLabel.Text = $"⚓ {fleetName}";
        if (_fleetMemberCount is not null) _fleetMemberCount.Text = $"成员: {memberCount}";
    }

    /// <summary>更新舰队成员列表。</summary>
    public void SetFleetMembers(IReadOnlyList<(string Name, string ShipType, float HpPct, float ShieldPct)> members)
    {
        if (_fleetMemberList is null) return;
        // 清除旧条目
        foreach (var child in _fleetMemberList.GetChildren()) child.QueueFree();

        foreach (var m in members)
        {
            var hbox = new HBoxContainer();
            hbox.AddThemeConstantOverride("separation", 4);

            hbox.AddChild(MakeLabel(m.Name, 9, TextWhite));
            hbox.AddChild(MakeLabel(m.ShipType, 8, DimText));
            hbox.AddChild(MakeLabel($"H:{m.HpPct:F0}%", 8, m.HpPct > 50 ? NeonGreen : WarningRed));
            hbox.AddChild(MakeLabel($"S:{m.ShieldPct:F0}%", 8, NeonBlue));

            _fleetMemberList.AddChild(hbox);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                          辅助
    // ═══════════════════════════════════════════════════════════════════════

    private static void SetBar(ProgressBar? bar, float current, float max)
    {
        if (bar is null) return;
        bar.MaxValue = max > 0 ? max : 1;
        bar.Value = current;
    }

    private Button MakeSlotButton(int size, string hotkey, string icon)
    {
        var btn = new Button
        {
            Text = $"{icon}\n{hotkey}",
            CustomMinimumSize = new Vector2(size, size),
            FocusMode = Control.FocusModeEnum.None,
        };
        var style = new StyleBoxFlat
        {
            BgColor = SlotBg,
            BorderColor = SlotBorder,
            BorderWidthTop = 1, BorderWidthBottom = 1,
            BorderWidthLeft = 1, BorderWidthRight = 1,
            CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
            ContentMarginTop = 3, ContentMarginBottom = 3,
            ContentMarginLeft = 2, ContentMarginRight = 2,
        };
        btn.AddThemeStyleboxOverride("normal", style);
        btn.AddThemeFontSizeOverride("font_size", ArkTheme.ScaledFontSize(9));
        btn.AddThemeColorOverride("font_color", TextWhite);
        return btn;
    }

    private static Label MakeLabel(string text, int fontSize, Color color)
    {
        var lbl = new Label { Text = text };
        lbl.AddThemeFontSizeOverride("font_size", ArkTheme.ScaledFontSize(fontSize));
        lbl.AddThemeColorOverride("font_color", color);
        return lbl;
    }

    private static ProgressBar MakeBar(Color fill, Color bg, int width, int height)
    {
        var bar = new ProgressBar { MaxValue = 100, Value = 100, ShowPercentage = false, CustomMinimumSize = new Vector2(width, height) };
        bar.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = bg, CornerRadiusTopLeft = 2, CornerRadiusTopRight = 2, CornerRadiusBottomLeft = 2, CornerRadiusBottomRight = 2 });
        bar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = fill, CornerRadiusTopLeft = 2, CornerRadiusTopRight = 2, CornerRadiusBottomLeft = 2, CornerRadiusBottomRight = 2 });
        return bar;
    }

    private static StyleBoxFlat MakeHoloPanel() => new()
    {
        BgColor = PanelBg,
        BorderColor = new Color(0.15f, 0.4f, 0.6f, 0.5f),
        BorderWidthTop = 1, BorderWidthBottom = 1,
        BorderWidthLeft = 1, BorderWidthRight = 1,
        CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
        CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
        ContentMarginLeft = 8, ContentMarginRight = 8,
        ContentMarginTop = 6, ContentMarginBottom = 6,
    };
}
