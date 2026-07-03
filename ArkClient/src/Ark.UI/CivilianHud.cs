using Godot;
using System;
using System.Collections.Generic;
using Ark.Shared.Data;

namespace Ark.UI;

/// <summary>
/// 平民模式 HUD — 和平探索、采集、贸易、社会互动。
///
/// 布局：
///   底部：主动作栏（1-0 键，非战斗技能）
///   左侧：小地图指示 + 任务追踪器
///   右侧：聊天/社交
///   顶部：角色信息栏（生命/能量/等级/名称）
///
/// 风格：简洁清新，半透明玻璃 HUD，柔和蓝绿色调。
/// </summary>
public partial class CivilianHud : CanvasLayer
{
    // ─── 颜色主题（柔和蓝绿） ───
    private static readonly Color PanelBg      = new(0.05f, 0.08f, 0.12f, 0.75f);
    private static readonly Color AccentColor  = new(0.3f, 0.75f, 0.7f);
    private static readonly Color TextColor    = new(0.85f, 0.9f, 0.92f);
    private static readonly Color DimText      = new(0.55f, 0.6f, 0.65f);
    private static readonly Color SlotBg       = new(0.08f, 0.12f, 0.16f, 0.85f);
    private static readonly Color SlotBorder   = new(0.25f, 0.5f, 0.48f, 0.6f);

    // ─── 动作栏 ───
    private HBoxContainer? _actionBar;
    private readonly List<Button> _slotButtons = [];
    private readonly List<string> _slotAbilityIds = [];

    // ─── 角色信息 ───
    private Label? _nameLabel;
    private ProgressBar? _healthBar;
    private ProgressBar? _energyBar;
    private Label? _levelLabel;

    // ─── 任务追踪 ───
    private VBoxContainer? _questTracker;
    private Label? _questTitle;
    private Label? _questObjective;

    // ─── Buff/状态 ───
    private HBoxContainer? _buffBar;

    // ─── 导航 ───
    private Label? _locationLabel;

    public event Action<int, string>? OnSlotActivated; // slotIndex, abilityId

    public override void _Ready()
    {
        Layer = 7;
        BuildCharacterInfoBar();
        BuildActionBar();
        BuildQuestTracker();
        BuildLocationIndicator();
        BuildBuffBar();
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                          顶部：角色信息栏
    // ═══════════════════════════════════════════════════════════════════════

    private void BuildCharacterInfoBar()
    {
        var panel = new PanelContainer();
        panel.AnchorLeft = 0.5f; panel.AnchorRight = 0.5f;
        panel.AnchorTop = 0f; panel.AnchorBottom = 0f;
        panel.GrowHorizontal = Control.GrowDirection.Both;
        panel.OffsetLeft = -160; panel.OffsetRight = 160;
        panel.OffsetTop = 8; panel.OffsetBottom = 70;
        panel.AddThemeStyleboxOverride("panel", MakeGlassPanel());
        AddChild(panel);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 2);
        panel.AddChild(vbox);

        // 名称 + 等级
        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 8);
        vbox.AddChild(hbox);

        _nameLabel = MakeLabel("玩家名称", 13, TextColor);
        hbox.AddChild(_nameLabel);
        _levelLabel = MakeLabel("Lv.1", 12, AccentColor);
        hbox.AddChild(_levelLabel);

        // 生命条
        _healthBar = MakeProgressBar(new Color(0.3f, 0.7f, 0.35f), new Color(0.12f, 0.2f, 0.12f));
        _healthBar.CustomMinimumSize = new Vector2(280, 14);
        vbox.AddChild(_healthBar);

        // 能量条
        _energyBar = MakeProgressBar(new Color(0.3f, 0.55f, 0.8f), new Color(0.1f, 0.15f, 0.25f));
        _energyBar.CustomMinimumSize = new Vector2(280, 10);
        vbox.AddChild(_energyBar);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                          底部：主动作栏
    // ═══════════════════════════════════════════════════════════════════════

