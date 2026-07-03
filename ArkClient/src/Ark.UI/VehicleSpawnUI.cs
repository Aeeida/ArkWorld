using Godot;
using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Ark.Bridge.Features.BaseBuilding;
using Ark.Ecs.Components;
using Ark.Ecs.Tags;

namespace Ark.UI;

/// <summary>
/// 载具生成 UI — 选中已完工的坦克工厂后显示载具类型列表。
/// 点击按钮在工厂附近生成对应载具。
///
/// 使用方式：
///   1. GameBootstrap.Initialize 注入依赖
///   2. 外部调用 ShowForFactory(entityId, worldPos) 打开
///   3. 选择载具类型后通过 OnVehicleSpawnRequested 事件通知
/// </summary>
public partial class VehicleSpawnUI : CanvasLayer
{
    // ═══ 载具类型配置 ═══
    private static readonly (int DefId, string Name, string Icon)[] VehicleTypes =
    [
        (1, "越野车",   "🚗"),
        (2, "主战坦克", "🛡️"),
        (3, "防空炮",   "🎯"),
        (4, "战斗机",   "✈️"),
        (5, "巡逻艇",   "🚤"),
    ];

    // ═══ 节点 ═══
    private PanelContainer? _panel;
    private VBoxContainer? _btnBox;
    private Label? _titleLabel;

    // ═══ 状态 ═══
    private bool _visible;
    private int _factoryEntityId;
    private Godot.Vector3 _factoryWorldPos;

    // 每个工厂的已生成载具计数（用于环形分布）
    private readonly Dictionary<int, int> _factorySpawnCount = new();

    // ═══ 外部依赖（由 Initialize 注入）═══
    private EntityStore? _store;
    private BuildingVisualManager? _buildingVisuals;

    /// <summary>请求生成载具：(vehicleDefId, spawnPosition)</summary>
    public event Action<int, System.Numerics.Vector3>? OnVehicleSpawnRequested;

    public override void _Ready()
    {
        Layer = 6;
        BuildUI();
        HidePanel();
    }

    /// <summary>
    /// 注入依赖。
    /// </summary>
    public void Initialize(EntityStore store, BuildingVisualManager visuals)
    {
        _store = store;
        _buildingVisuals = visuals;
    }

    private void BuildUI()
    {
        _panel = new PanelContainer { Name = "VehiclePanel" };
        _panel.AnchorLeft = 0.5f;
        _panel.AnchorRight = 0.5f;
        _panel.AnchorTop = 0.5f;
        _panel.AnchorBottom = 0.5f;
        _panel.OffsetLeft = -130;
        _panel.OffsetRight = 130;
        _panel.OffsetTop = -160;
        _panel.OffsetBottom = 160;

        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.10f, 0.12f, 0.94f),
            BorderColor = new Color(0.5f, 0.6f, 0.4f, 1f),
            BorderWidthLeft = 2, BorderWidthRight = 2,
            BorderWidthTop = 2, BorderWidthBottom = 2,
            CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
            ContentMarginLeft = 10, ContentMarginRight = 10,
            ContentMarginTop = 10, ContentMarginBottom = 10,
        };
        _panel.AddThemeStyleboxOverride("panel", style);
        AddChild(_panel);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 6);
        _panel.AddChild(vbox);

        _titleLabel = new Label
        {
            Text = "🏭 载具工厂",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _titleLabel.AddThemeFontSizeOverride("font_size", ArkTheme.ScaledFontSize(16));
        _titleLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.85f, 0.5f));
        vbox.AddChild(_titleLabel);

        var sep = new HSeparator();
        sep.AddThemeColorOverride("color", new Color(0.4f, 0.4f, 0.4f));
        vbox.AddChild(sep);

        _btnBox = new VBoxContainer();
        _btnBox.AddThemeConstantOverride("separation", 4);
        vbox.AddChild(_btnBox);

        foreach (var (defId, name, icon) in VehicleTypes)
        {
            var btn = new Button
            {
                Text = $"{icon}  {name}",
                CustomMinimumSize = new Godot.Vector2(0, 44),
            };
            btn.AddThemeFontSizeOverride("font_size", ArkTheme.ScaledFontSize(14));
            int capturedId = defId;
            btn.Pressed += () => OnVehicleSelected(capturedId);
            _btnBox.AddChild(btn);
        }

        var sep2 = new HSeparator();
        sep2.AddThemeColorOverride("color", new Color(0.4f, 0.4f, 0.4f));
        vbox.AddChild(sep2);

        var closeBtn = new Button
        {
            Text = "✖  关闭",
            CustomMinimumSize = new Godot.Vector2(0, 34),
        };
        closeBtn.AddThemeColorOverride("font_color", new Color(1f, 0.4f, 0.4f));
        closeBtn.Pressed += HidePanel;
        vbox.AddChild(closeBtn);
    }

    private void OnVehicleSelected(int vehicleDefId)
    {
        // 获取该工厂的当前生成计数
        _factorySpawnCount.TryGetValue(_factoryEntityId, out int count);

        // 环形分布：8m 半径起步，每 8 辆一圈后扩大半径
        int perRing = 8;
        int ring = count / perRing;
        int indexInRing = count % perRing;
        float radius = 10f + ring * 8f;
        float angle = indexInRing * (2f * MathF.PI / perRing) + ring * 0.5f; // 每圈错开

        var spawnPos = new System.Numerics.Vector3(
            _factoryWorldPos.X + MathF.Cos(angle) * radius,
            _factoryWorldPos.Y,
            _factoryWorldPos.Z + MathF.Sin(angle) * radius);

        _factorySpawnCount[_factoryEntityId] = count + 1;

        OnVehicleSpawnRequested?.Invoke(vehicleDefId, spawnPos);
        GD.Print($"[VehicleSpawnUI] Requested vehicle {vehicleDefId} at ({spawnPos.X:F1}, {spawnPos.Z:F1}) [slot {count}]");
        HidePanel();
    }

    /// <summary>
    /// 为指定工厂显示载具选择面板。
    /// </summary>
    public void ShowForFactory(int factoryEntityId, Godot.Vector3 worldPos)
    {
        _factoryEntityId = factoryEntityId;
        _factoryWorldPos = worldPos;
        _visible = true;
        Show();

        // 显示鼠标
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    public void HidePanel()
    {
        _visible = false;
        Hide();
    }

    public bool IsOpen => _visible;

    public override void _Input(InputEvent @event)
    {
        if (!_visible) return;

        // ESC 关闭面板
        if (@event.IsActionPressed("ui_cancel"))
        {
            HidePanel();
            GetViewport().SetInputAsHandled();
        }
    }
}
