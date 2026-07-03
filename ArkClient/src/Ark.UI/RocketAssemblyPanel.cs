using Godot;
using System;

namespace Ark.UI;

/// <summary>
/// 火箭设计组装面板 (VAB) — 全屏界面。
///
/// 布局：
///   ┌─────────────┬──────────────────────────┬────────────────────┐
///   │  左侧分类    │     中央拼装设计区         │  右侧信息面板       │
///   │  配件列表    │     可视化火箭堆叠         │  级段/推力/燃油     │
///   │  (按分类)    │     (点击配件添加)         │  △V/乘员/物资      │
///   │             │                          │  问题检查           │
///   │             │                          │  [准备发射] 按钮    │
///   └─────────────┴──────────────────────────┴────────────────────┘
///
/// 进入时仅显示 UI 面板进行火箭设计，不修改周围环境。
/// </summary>
public partial class RocketAssemblyPanel : CanvasLayer
{
    // ── 节点 ──
    private PanelContainer? _root;
    private TabContainer?   _partsTabContainer;
    private VBoxContainer?  _assemblyArea;
    private VBoxContainer?  _statsPanel;
    private Label?          _vesselNameLabel;
    private Label?          _statsOverview;
    private Label?          _stagesDetail;
    private Label?          _issuesLabel;
    private Button?         _launchBtn;
    private Button?         _closeBtn;

    // ── 状态 ──
    private int     _padEntityId;
    private Vector3 _padWorldPos;
    private readonly RocketConfig _config = new();
    private bool _isOpen;

    /// <summary>面板是否打开。</summary>
    public bool IsOpen => _isOpen;

    /// <summary>用户点击「准备发射」(设计完成)。</summary>
    public event Action<int, Vector3, RocketConfig>? OnLaunchRequested;
    /// <summary>用户关闭面板。</summary>
    public event Action? OnPanelClosed;

    public override void _Ready()
    {
        Layer = 8;
        BuildUI();
        _root!.Visible = false;
    }

    /// <summary>为指定发射台打开设计面板（纯 UI，不改变环境）。</summary>
    public void ShowForPad(int padEntityId, Vector3 worldPos)
    {
        _padEntityId = padEntityId;
        _padWorldPos = worldPos;
        _config.InstalledPartIds.Clear();
        _config.Stages.Clear();
        _config.VesselName = "新火箭 #1";
        _isOpen = true;
        _root!.Visible = true;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        RefreshAll();
    }

    public new void Hide()
    {
        _isOpen = false;
        if (_root != null) _root.Visible = false;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                          UI 构建
    // ═══════════════════════════════════════════════════════════════════════

    private void BuildUI()
    {
        // ── 主面板（全屏减边距）──
        _root = new PanelContainer { Name = "VABRoot" };
        _root.AnchorRight  = 1;
        _root.AnchorBottom = 1;
        _root.OffsetLeft   = 20;
        _root.OffsetRight  = -20;
        _root.OffsetTop    = 50;
        _root.OffsetBottom = -20;
        _root.AddThemeStyleboxOverride("panel", MakeVABStyle());
        AddChild(_root);

        var outerVBox = new VBoxContainer();
        outerVBox.AddThemeConstantOverride("separation", 6);
        _root.AddChild(outerVBox);

        // ── 标题栏 ──
        BuildTitleBar(outerVBox);

        // ── 三栏主体 ──
        var mainHBox = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        mainHBox.AddThemeConstantOverride("separation", 8);
        outerVBox.AddChild(mainHBox);

        // 左栏：分类配件列表
        BuildPartsPanel(mainHBox);

        // 中栏：拼装设计区
        BuildAssemblyArea(mainHBox);

        // 右栏：信息面板
        BuildStatsPanel(mainHBox);
    }

    private void BuildTitleBar(VBoxContainer parent)
    {
        var titleBar = new HBoxContainer();
        parent.AddChild(titleBar);

        _vesselNameLabel = new Label
        {
            Text = "\U0001f680 火箭设计组装中心 — 新火箭 #1",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _vesselNameLabel.AddThemeFontSizeOverride("font_size", ArkTheme.ScaledFontSize(20));
        _vesselNameLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.7f, 1f));
        titleBar.AddChild(_vesselNameLabel);

        var clearBtn = new Button { Text = "\U0001f5d1 清空设计", FocusMode = Control.FocusModeEnum.None };
        clearBtn.Pressed += ClearDesign;
        titleBar.AddChild(clearBtn);

        _closeBtn = new Button { Text = "\u2716 退出", FocusMode = Control.FocusModeEnum.None };
        _closeBtn.Pressed += () => { Hide(); OnPanelClosed?.Invoke(); };
        titleBar.AddChild(_closeBtn);
    }

