using Godot;

namespace Ark.UI;

/// <summary>
/// 选择 + 战斗 HUD — 显示：
///   ① 当前控制角色名称 + 武器 + 弹药
///   ② 光标模式下选中物体的信息框
///   ③ 选中物体的 3D 框线
///
/// 由 GameBootstrap 每帧调用 Update* 方法刷新数据。
/// </summary>
public partial class SelectionHUD : CanvasLayer
{
    // ═══════════════════════════════════════════════════════════════════════
    //                          左下：角色 + 武器 + 弹药
    // ═══════════════════════════════════════════════════════════════════════

    private Label _charNameLabel = null!;
    private Label _weaponLabel   = null!;
    private Label _ammoLabel     = null!;
    private Label _weaponDebugLabel = null!;

    // ═══════════════════════════════════════════════════════════════════════
    //                          中下：选中物体信息框
    // ═══════════════════════════════════════════════════════════════════════

    private PanelContainer _selectionPanel  = null!;
    private Label          _selectionTitle  = null!;
    private Label          _selectionDetail = null!;
    private bool           _selectionVisible;
    private float          _selectionTimer;

    // ═══════════════════════════════════════════════════════════════════════
    //                          构建 UI
    // ═══════════════════════════════════════════════════════════════════════

    public override void _Ready()
    {
        Layer = 8;

        BuildCharacterPanel();
        BuildSelectionPanel();
    }

    private void BuildCharacterPanel()
    {
        var box = new VBoxContainer();
        box.AnchorLeft   = 0;
        box.AnchorTop    = 1;
        box.AnchorRight  = 0;
        box.AnchorBottom = 1;
        box.OffsetLeft   = 16;
        box.OffsetTop    = -120;
        box.OffsetRight  = 300;
        box.OffsetBottom = -10;
        box.GrowHorizontal = Control.GrowDirection.End;
        box.GrowVertical   = Control.GrowDirection.Begin;
        AddChild(box);

        _charNameLabel = MakeLabel(17, new Color(0.9f, 0.9f, 0.95f));
        box.AddChild(_charNameLabel);

        _weaponLabel = MakeLabel(14, new Color(0.75f, 0.85f, 1f));
        box.AddChild(_weaponLabel);

        _ammoLabel = MakeLabel(22, new Color(1f, 0.95f, 0.6f));
        box.AddChild(_ammoLabel);

        _weaponDebugLabel = MakeLabel(12, new Color(0.7f, 0.82f, 0.95f));
        box.AddChild(_weaponDebugLabel);
    }

    private void BuildSelectionPanel()
    {
        _selectionPanel = new PanelContainer();
        _selectionPanel.AnchorLeft   = 0.5f;
        _selectionPanel.AnchorTop    = 1;
        _selectionPanel.AnchorRight  = 0.5f;
        _selectionPanel.AnchorBottom = 1;
        _selectionPanel.OffsetLeft   = -160;
        _selectionPanel.OffsetTop    = -80;
        _selectionPanel.OffsetRight  = 160;
        _selectionPanel.OffsetBottom = -10;
        _selectionPanel.GrowHorizontal = Control.GrowDirection.Both;
        _selectionPanel.GrowVertical   = Control.GrowDirection.Begin;

        // 半透明背景
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0, 0, 0, 0.55f),
            BorderColor = new Color(0.4f, 0.7f, 1f, 0.8f),
            BorderWidthBottom = 2,
            BorderWidthTop = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            ContentMarginLeft = 12,
            ContentMarginRight = 12,
            ContentMarginTop = 8,
            ContentMarginBottom = 8,
        };
        _selectionPanel.AddThemeStyleboxOverride("panel", style);
        _selectionPanel.Visible = false;
        AddChild(_selectionPanel);

        var vbox = new VBoxContainer();
        _selectionPanel.AddChild(vbox);

        _selectionTitle = MakeLabel(16, new Color(0.5f, 0.85f, 1f));
        _selectionTitle.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(_selectionTitle);

        _selectionDetail = MakeLabel(13, new Color(0.7f, 0.75f, 0.8f));
        _selectionDetail.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(_selectionDetail);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                          公开更新 API
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>更新当前控制角色信息。</summary>
    public void UpdateCharacterInfo(string charName, string weaponName, int currentMag, int magCapacity, int reserve)
        => UpdateCharacterInfo(charName, weaponName, currentMag, magCapacity, reserve, string.Empty);

    public void UpdateCharacterInfo(string charName, string weaponName, int currentMag, int magCapacity, int reserve, string debugInfo)
    {
        _charNameLabel.Text = charName;
        _weaponLabel.Text   = $"🔫 {weaponName}";

        string ammoColor = currentMag <= 5 ? "[color=#ff6644]" : "[color=#ffe866]";
        _ammoLabel.Text = $"{currentMag} / {magCapacity}  [{reserve}]";

        // 弹量低时变红
        if (currentMag <= 5 && magCapacity > 0)
            _ammoLabel.AddThemeColorOverride("font_color", new Color(1f, 0.35f, 0.2f));
        else
            _ammoLabel.AddThemeColorOverride("font_color", new Color(1f, 0.95f, 0.6f));

        _weaponDebugLabel.Text = string.IsNullOrWhiteSpace(debugInfo) ? string.Empty : debugInfo;
    }

    /// <summary>显示选中物体信息框。</summary>
    public void ShowSelection(string title, string detail, float duration = 5f)
    {
        _selectionTitle.Text  = title;
        _selectionDetail.Text = detail;
        _selectionPanel.Visible = true;
        _selectionVisible = true;
        _selectionTimer = duration;
    }

    /// <summary>隐藏选中信息框。</summary>
    public void HideSelection()
    {
        _selectionPanel.Visible = false;
        _selectionVisible = false;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                          帧更新
    // ═══════════════════════════════════════════════════════════════════════

    public override void _Process(double delta)
    {
        if (_selectionVisible)
        {
            _selectionTimer -= (float)delta;
            if (_selectionTimer <= 0)
                HideSelection();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //                          辅助
    // ═══════════════════════════════════════════════════════════════════════

    private static Label MakeLabel(int fontSize, Color color)
    {
        var label = new Label();
        label.AddThemeFontSizeOverride("font_size", ArkTheme.ScaledFontSize(fontSize));
        label.AddThemeColorOverride("font_color", color);
        return label;
    }
}
