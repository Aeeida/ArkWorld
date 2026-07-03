using Godot;
using System;
using System.Collections.Generic;
using Ark.Shared.Data;

namespace Ark.UI;

/// <summary>
/// 战斗模式 HUD — 人员 / 载具战斗。
///
/// 布局（参考 WoW 经典战斗 HUD + 星际公民）：
///   中心/顶部：目标框（Target Frame）
///   底部：多层动作栏（主技能 + 辅助栏 + 载具专用栏）
///   左侧：小队/团队框架（Party/Raid Frames）
///   右侧：威胁列表、冷却覆盖
///   载具专用叠加：速度表、弹药/燃料条、瞄准准星
///
/// 风格：高对比、警报色（红/橙威胁、绿/蓝盟友），动态动画。
/// </summary>
public partial class CombatModeHud : CanvasLayer
{
    // ─── 颜色主题（战斗高对比） ───
    private static readonly Color PanelBg      = new(0.06f, 0.06f, 0.10f, 0.85f);
    private static readonly Color EnemyRed     = new(0.9f, 0.2f, 0.15f);
    private static readonly Color FriendlyGreen = new(0.2f, 0.75f, 0.3f);
    private static readonly Color AllyBlue     = new(0.3f, 0.55f, 0.9f);
    private static readonly Color WarningOrange = new(0.95f, 0.6f, 0.1f);
    private static readonly Color TextWhite    = new(0.92f, 0.92f, 0.95f);
    private static readonly Color DimText      = new(0.55f, 0.55f, 0.6f);
    private static readonly Color SlotBg       = new(0.10f, 0.10f, 0.14f, 0.9f);
    private static readonly Color SlotBorder   = new(0.35f, 0.35f, 0.45f, 0.7f);

    // ─── 目标框 ───
    private PanelContainer? _targetPanel;
    private Label? _targetName;
    private ProgressBar? _targetHealthBar;
    private Label? _targetDistance;
    private Label? _targetFaction;

    // ─── 玩家生命/护盾/能量 ───
    private ProgressBar? _playerHealthBar;
    private ProgressBar? _playerShieldBar;
    private ProgressBar? _playerEnergyBar;
    private Label? _playerNameLabel;

    // ─── 多层动作栏 ───
    private HBoxContainer? _primaryBar;
    private HBoxContainer? _secondaryBar;
    private HBoxContainer? _vehicleBar;
    private readonly List<Button> _primarySlots = [];
    private readonly List<Button> _secondarySlots = [];
    private readonly List<Button> _vehicleSlots = [];
    private readonly List<string> _primaryAbilityIds = [];
    private readonly List<string> _secondaryAbilityIds = [];
    private readonly List<string> _vehicleAbilityIds = [];

    // ─── 小队框架 ───
    private VBoxContainer? _partyFrames;
    private readonly List<PartyFrameWidget> _partyWidgets = [];

    // ─── 威胁列表 ───
    private VBoxContainer? _threatList;

    // ─── 载具覆盖 ───
    private PanelContainer? _vehicleOverlay;
    private Label? _vehicleSpeedLabel;
    private ProgressBar? _vehicleHealthBar;
    private ProgressBar? _vehicleFuelBar;
    private Label? _vehicleAmmoLabel;
    private bool _vehicleMode;

    public event Action<int, string>? OnPrimarySlotActivated;
    public event Action<int, string>? OnSecondarySlotActivated;
    public event Action<int, string>? OnVehicleSlotActivated;