    private void BuildPartsPanel(HBoxContainer parent)
    {
        var leftPanel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(260, 0),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        leftPanel.AddThemeStyleboxOverride("panel", MakeSectionStyle(new Color(0.08f, 0.08f, 0.14f, 0.9f)));
        parent.AddChild(leftPanel);

        var leftVBox = new VBoxContainer();
        leftVBox.AddThemeConstantOverride("separation", 4);
        leftPanel.AddChild(leftVBox);

        var leftTitle = new Label { Text = "\U0001f9f0 配件仓库" };
        leftTitle.AddThemeFontSizeOverride("font_size", ArkTheme.ScaledFontSize(15));
        leftTitle.AddThemeColorOverride("font_color", new Color(0.9f, 0.8f, 0.3f));
        leftVBox.AddChild(leftTitle);

        _partsTabContainer = new TabContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            TabAlignment = TabBar.AlignmentMode.Left,
        };
        leftVBox.AddChild(_partsTabContainer);

        // 为每个分类创建一个选项卡
        foreach (var cat in RocketPartDef.Categories)
        {
            var scroll = new ScrollContainer
            {
                Name = RocketPartDef.CategoryDisplayName(cat),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical   = Control.SizeFlags.ExpandFill,
                HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            };

            var vbox = new VBoxContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            vbox.AddThemeConstantOverride("separation", 3);
            scroll.AddChild(vbox);

            foreach (var part in RocketPartDef.GetByCategory(cat))
            {
                var btn = new Button
                {
                    Text = $"{part.Icon} {part.Name} ({part.Mass:F1}t)",
                    FocusMode = Control.FocusModeEnum.None,
                    CustomMinimumSize = new Vector2(220, 30),
                    ClipText = true,
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                };
                btn.TooltipText = RocketConfig.GetPartTooltip(part);
                var cap = part;
                btn.Pressed += () => InstallPart(cap);
                vbox.AddChild(btn);
            }
            _partsTabContainer.AddChild(scroll);
        }
    }

    private void BuildAssemblyArea(HBoxContainer parent)
    {
        var centerPanel = new PanelContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        centerPanel.AddThemeStyleboxOverride("panel", MakeSectionStyle(new Color(0.05f, 0.06f, 0.1f, 0.9f)));
        parent.AddChild(centerPanel);

        var centerVBox = new VBoxContainer();
        centerVBox.AddThemeConstantOverride("separation", 4);
        centerPanel.AddChild(centerVBox);

        var centerTitle = new Label { Text = "\U0001f3d7 组装区 — 从左侧选择配件点击安装" };
        centerTitle.AddThemeFontSizeOverride("font_size", ArkTheme.ScaledFontSize(14));
        centerTitle.AddThemeColorOverride("font_color", new Color(0.6f, 0.8f, 0.6f));
        centerVBox.AddChild(centerTitle);

        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        centerVBox.AddChild(scroll);

        _assemblyArea = new VBoxContainer();
        _assemblyArea.AddThemeConstantOverride("separation", 2);
        scroll.AddChild(_assemblyArea);
    }

    private void BuildStatsPanel(HBoxContainer parent)
    {
        var rightPanel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(300, 0),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        rightPanel.AddThemeStyleboxOverride("panel", MakeSectionStyle(new Color(0.06f, 0.08f, 0.12f, 0.9f)));
        parent.AddChild(rightPanel);

        _statsPanel = new VBoxContainer();
        _statsPanel.AddThemeConstantOverride("separation", 8);
        rightPanel.AddChild(_statsPanel);

        // 总览
        var overviewTitle = new Label { Text = "\U0001f4ca 飞行器总览" };
        overviewTitle.AddThemeFontSizeOverride("font_size", ArkTheme.ScaledFontSize(15));
        overviewTitle.AddThemeColorOverride("font_color", new Color(0.4f, 0.8f, 1f));
        _statsPanel.AddChild(overviewTitle);

        _statsOverview = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _statsOverview.AddThemeFontSizeOverride("font_size", ArkTheme.ScaledFontSize(12));
        _statsPanel.AddChild(_statsOverview);

        // 级段详情
        var stagesTitle = new Label { Text = "\U0001f680 级段详情" };
        stagesTitle.AddThemeFontSizeOverride("font_size", ArkTheme.ScaledFontSize(14));
        stagesTitle.AddThemeColorOverride("font_color", new Color(0.8f, 0.6f, 1f));
        _statsPanel.AddChild(stagesTitle);

        _stagesDetail = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _stagesDetail.AddThemeFontSizeOverride("font_size", ArkTheme.ScaledFontSize(11));
        _statsPanel.AddChild(_stagesDetail);

        // 问题检查
        _issuesLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _issuesLabel.AddThemeFontSizeOverride("font_size", ArkTheme.ScaledFontSize(12));
        _statsPanel.AddChild(_issuesLabel);

        // 发射按钮
        _launchBtn = new Button
        {
            Text = "\U0001f680 准备发射",
            CustomMinimumSize = new Vector2(0, 50),
            FocusMode = Control.FocusModeEnum.None,
            Disabled = true,
        };
        _launchBtn.AddThemeFontSizeOverride("font_size", ArkTheme.ScaledFontSize(18));
        _launchBtn.Pressed += OnLaunchPressed;
        _statsPanel.AddChild(_launchBtn);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                          交互逻辑
    // ═══════════════════════════════════════════════════════════════════════

    private void InstallPart(RocketPartDef part)
    {
        _config.InstalledPartIds.Add(part.PartId);
        RefreshAll();
    }

    private void RemovePart(int index)
    {
        if (index >= 0 && index < _config.InstalledPartIds.Count)
        {
            _config.InstalledPartIds.RemoveAt(index);
            RefreshAll();
        }
    }

    private void ClearDesign()
    {
        _config.InstalledPartIds.Clear();
        _config.Stages.Clear();
        RefreshAll();
    }

    private void RefreshAll()
    {
        _config.RebuildStages();
        RefreshAssemblyView();
        RefreshStats();
    }

    private void RefreshAssemblyView()
    {
        if (_assemblyArea == null) return;
        foreach (var child in _assemblyArea.GetChildren())
            child.QueueFree();

        if (_config.InstalledPartIds.Count == 0)
        {
            var hint = new Label { Text = "\U0001f449 从左侧选择配件安装到火箭\n\n提示：从下到上安装\n引擎 → 燃料箱 → 分离器 → 燃料箱 → 指令舱" };
            hint.AddThemeFontSizeOverride("font_size", ArkTheme.ScaledFontSize(13));
            hint.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.6f));
            _assemblyArea.AddChild(hint);
            return;
        }

        // 显示为从底部到顶部的堆叠（反序显示，顶部=头锥在上方）
        int stageIdx = 0;
        for (int i = _config.InstalledPartIds.Count - 1; i >= 0; i--)
        {
            var part = RocketPartDef.Get(_config.InstalledPartIds[i]);
            if (part == null) continue;

            // 分离器分界线
            if (part.Decoupler && i < _config.InstalledPartIds.Count - 1)
            {
                stageIdx++;
                var sep = new HSeparator();
                _assemblyArea.AddChild(sep);
                var stageLabel = new Label { Text = $"── 第 {stageIdx} 级分离 ──", HorizontalAlignment = HorizontalAlignment.Center };
                stageLabel.AddThemeFontSizeOverride("font_size", ArkTheme.ScaledFontSize(10));
                stageLabel.AddThemeColorOverride("font_color", new Color(1f, 0.5f, 0.3f));
                _assemblyArea.AddChild(stageLabel);
            }

            var hbox = new HBoxContainer();
            hbox.AddThemeConstantOverride("separation", 4);

            // 配件颜色指示条
            var colorRect = new ColorRect
            {
                CustomMinimumSize = new Vector2(4, 28),
                Color = GetCategoryColor(part.Category),
            };
            hbox.AddChild(colorRect);

            var lbl = new Label
            {
                Text = $"{part.Icon} {part.Name}  [{part.Mass:F1}t]",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 28),
            };
            lbl.AddThemeFontSizeOverride("font_size", ArkTheme.ScaledFontSize(12));
            lbl.TooltipText = RocketConfig.GetPartTooltip(part);
            hbox.AddChild(lbl);

            // 质量/推力简要
            if (part.Thrust > 0)
            {
                var thrustLbl = new Label { Text = $"{part.Thrust:F0}kN" };
                thrustLbl.AddThemeFontSizeOverride("font_size", ArkTheme.ScaledFontSize(10));
                thrustLbl.AddThemeColorOverride("font_color", new Color(1f, 0.6f, 0.3f));
                hbox.AddChild(thrustLbl);
            }

            int capturedIndex = i;
            var removeBtn = new Button { Text = "\u2716", FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(28, 28) };
            removeBtn.Pressed += () => RemovePart(capturedIndex);
            hbox.AddChild(removeBtn);

            _assemblyArea.AddChild(hbox);
        }
    }

    private void RefreshStats()
    {
        if (_statsOverview == null || _stagesDetail == null || _issuesLabel == null || _launchBtn == null) return;

        // ── 总览 ──
        _statsOverview.Text =
            $"\U0001f3f7 名称: {_config.VesselName}\n" +
            $"\u2696  总质量: {_config.TotalMass:F2}t (干{_config.TotalDryMass:F2} + 燃{_config.TotalFuelMass:F2})\n" +
            $"\U0001f525 总推力: {_config.TotalThrust:F0}kN\n" +
            $"\u2696  推重比: {_config.ThrustToWeightRatio:F2}\n" +
            $"\u26a1 总 \u0394V: {_config.TotalDeltaV:F0} m/s\n" +
            $"\U0001f6e2 总燃料: {_config.TotalFuel:F0} 单位\n" +
            $"\U0001f4a8 总阻力: {_config.TotalDragCoefficient:F2}\n" +
            $"\U0001f468 乘员: {_config.TotalCrewCapacity}人  |  \U0001f4e6 物资: {_config.TotalCargoSlots}槽\n" +
            $"\U0001f680 级段数: {_config.Stages.Count}  |  配件: {_config.InstalledPartIds.Count}个";

        _statsOverview.AddThemeColorOverride("font_color",
            _config.ThrustToWeightRatio >= 1.0f ? new Color(0.7f, 0.9f, 0.7f) : new Color(0.9f, 0.6f, 0.4f));

        // ── 级段详情 ──
        if (_config.Stages.Count > 0)
        {
            var sb = new System.Text.StringBuilder();
            float payloadAbove = 0;
            for (int i = _config.Stages.Count - 1; i >= 0; i--)
            {
                var stage = _config.Stages[i];
                float dv = stage.DeltaV(payloadAbove);
                float twr = stage.WetMass + payloadAbove > 0
                    ? stage.Thrust / ((stage.WetMass + payloadAbove) * 9.81f) : 0;
                sb.AppendLine($"◆ 第{i + 1}级: {stage.PartIds.Count}件 | {stage.WetMass:F1}t | 推{stage.Thrust:F0}kN | TWR {twr:F2} | \u0394V {dv:F0}m/s");
                payloadAbove += stage.WetMass;
            }
            _stagesDetail.Text = sb.ToString();
            _stagesDetail.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.9f));
        }
        else
        {
            _stagesDetail.Text = "暂无级段数据";
            _stagesDetail.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
        }

        // ── 问题检查 ──
        var issues = _config.GetIssues();
        if (issues.Count > 0)
        {
            _issuesLabel.Text = "\u26a0 检查结果:\n" + string.Join("\n", issues.ConvertAll(s => $"  \u2022 {s}"));
            _issuesLabel.AddThemeColorOverride("font_color",
                _config.IsLaunchReady ? new Color(0.9f, 0.9f, 0.3f) : new Color(1f, 0.4f, 0.3f));
        }
        else
        {
            _issuesLabel.Text = "\u2705 所有检查通过 — 可以发射！";
            _issuesLabel.AddThemeColorOverride("font_color", new Color(0.3f, 1f, 0.3f));
        }

        _launchBtn.Disabled = !_config.IsLaunchReady;
        _launchBtn.Text = _config.IsLaunchReady
            ? "\U0001f680 准备发射！（切换至室外）"
            : "\u26d4 不满足发射条件";
    }

    private void OnLaunchPressed()
    {
        if (!_config.IsLaunchReady) return;
        OnLaunchRequested?.Invoke(_padEntityId, _padWorldPos, _config);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                          样式
    // ═══════════════════════════════════════════════════════════════════════

    private static Color GetCategoryColor(string cat) => cat switch
    {
        "Capsule"   => new Color(0.3f, 0.7f, 1f),
        "FuelTank"  => new Color(0.8f, 0.6f, 0.2f),
        "Engine"    => new Color(1f, 0.4f, 0.2f),
        "Booster"   => new Color(1f, 0.6f, 0.3f),
        "Structure" => new Color(0.5f, 0.5f, 0.5f),
        "Aero"      => new Color(0.4f, 0.8f, 0.4f),
        "Utility"   => new Color(0.6f, 0.6f, 0.9f),
        "Science"   => new Color(0.9f, 0.9f, 0.3f),
        _ => Colors.Gray,
    };

    private static StyleBoxFlat MakeVABStyle() => new()
    {
        BgColor = new Color(0.04f, 0.04f, 0.08f, 0.95f),
        CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12,
        CornerRadiusBottomLeft = 12, CornerRadiusBottomRight = 12,
        ContentMarginLeft = 12, ContentMarginRight = 12,
        ContentMarginTop = 8, ContentMarginBottom = 8,
        BorderWidthTop = 2, BorderWidthBottom = 2,
        BorderWidthLeft = 2, BorderWidthRight = 2,
        BorderColor = new Color(0.15f, 0.3f, 0.7f, 0.8f),
    };

    private static StyleBoxFlat MakeSectionStyle(Color bg) => new()
    {
        BgColor = bg,
        CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6,
        CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
        ContentMarginLeft = 8, ContentMarginRight = 8,
        ContentMarginTop = 6, ContentMarginBottom = 6,
        BorderWidthTop = 1, BorderWidthBottom = 1,
        BorderWidthLeft = 1, BorderWidthRight = 1,
        BorderColor = new Color(0.2f, 0.3f, 0.5f, 0.5f),
    };
}