    private void BuildActionBar()
    {
        var barPanel = new PanelContainer();
        barPanel.AnchorLeft = 0.5f; barPanel.AnchorRight = 0.5f;
        barPanel.AnchorTop = 1f; barPanel.AnchorBottom = 1f;
        barPanel.GrowHorizontal = Control.GrowDirection.Both;
        barPanel.GrowVertical = Control.GrowDirection.Begin;
        int slotSize = 52;
        int slotCount = 10;
        int spacing = 4;
        int totalW = slotCount * slotSize + (slotCount - 1) * spacing + 16;
        barPanel.OffsetLeft = -(totalW / 2); barPanel.OffsetRight = (totalW / 2);
        barPanel.OffsetTop = -74; barPanel.OffsetBottom = -8;
        barPanel.AddThemeStyleboxOverride("panel", MakeGlassPanel());
        AddChild(barPanel);

        _actionBar = new HBoxContainer();
        _actionBar.AddThemeConstantOverride("separation", spacing);
        _actionBar.Alignment = BoxContainer.AlignmentMode.Center;
        barPanel.AddChild(_actionBar);

        for (int i = 0; i < slotCount; i++)
        {
            var slot = MakeSlotButton(i, $"{(i + 1) % 10}", "—");
            _actionBar.AddChild(slot);
            _slotButtons.Add(slot);
            _slotAbilityIds.Add("");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                      左侧：任务追踪器
    // ═══════════════════════════════════════════════════════════════════════

    private void BuildQuestTracker()
    {
        var panel = new PanelContainer();
        panel.AnchorLeft = 0f; panel.AnchorRight = 0f;
        panel.AnchorTop = 0.15f; panel.AnchorBottom = 0.15f;
        panel.GrowVertical = Control.GrowDirection.End;
        panel.OffsetLeft = 8; panel.OffsetRight = 200;
        panel.OffsetTop = 0; panel.OffsetBottom = 120;
        panel.AddThemeStyleboxOverride("panel", MakeGlassPanel());
        AddChild(panel);

        _questTracker = new VBoxContainer();
        _questTracker.AddThemeConstantOverride("separation", 4);
        panel.AddChild(_questTracker);

        var header = MakeLabel("📋 任务追踪", 12, AccentColor);
        _questTracker.AddChild(header);

        _questTitle = MakeLabel("暂无活跃任务", 11, TextColor);
        _questTracker.AddChild(_questTitle);

        _questObjective = MakeLabel("", 10, DimText);
        _questObjective.AutowrapMode = TextServer.AutowrapMode.Word;
        _questTracker.AddChild(_questObjective);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                      顶部：位置指示器
    // ═══════════════════════════════════════════════════════════════════════

    private void BuildLocationIndicator()
    {
        _locationLabel = MakeLabel("🧭 未知位置", 11, DimText);
        _locationLabel.AnchorLeft = 1f; _locationLabel.AnchorRight = 1f;
        _locationLabel.AnchorTop = 0f; _locationLabel.AnchorBottom = 0f;
        _locationLabel.OffsetLeft = -220; _locationLabel.OffsetRight = -8;
        _locationLabel.OffsetTop = 8; _locationLabel.OffsetBottom = 30;
        _locationLabel.HorizontalAlignment = HorizontalAlignment.Right;
        AddChild(_locationLabel);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                      Buff/Debuff 图标栏
    // ═══════════════════════════════════════════════════════════════════════

    private void BuildBuffBar()
    {
        _buffBar = new HBoxContainer();
        _buffBar.AnchorLeft = 0.5f; _buffBar.AnchorRight = 0.5f;
        _buffBar.AnchorTop = 0f; _buffBar.AnchorBottom = 0f;
        _buffBar.GrowHorizontal = Control.GrowDirection.Both;
        _buffBar.OffsetLeft = -120; _buffBar.OffsetRight = 120;
        _buffBar.OffsetTop = 76; _buffBar.OffsetBottom = 100;
        _buffBar.AddThemeConstantOverride("separation", 2);
        _buffBar.Alignment = BoxContainer.AlignmentMode.Center;
        AddChild(_buffBar);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                          公开 API
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>更新角色信息。</summary>
    public void UpdateCharacterInfo(string name, int level, float hp, float maxHp, float energy, float maxEnergy)
    {
        if (_nameLabel is not null) _nameLabel.Text = name;
        if (_levelLabel is not null) _levelLabel.Text = $"Lv.{level}";
        if (_healthBar is not null) { _healthBar.MaxValue = maxHp; _healthBar.Value = hp; }
        if (_energyBar is not null) { _energyBar.MaxValue = maxEnergy; _energyBar.Value = energy; }
    }

    /// <summary>设置动作栏槽位。</summary>
    public void SetActionBarSlots(IReadOnlyList<ClientActionBarSlot> slots, ClientAbilityDefRegistry? registry)
    {
        for (int i = 0; i < _slotButtons.Count && i < slots.Count; i++)
        {
            var slot = slots[i];
            var def = registry?.Get(slot.AbilityId);
            string label = def is not null ? $"{def.Value.Icon}\n{slot.Hotkey}" : $"—\n{slot.Hotkey}";
            _slotButtons[i].Text = label;
            _slotButtons[i].TooltipText = def?.Description ?? "";
            if (i < _slotAbilityIds.Count)
                _slotAbilityIds[i] = slot.AbilityId;
            else
                _slotAbilityIds.Add(slot.AbilityId);
        }
    }

    /// <summary>更新任务追踪。</summary>
    public void UpdateQuestTracker(string questName, string objective)
    {
        if (_questTitle is not null) _questTitle.Text = questName;
        if (_questObjective is not null) _questObjective.Text = objective;
    }

    /// <summary>更新位置。</summary>
    public void UpdateLocation(string location)
    {
        if (_locationLabel is not null) _locationLabel.Text = $"🧭 {location}";
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                          辅助
    // ═══════════════════════════════════════════════════════════════════════

    private Button MakeSlotButton(int index, string hotkey, string icon)
    {
        var btn = new Button
        {
            Text = $"{icon}\n{hotkey}",
            CustomMinimumSize = new Vector2(52, 52),
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
            ContentMarginTop = 4, ContentMarginBottom = 4,
            ContentMarginLeft = 2, ContentMarginRight = 2,
        };
        btn.AddThemeStyleboxOverride("normal", style);
        btn.AddThemeFontSizeOverride("font_size", ArkTheme.ScaledFontSize(10));
        btn.AddThemeColorOverride("font_color", TextColor);
        int idx = index;
        btn.Pressed += () =>
        {
            if (idx < _slotAbilityIds.Count && !string.IsNullOrEmpty(_slotAbilityIds[idx]))
                OnSlotActivated?.Invoke(idx, _slotAbilityIds[idx]);
        };
        return btn;
    }

    private static Label MakeLabel(string text, int fontSize, Color color)
    {
        var lbl = new Label { Text = text };
        lbl.AddThemeFontSizeOverride("font_size", ArkTheme.ScaledFontSize(fontSize));
        lbl.AddThemeColorOverride("font_color", color);
        return lbl;
    }

    private static ProgressBar MakeProgressBar(Color fillColor, Color bgColor)
    {
        var bar = new ProgressBar
        {
            MaxValue = 100, Value = 100, ShowPercentage = false,
        };
        var bgStyle = new StyleBoxFlat { BgColor = bgColor, CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3, CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3 };
        var fillStyle = new StyleBoxFlat { BgColor = fillColor, CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3, CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3 };
        bar.AddThemeStyleboxOverride("background", bgStyle);
        bar.AddThemeStyleboxOverride("fill", fillStyle);
        return bar;
    }

    private static StyleBoxFlat MakeGlassPanel() => new()
    {
        BgColor = PanelBg,
        BorderColor = SlotBorder,
        BorderWidthTop = 1, BorderWidthBottom = 1,
        BorderWidthLeft = 1, BorderWidthRight = 1,
        CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6,
        CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
        ContentMarginLeft = 8, ContentMarginRight = 8,
        ContentMarginTop = 6, ContentMarginBottom = 6,
    };
}