    public override void _Ready()
    {
        Layer = 8;
        BuildPlayerFrame();
        BuildTargetFrame();
        BuildActionBars();
        BuildPartyFrames();
        BuildThreatList();
        BuildVehicleOverlay();
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                   中心偏上：玩家生命/护盾/能量
    // ═══════════════════════════════════════════════════════════════════════

    private void BuildPlayerFrame()
    {
        var panel = new PanelContainer();
        panel.AnchorLeft = 0.5f; panel.AnchorRight = 0.5f;
        panel.AnchorTop = 0f; panel.AnchorBottom = 0f;
        panel.GrowHorizontal = Control.GrowDirection.Both;
        panel.OffsetLeft = -180; panel.OffsetRight = -10;
        panel.OffsetTop = 8; panel.OffsetBottom = 80;
        panel.AddThemeStyleboxOverride("panel", MakePanel(PanelBg));
        AddChild(panel);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 2);
        panel.AddChild(vbox);

        _playerNameLabel = MakeLabel("玩家", 12, TextWhite);
        vbox.AddChild(_playerNameLabel);

        _playerHealthBar = MakeBar(FriendlyGreen, new Color(0.15f, 0.2f, 0.15f), 160, 16);
        vbox.AddChild(_playerHealthBar);

        _playerShieldBar = MakeBar(AllyBlue, new Color(0.1f, 0.15f, 0.25f), 160, 10);
        vbox.AddChild(_playerShieldBar);

        _playerEnergyBar = MakeBar(WarningOrange, new Color(0.2f, 0.15f, 0.08f), 160, 10);
        vbox.AddChild(_playerEnergyBar);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                   中心偏上右侧：目标框
    // ═══════════════════════════════════════════════════════════════════════

    private void BuildTargetFrame()
    {
        _targetPanel = new PanelContainer();
        _targetPanel.AnchorLeft = 0.5f; _targetPanel.AnchorRight = 0.5f;
        _targetPanel.AnchorTop = 0f; _targetPanel.AnchorBottom = 0f;
        _targetPanel.GrowHorizontal = Control.GrowDirection.Both;
        _targetPanel.OffsetLeft = 10; _targetPanel.OffsetRight = 180;
        _targetPanel.OffsetTop = 8; _targetPanel.OffsetBottom = 80;
        _targetPanel.AddThemeStyleboxOverride("panel", MakePanel(PanelBg));
        _targetPanel.Visible = false;
        AddChild(_targetPanel);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 2);
        _targetPanel.AddChild(vbox);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 6);
        vbox.AddChild(header);

        _targetName = MakeLabel("目标", 12, EnemyRed);
        header.AddChild(_targetName);

        _targetFaction = MakeLabel("", 10, DimText);
        header.AddChild(_targetFaction);

        _targetHealthBar = MakeBar(EnemyRed, new Color(0.25f, 0.08f, 0.08f), 160, 16);
        vbox.AddChild(_targetHealthBar);

        _targetDistance = MakeLabel("0m", 10, DimText);
        vbox.AddChild(_targetDistance);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                   底部：多层动作栏
    // ═══════════════════════════════════════════════════════════════════════

    private void BuildActionBars()
    {
        int slotSize = 48;
        int spacing = 3;

        // 主技能栏（1-0，10 个槽位）
        _primaryBar = MakeActionBarRow(10, slotSize, spacing, -80, _primarySlots, _primaryAbilityIds,
            (i, id) => OnPrimarySlotActivated?.Invoke(i, id));

        // 辅助栏（Shift+1-4，4 个槽位）
        _secondaryBar = MakeActionBarRow(4, slotSize, spacing, -130, _secondarySlots, _secondaryAbilityIds,
            (i, id) => OnSecondarySlotActivated?.Invoke(i, id));

        // 载具专用栏（仅载具模式可见）
        _vehicleBar = MakeActionBarRow(4, slotSize, spacing, -180, _vehicleSlots, _vehicleAbilityIds,
            (i, id) => OnVehicleSlotActivated?.Invoke(i, id));
        _vehicleBar.Visible = false;
    }

