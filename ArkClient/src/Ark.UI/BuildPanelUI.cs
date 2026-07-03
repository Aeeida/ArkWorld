using Godot;
using System;
using Ark.Bridge.Features.BaseBuilding;

namespace Ark.UI;

/// <summary>
/// 建造模式右侧面板 — 显示可选建筑列表，点击后通知 BuildPlacementController。
/// 面板随 B 键切换显示/隐藏。
/// </summary>
public partial class BuildPanelUI : CanvasLayer
{
    // ─── 尺寸常量 ───
    private const int PanelWidth     = 220;
    private const int ButtonHeight   = 80;
    private const int PanelPadding   = 8;
    private const int TitleHeight    = 36;

    // ─── 节点引用 ───
    private PanelContainer?  _panelContainer;
    private VBoxContainer?   _btnBox;
    private Label?           _titleLabel;
    private Label?           _statusLabel;
    private Button?          _exitBtn;

    // ─── 状态 ───
    private int         _selectedTypeId;
    private Button?     _selectedButton;

    // ─── 事件：玩家从面板选了某个建筑类型 ───
    public event Action<int>? OnBuildingSelected;  // typeId
    public event Action?      OnPanelClosed;

    public override void _Ready()
    {
        Layer = 5; // 覆盖在 HUD 之上
        BuildUI();
        Hide();    // 默认隐藏，按 B 打开
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                          构建 UI 树
    // ═══════════════════════════════════════════════════════════════════════

    private void BuildUI()
    {
        // ─── 外层 Panel 容器（右侧锚点）───
        _panelContainer = new PanelContainer { Name = "BuildPanel" };
        _panelContainer.AnchorLeft   = 1f;
        _panelContainer.AnchorRight  = 1f;
        _panelContainer.AnchorTop    = 0f;
        _panelContainer.AnchorBottom = 1f;
        _panelContainer.OffsetLeft   = -PanelWidth;
        _panelContainer.OffsetRight  = 0f;
        _panelContainer.OffsetTop    = 0f;
        _panelContainer.OffsetBottom = 0f;
        _panelContainer.MouseFilter  = Control.MouseFilterEnum.Stop;
        _panelContainer.AddThemeStyleboxOverride("panel", MakePanelStyle());
        AddChild(_panelContainer);

        // ─── 内层垂直布局 ───
        var vbox = new VBoxContainer { Name = "VBox" };
        vbox.AddThemeConstantOverride("separation", PanelPadding);
        vbox.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect,
                                        Control.LayoutPresetMode.KeepSize,
                                        PanelPadding);
        _panelContainer.AddChild(vbox);

        // ─── 标题 ───
        _titleLabel = new Label
        {
            Text                = "🏗  建造模式",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _titleLabel.AddThemeFontSizeOverride("font_size", ArkTheme.ScaledFontSize(16));
        _titleLabel.AddThemeColorOverride("font_color", new Color(1f, 0.9f, 0.5f));
        vbox.AddChild(_titleLabel);

        // ─── 分隔线 ───
        vbox.AddChild(MakeSeparator());

        // ─── 建筑按钮列表 ───
        _btnBox = new VBoxContainer { Name = "BtnBox" };
        _btnBox.MouseFilter = Control.MouseFilterEnum.Pass;
        _btnBox.AddThemeConstantOverride("separation", 4);
        vbox.AddChild(_btnBox);

        foreach (var def in BuildingDef.All)
        {
            var btn = MakeBuildingButton(def);
            _btnBox.AddChild(btn);
        }

        // ─── 分隔线 ───
        vbox.AddChild(MakeSeparator());

        // ─── 状态文字（提示当前选中）───
        _statusLabel = new Label
        {
            Text                = "点击选择建筑",
            AutowrapMode        = TextServer.AutowrapMode.Word,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _statusLabel.AddThemeFontSizeOverride("font_size", ArkTheme.ScaledFontSize(11));
        _statusLabel.AddThemeColorOverride("font_color", new Color(0.75f, 0.75f, 0.75f));
        vbox.AddChild(_statusLabel);

        // ─── 提示文字 ───
        var hintLabel = new Label
        {
            Text                = "[LMB] 放置   [RMB/B] 取消\n[R] 旋转 45°",
            AutowrapMode        = TextServer.AutowrapMode.Word,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        hintLabel.AddThemeFontSizeOverride("font_size", ArkTheme.ScaledFontSize(10));
        hintLabel.AddThemeColorOverride("font_color", new Color(0.55f, 0.55f, 0.55f));
        vbox.AddChild(hintLabel);

        // ─── 填充（把退出按钮推到底部）───
        vbox.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });

        // ─── 退出建造模式按钮 ───
        _exitBtn = new Button
        {
            Text          = "✖  退出建造模式  [B]",
            CustomMinimumSize = new Vector2(0, 34)
        };
        _exitBtn.AddThemeColorOverride("font_color",         new Color(1f, 0.4f, 0.4f));
        _exitBtn.AddThemeColorOverride("font_hover_color",   new Color(1f, 0.6f, 0.6f));
        _exitBtn.Pressed += () => OnPanelClosed?.Invoke();
        vbox.AddChild(_exitBtn);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                          按钮工厂
    // ═══════════════════════════════════════════════════════════════════════

    private Button MakeBuildingButton(BuildingDef.Def def)
    {
        var btn = new Button
        {
            Name              = $"Btn_{def.TypeId}",
            CustomMinimumSize = new Vector2(0, ButtonHeight),
            TooltipText       = def.Description,
            ToggleMode        = true,
            MouseFilter       = Control.MouseFilterEnum.Stop,
        };
        btn.AddThemeFontSizeOverride("font_size", ArkTheme.ScaledFontSize(13));

        // 自定义内容：图标 + 名称 + 建造时间
        // Godot Button.Text 支持 BBCode 的方法是用 RichTextLabel，
        // 这里直接用多行文本简化处理
        btn.Text = $"{def.Icon}  {def.Name}\n" +
                   $"⏱ {def.BuildTime:F0}s   " +
                   $"📐 {def.Size.X:F0}×{def.Size.Z:F0}";

        btn.Pressed += () => SelectBuilding(def.TypeId, btn);
        return btn;
    }

    private static HSeparator MakeSeparator()
    {
        var sep = new HSeparator();
        sep.AddThemeColorOverride("color", new Color(0.4f, 0.4f, 0.4f));
        return sep;
    }

    private static StyleBoxFlat MakePanelStyle()
    {
        return new StyleBoxFlat
        {
            BgColor          = new Color(0.10f, 0.10f, 0.12f, 0.92f),
            BorderColor      = new Color(0.35f, 0.35f, 0.40f, 1f),
            BorderWidthLeft  = 1,
            BorderWidthTop   = 1,
            BorderWidthRight = 0,
            BorderWidthBottom= 1,
            CornerRadiusTopLeft    = 6,
            CornerRadiusBottomLeft = 6,
            ContentMarginLeft   = (int)PanelPadding,
            ContentMarginRight  = (int)PanelPadding,
            ContentMarginTop    = (int)PanelPadding,
            ContentMarginBottom = (int)PanelPadding,
        };
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                          选择逻辑
    // ═══════════════════════════════════════════════════════════════════════

    private void SelectBuilding(int typeId, Button btn)
    {
        // 取消上一个按钮的高亮
        if (_selectedButton != null && _selectedButton != btn)
        {
            _selectedButton.ButtonPressed = false;
        }

        _selectedTypeId  = typeId;
        _selectedButton  = btn;
        btn.ButtonPressed = true;

        var def = BuildingDef.Get(typeId);
        if (def.HasValue)
        {
            _statusLabel!.Text = $"已选：{def.Value.Icon} {def.Value.Name}\n{def.Value.Description}";
        }

        OnBuildingSelected?.Invoke(typeId);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                          公开 API
    // ═══════════════════════════════════════════════════════════════════════

    public void ShowPanel()
    {
        Show();
        // 默认选中第一个建筑
        if (_selectedTypeId == 0 && _btnBox?.GetChildCount() > 0)
        {
            var firstBtn = _btnBox.GetChild<Button>(0);
            SelectBuilding(BuildingDef.All[0].TypeId, firstBtn);
        }
    }

    public void HidePanel()
    {
        Hide();
        _selectedTypeId = 0;
        if (_selectedButton != null)
        {
            _selectedButton.ButtonPressed = false;
            _selectedButton = null;
        }
        _statusLabel!.Text = "点击选择建筑";
    }

    public bool IsPointerOverPanel(Vector2 screenPosition)
    {
        return Visible
            && _panelContainer is not null
            && _panelContainer.Visible
            && _panelContainer.GetGlobalRect().HasPoint(screenPosition);
    }

    public int SelectedTypeId => _selectedTypeId;
}