    private HBoxContainer MakeActionBarRow(int count, int slotSize, int spacing, int offsetTop,
        List<Button> buttons, List<string> abilityIds, Action<int, string> onActivated)
    {
        var barPanel = new PanelContainer();
        barPanel.AnchorLeft = 0.5f; barPanel.AnchorRight = 0.5f;
        barPanel.AnchorTop = 1f; barPanel.AnchorBottom = 1f;
        barPanel.GrowHorizontal = Control.GrowDirection.Both;
        barPanel.GrowVertical = Control.GrowDirection.Begin;
        int totalW = count * slotSize + (count - 1) * spacing + 12;
        barPanel.OffsetLeft = -(totalW / 2); barPanel.OffsetRight = (totalW / 2);
        barPanel.OffsetTop = offsetTop; barPanel.OffsetBottom = offsetTop + slotSize + 12;
        barPanel.AddThemeStyleboxOverride("panel", MakePanel(new Color(0.04f, 0.04f, 0.08f, 0.7f)));
        AddChild(barPanel);

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", spacing);
        hbox.Alignment = BoxContainer.AlignmentMode.Center;
        barPanel.AddChild(hbox);

        for (int i = 0; i < count; i++)
        {
            var btn = MakeSlotButton(slotSize, $"{(i + 1) % 10}", "—");
            hbox.AddChild(btn);
            buttons.Add(btn);
            abilityIds.Add("");
            int idx = i;
            btn.Pressed += () =>
            {
                if (idx < abilityIds.Count && !string.IsNullOrEmpty(abilityIds[idx]))
                    onActivated(idx, abilityIds[idx]);
            };
        }

        return hbox;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                   左侧：小队/团队框架
    // ═══════════════════════════════════════════════════════════════════════

    private void BuildPartyFrames()
    {
        var panel = new PanelContainer();
        panel.AnchorLeft = 0f; panel.AnchorRight = 0f;
        panel.AnchorTop = 0.12f; panel.AnchorBottom = 0.12f;
        panel.GrowVertical = Control.GrowDirection.End;
        panel.OffsetLeft = 8; panel.OffsetRight = 170;
        panel.OffsetTop = 0; panel.OffsetBottom = 260;
        panel.AddThemeStyleboxOverride("panel", MakePanel(new Color(0.05f, 0.05f, 0.08f, 0.6f)));
        AddChild(panel);

        _partyFrames = new VBoxContainer();
        _partyFrames.AddThemeConstantOverride("separation", 4);
        panel.AddChild(_partyFrames);

        var header = MakeLabel("👥 小队", 11, AllyBlue);
        _partyFrames.AddChild(header);

        // 预创建 5 个框架
        for (int i = 0; i < 5; i++)
        {
            var widget = new PartyFrameWidget();
            _partyFrames.AddChild(widget);
            _partyWidgets.Add(widget);
            widget.Visible = false;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                   右侧：威胁列表
    // ═══════════════════════════════════════════════════════════════════════

    private void BuildThreatList()
    {
        var panel = new PanelContainer();
        panel.AnchorLeft = 1f; panel.AnchorRight = 1f;
        panel.AnchorTop = 0.3f; panel.AnchorBottom = 0.3f;
        panel.GrowVertical = Control.GrowDirection.End;
        panel.OffsetLeft = -170; panel.OffsetRight = -8;
        panel.OffsetTop = 0; panel.OffsetBottom = 160;
        panel.AddThemeStyleboxOverride("panel", MakePanel(new Color(0.05f, 0.05f, 0.08f, 0.5f)));
        AddChild(panel);

        _threatList = new VBoxContainer();
        _threatList.AddThemeConstantOverride("separation", 2);
        panel.AddChild(_threatList);

        var header = MakeLabel("⚠ 威胁", 11, WarningOrange);
        _threatList.AddChild(header);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                   载具覆盖 HUD
    // ═══════════════════════════════════════════════════════════════════════

    private void BuildVehicleOverlay()
    {
        _vehicleOverlay = new PanelContainer();
        _vehicleOverlay.AnchorLeft = 0f; _vehicleOverlay.AnchorRight = 0f;
        _vehicleOverlay.AnchorTop = 1f; _vehicleOverlay.AnchorBottom = 1f;
        _vehicleOverlay.GrowVertical = Control.GrowDirection.Begin;
        _vehicleOverlay.OffsetLeft = 8; _vehicleOverlay.OffsetRight = 220;
        _vehicleOverlay.OffsetTop = -180; _vehicleOverlay.OffsetBottom = -8;
        _vehicleOverlay.AddThemeStyleboxOverride("panel", MakePanel(new Color(0.08f, 0.08f, 0.12f, 0.9f)));
        _vehicleOverlay.Visible = false;
        AddChild(_vehicleOverlay);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 4);
        _vehicleOverlay.AddChild(vbox);

        var title = MakeLabel("🚗 载具", 12, WarningOrange);
        vbox.AddChild(title);

        _vehicleSpeedLabel = MakeLabel("速度: 0 km/h", 11, TextWhite);
        vbox.AddChild(_vehicleSpeedLabel);

        vbox.AddChild(MakeLabel("生命", 9, DimText));
        _vehicleHealthBar = MakeBar(FriendlyGreen, new Color(0.15f, 0.2f, 0.15f), 190, 12);
        vbox.AddChild(_vehicleHealthBar);

        vbox.AddChild(MakeLabel("燃油", 9, DimText));
        _vehicleFuelBar = MakeBar(WarningOrange, new Color(0.2f, 0.15f, 0.08f), 190, 10);
        vbox.AddChild(_vehicleFuelBar);

        _vehicleAmmoLabel = MakeLabel("弹药: 0 / 0", 11, TextWhite);
        vbox.AddChild(_vehicleAmmoLabel);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                          公开 API
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>更新玩家框。</summary>
    public void UpdatePlayerFrame(string name, float hp, float maxHp, float shield, float maxShield, float energy, float maxEnergy)
    {
        if (_playerNameLabel is not null) _playerNameLabel.Text = name;
        SetBar(_playerHealthBar, hp, maxHp);
        SetBar(_playerShieldBar, shield, maxShield);
        SetBar(_playerEnergyBar, energy, maxEnergy);
    }

    /// <summary>显示/更新目标框。</summary>
    public void UpdateTargetFrame(string name, float hp, float maxHp, float distance, string faction)
    {
        if (_targetPanel is not null) _targetPanel.Visible = true;
        if (_targetName is not null) _targetName.Text = name;
        SetBar(_targetHealthBar, hp, maxHp);
        if (_targetDistance is not null) _targetDistance.Text = $"{distance:F0}m";
        if (_targetFaction is not null) _targetFaction.Text = faction;

        // 敌/友颜色切换
        var col = faction switch
        {
            "Hostile" => EnemyRed,
            "Friendly" => FriendlyGreen,
            _ => DimText
        };
        _targetName?.AddThemeColorOverride("font_color", col);
    }

    /// <summary>隐藏目标框。</summary>
    public void ClearTarget()
    {
        if (_targetPanel is not null) _targetPanel.Visible = false;
    }

    /// <summary>设置主技能栏。</summary>
    public void SetPrimaryBar(IReadOnlyList<ClientActionBarSlot> slots, ClientAbilityDefRegistry? registry)
        => FillBar(_primarySlots, _primaryAbilityIds, slots, registry);

    /// <summary>设置辅助栏。</summary>
    public void SetSecondaryBar(IReadOnlyList<ClientActionBarSlot> slots, ClientAbilityDefRegistry? registry)
        => FillBar(_secondarySlots, _secondaryAbilityIds, slots, registry);

    /// <summary>设置载具栏。</summary>
    public void SetVehicleBar(IReadOnlyList<ClientActionBarSlot> slots, ClientAbilityDefRegistry? registry)
    {
        FillBar(_vehicleSlots, _vehicleAbilityIds, slots, registry);
        if (_vehicleBar is not null) _vehicleBar.Visible = slots.Count > 0;
    }

    /// <summary>更新小队成员。</summary>
    public void UpdatePartyMember(int index, string name, float hp, float maxHp, bool online)
    {
        if (index < 0 || index >= _partyWidgets.Count) return;
        _partyWidgets[index].Visible = true;
        _partyWidgets[index].Update(name, hp, maxHp, online);
    }

    /// <summary>清空小队框。</summary>
    public void ClearPartyFrames()
    {
        foreach (var w in _partyWidgets) w.Visible = false;
    }

    /// <summary>切换载具模式。</summary>
    public void SetVehicleMode(bool active)
    {
        _vehicleMode = active;
        if (_vehicleOverlay is not null) _vehicleOverlay.Visible = active;
        if (_vehicleBar is not null) _vehicleBar.Visible = active;
    }

    /// <summary>更新载具覆盖数据。</summary>
    public void UpdateVehicleOverlay(float speed, float hp, float maxHp, float fuel, float maxFuel, int ammo, int maxAmmo)
    {
        if (_vehicleSpeedLabel is not null) _vehicleSpeedLabel.Text = $"速度: {speed:F0} km/h";
        SetBar(_vehicleHealthBar, hp, maxHp);
        SetBar(_vehicleFuelBar, fuel, maxFuel);
        if (_vehicleAmmoLabel is not null) _vehicleAmmoLabel.Text = $"弹药: {ammo} / {maxAmmo}";
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                          辅助
    // ═══════════════════════════════════════════════════════════════════════

    private static void FillBar(List<Button> buttons, List<string> ids,
        IReadOnlyList<ClientActionBarSlot> slots, ClientAbilityDefRegistry? registry)
    {
        for (int i = 0; i < buttons.Count && i < slots.Count; i++)
        {
            var slot = slots[i];
            var def = registry?.Get(slot.AbilityId);
            buttons[i].Text = def is not null ? $"{def.Value.Icon}\n{slot.Hotkey}" : $"—\n{slot.Hotkey}";
            buttons[i].TooltipText = def?.Description ?? "";
            if (i < ids.Count) ids[i] = slot.AbilityId;
            else ids.Add(slot.AbilityId);
        }
    }

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
            CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3,
            CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3,
            ContentMarginTop = 2, ContentMarginBottom = 2,
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

    private static StyleBoxFlat MakePanel(Color bg) => new()
    {
        BgColor = bg,
        BorderColor = new Color(0.3f, 0.3f, 0.4f, 0.5f),
        BorderWidthTop = 1, BorderWidthBottom = 1,
        BorderWidthLeft = 1, BorderWidthRight = 1,
        CornerRadiusTopLeft = 5, CornerRadiusTopRight = 5,
        CornerRadiusBottomLeft = 5, CornerRadiusBottomRight = 5,
        ContentMarginLeft = 6, ContentMarginRight = 6,
        ContentMarginTop = 4, ContentMarginBottom = 4,
    };
}

// ═══════════════════════════════════════════════════════════════════════════════
//                   小队成员子组件
// ═══════════════════════════════════════════════════════════════════════════════

public partial class PartyFrameWidget : VBoxContainer
{
    private Label? _nameLabel;
    private ProgressBar? _hpBar;

    public override void _Ready()
    {
        AddThemeConstantOverride("separation", 1);

        _nameLabel = new Label();
        _nameLabel.AddThemeFontSizeOverride("font_size", ArkTheme.ScaledFontSize(10));
        _nameLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.85f, 0.9f));
        AddChild(_nameLabel);

        _hpBar = new ProgressBar { MaxValue = 100, Value = 100, ShowPercentage = false, CustomMinimumSize = new Vector2(140, 10) };
        _hpBar.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = new Color(0.15f, 0.15f, 0.2f), CornerRadiusTopLeft = 2, CornerRadiusTopRight = 2, CornerRadiusBottomLeft = 2, CornerRadiusBottomRight = 2 });
        _hpBar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = new Color(0.2f, 0.7f, 0.3f), CornerRadiusTopLeft = 2, CornerRadiusTopRight = 2, CornerRadiusBottomLeft = 2, CornerRadiusBottomRight = 2 });
        AddChild(_hpBar);
    }

    public void Update(string name, float hp, float maxHp, bool online)
    {
        if (_nameLabel is not null)
        {
            _nameLabel.Text = online ? name : $"[离线] {name}";
            _nameLabel.AddThemeColorOverride("font_color",
                online ? new Color(0.8f, 0.85f, 0.9f) : new Color(0.45f, 0.45f, 0.5f));
        }
        if (_hpBar is not null) { _hpBar.MaxValue = maxHp > 0 ? maxHp : 1; _hpBar.Value = hp; }
    }
}
